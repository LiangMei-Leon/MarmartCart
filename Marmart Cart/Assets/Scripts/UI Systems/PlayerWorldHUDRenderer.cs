using Shapes;
using UnityEngine;

/// <summary>
/// Step 5D.2.3 renderer - Near-Camera Render Plane architecture test.
///
///
/// LOAD normal mode:
/// - 1..20 uses the original growing bar;
/// - 21 starts a new 21..40 rolling window;
/// - 41 starts 41..60, etc.;
/// - Pre-Slots represent future capacity inside the current window;
/// - decade milestone ticks show 10/20/30/40/...;
/// - current tick always shows the true Current Load.
///
/// LOAD overload mode:
/// - overload never creates extra slots;
/// - top loaded slots turn overload color;
/// - current tick follows the lowest overload capsule;
/// - once overload fills the whole visible bar, the pointer loops
///   from top -> bottom again while all slots stay overload-colored.
///
/// HYPE remains the stable track + fill.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;
    [SerializeField] private PlayerWorldHUDLayoutProfile layoutProfile;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;
    [SerializeField] private bool skipWhenBehindCamera = true;

    #endregion

    #region Unity

    private void Awake()
    {
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        if (stateSystem == null) stateSystem = FindFirstObjectByType<PlayerWorldHUDStateSystem>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;
        if (cam == null || hudSystem == null || layoutProfile == null) return;

        if (!hudSystem.TryGetRenderableSlotForCamera(cam, out int playerIndex, out Transform hudAnchor)) return;
        if (hudAnchor == null) return;

        // The REAL cart anchor determines only where the HUD belongs on-screen.
        Vector3 anchorScreen = cam.WorldToScreenPoint(hudAnchor.position);
        if (skipWhenBehindCamera && anchorScreen.z <= 0f) return;

        // Optionally reconstruct that same screen X/Y on a fixed plane close to
        // this gameplay camera. The HUD still tracks the cart visually, but it
        // no longer physically intersects shelves, the cart, or the ground.
        Vector3 renderAnchorWorld = hudAnchor.position;

        if (layoutProfile.UseNearCameraRenderPlane)
        {
            float renderDepth = Mathf.Max(
                layoutProfile.NearCameraRenderDistance,
                cam.nearClipPlane + layoutProfile.NearClipSafetyPadding
            );

            anchorScreen.z = renderDepth;
            renderAnchorWorld = cam.ScreenToWorldPoint(anchorScreen);
        }

        float hypeNormalized = 0f;
        int safeCapacityCount = 0;
        int currentLoadCount = 0;
        int overloadCount = 0;

        if (stateSystem != null &&
            stateSystem.TryGetState(playerIndex, out PlayerWorldHUDState state) &&
            state != null)
        {
            hypeNormalized = state.HypeNormalized;
            safeCapacityCount = Mathf.Max(0, Mathf.RoundToInt(state.SafeCapacity));
            currentLoadCount = Mathf.Max(0, Mathf.RoundToInt(state.CurrentLoad));

            overloadCount = Mathf.Max(
                0,
                Mathf.Max(
                    Mathf.RoundToInt(state.OverloadAmount),
                    currentLoadCount - safeCapacityCount
                )
            );
        }

        Vector2 loadCenterOffset = new Vector2(
            -layoutProfile.HorizontalSeparationPixels,
            layoutProfile.VerticalOffsetPixels
        );

        Vector2 hypeCenterOffset = new Vector2(
            layoutProfile.HorizontalSeparationPixels,
            layoutProfile.VerticalOffsetPixels
        );

        Vector3 loadCenterWorld = ScreenOffsetToWorld(
            cam,
            renderAnchorWorld,
            loadCenterOffset
        );

        Vector3 hypeCenterWorld = ScreenOffsetToWorld(
            cam,
            renderAnchorWorld,
            hypeCenterOffset
        );

        Quaternion screenPlaneRotation = cam.transform.rotation;

        using (Draw.Command(cam))
        {
            Draw.ResetAllDrawStates();

            Draw.BlendMode = ShapesBlendMode.Transparent;

            // No custom depth test override in this architecture.
            // The HUD wins normal depth testing because all of it is rendered
            // physically close to this gameplay camera.
            Draw.RadiusSpace = ThicknessSpace.Pixels;
            Draw.ThicknessSpace = ThicknessSpace.Pixels;
            Draw.LineGeometry = LineGeometry.Billboard;
            Draw.LineEndCaps = LineEndCap.Round;

            // Draw Hype first. Load is drawn second so its ticks/text are the
            // final HUD elements submitted by this drawer.
            DrawHypeMeter(
                cam,
                hypeCenterWorld,
                screenPlaneRotation,
                hypeNormalized
            );

            DrawLoadMeter(
                cam,
                loadCenterWorld,
                screenPlaneRotation,
                safeCapacityCount,
                currentLoadCount,
                overloadCount
            );
        }
    }

    #endregion

    #region Load Meter

    private void DrawLoadMeter(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        int safeCapacityCount,
        int currentLoadCount,
        int overloadCount)
    {
        DrawFullRoundedArc(
            cam,
            centerWorld,
            rotation,
            layoutProfile.LoadRadiusPixels,
            layoutProfile.LoadTrackThicknessPixels,
            layoutProfile.LoadCenterAngleDegrees,
            layoutProfile.LoadSpanDegrees,
            layoutProfile.LoadTrackColor
        );

        bool overloaded = overloadCount > 0;

        GetLoadWindow(
            safeCapacityCount,
            overloaded,
            out int windowBase,
            out int visibleSlotCount,
            out int safeSlotsInWindow
        );

        float capsuleThickness = layoutProfile.GetLoadCapsuleThicknessPixels(
            visibleSlotCount
        );

        float capsuleLength = layoutProfile.LoadCapsuleLengthPixels;
        float halfSpan = layoutProfile.LoadSpanDegrees * 0.5f;

        float bottomDegrees =
            layoutProfile.LoadCenterAngleDegrees + halfSpan;

        float topDegrees =
            layoutProfile.LoadCenterAngleDegrees - halfSpan;

        Vector3 centerScreen =
            cam.WorldToScreenPoint(centerWorld);

        Color overloadColor =
            layoutProfile.GetLoadOverloadColor(overloadCount);

        int safeLoadedTotal =
            Mathf.Min(currentLoadCount, safeCapacityCount);

        int normalLoadedInWindow = 0;
        int redVisibleCount = 0;

        if (!overloaded)
        {
            normalLoadedInWindow = Mathf.Clamp(
                safeLoadedTotal - windowBase,
                0,
                safeSlotsInWindow
            );
        }
        else
        {
            // In overload mode the currently visible bar is a warning view of
            // the latest loaded safe slots, so it remains visually full/readable.
            normalLoadedInWindow = visibleSlotCount;

            redVisibleCount = Mathf.Clamp(
                overloadCount,
                0,
                visibleSlotCount
            );

            normalLoadedInWindow -= redVisibleCount;
        }

        for (int i = 0; i < visibleSlotCount; i++)
        {
            float angleDegrees = GetSlotAngleDegrees(
                i,
                visibleSlotCount,
                bottomDegrees,
                topDegrees
            );

            Color capsuleColor;

            if (overloaded)
            {
                if (i < normalLoadedInWindow)
                {
                    capsuleColor = layoutProfile.LoadFilledColor;
                }
                else
                {
                    capsuleColor = overloadColor;
                }
            }
            else
            {
                if (i >= safeSlotsInWindow)
                {
                    capsuleColor = layoutProfile.LoadPreSlotColor;
                }
                else if (i < normalLoadedInWindow)
                {
                    capsuleColor = layoutProfile.LoadFilledColor;
                }
                else
                {
                    capsuleColor = layoutProfile.LoadEmptyColor;
                }
            }

            DrawRadialCapsule(
                cam,
                centerScreen,
                angleDegrees * Mathf.Deg2Rad,
                layoutProfile.LoadRadiusPixels,
                capsuleLength,
                capsuleThickness,
                capsuleColor
            );
        }

        DrawLoadMilestoneTicks(
            cam,
            centerWorld,
            rotation,
            centerScreen,
            windowBase,
            visibleSlotCount,
            bottomDegrees,
            topDegrees,
            currentLoadCount,
            overloaded
        );

        DrawCurrentLoadTick(
            cam,
            centerWorld,
            rotation,
            centerScreen,
            windowBase,
            visibleSlotCount,
            bottomDegrees,
            topDegrees,
            currentLoadCount,
            overloadCount,
            overloadColor
        );
    }

    /// <summary>
    /// Normal mode:
    ///
    /// Safe 0..10:
    ///     base 0, visible 10
    ///
    /// Safe 11..20:
    ///     base 0, visible = Safe
    ///
    /// Safe 21:
    ///     base 20, visible 20, safe-in-window 1
    ///
    /// Safe 40:
    ///     base 20, visible 20, safe-in-window 20
    ///
    /// Safe 41:
    ///     base 40, visible 20, safe-in-window 1
    ///
    /// Overload mode:
    ///     show the latest up-to-20 safe slots as a full warning bar instead
    ///     of showing mostly Pre-Slots on a newly-started capacity page.
    /// </summary>
    private void GetLoadWindow(
        int safeCapacityCount,
        bool overloaded,
        out int windowBase,
        out int visibleSlotCount,
        out int safeSlotsInWindow)
    {
        int minimum = layoutProfile.MinimumVisualSlotCount;
        int windowSize = layoutProfile.LoadWindowSize;

        if (overloaded)
        {
            visibleSlotCount = Mathf.Clamp(
                Mathf.Max(safeCapacityCount, minimum),
                minimum,
                windowSize
            );

            windowBase = Mathf.Max(
                0,
                safeCapacityCount - visibleSlotCount
            );

            safeSlotsInWindow = visibleSlotCount;
            return;
        }

        if (safeCapacityCount <= windowSize)
        {
            windowBase = 0;

            visibleSlotCount = Mathf.Clamp(
                Mathf.Max(safeCapacityCount, minimum),
                minimum,
                windowSize
            );

            safeSlotsInWindow = Mathf.Clamp(
                safeCapacityCount,
                0,
                visibleSlotCount
            );

            return;
        }

        windowBase =
            ((safeCapacityCount - 1) / windowSize) * windowSize;

        visibleSlotCount = windowSize;

        safeSlotsInWindow = Mathf.Clamp(
            safeCapacityCount - windowBase,
            0,
            windowSize
        );
    }

    private void DrawRadialCapsule(
        Camera cam,
        Vector3 centerScreen,
        float angleRadians,
        float radiusPixels,
        float lengthPixels,
        float thicknessPixels,
        Color color)
    {
        Vector2 radial =
            ShapesMath.AngToDir(angleRadians);

        Vector2 capsuleCenter =
            new Vector2(
                centerScreen.x + radial.x * radiusPixels,
                centerScreen.y + radial.y * radiusPixels
            );

        Vector2 halfRadial =
            radial * (lengthPixels * 0.5f);

        Vector3 screenA =
            new Vector3(
                capsuleCenter.x - halfRadial.x,
                capsuleCenter.y - halfRadial.y,
                centerScreen.z
            );

        Vector3 screenB =
            new Vector3(
                capsuleCenter.x + halfRadial.x,
                capsuleCenter.y + halfRadial.y,
                centerScreen.z
            );

        Draw.Line(
            cam.ScreenToWorldPoint(screenA),
            cam.ScreenToWorldPoint(screenB),
            thicknessPixels,
            color
        );
    }

    #endregion

    #region Load Ticks

    private void DrawLoadMilestoneTicks(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        Vector3 centerScreen,
        int windowBase,
        int visibleSlotCount,
        float bottomDegrees,
        float topDegrees,
        int currentLoadCount,
        bool overloaded)
    {
        if (!layoutProfile.ShowLoadMilestoneTicks) return;

        int firstValue = windowBase + 1;
        int lastValue = windowBase + visibleSlotCount;
        int interval = layoutProfile.LoadMilestoneInterval;

        int firstMilestone =
            Mathf.CeilToInt(firstValue / (float)interval) * interval;

        for (
            int milestone = firstMilestone;
            milestone <= lastValue;
            milestone += interval)
        {
            // If current pointer already sits at exactly this non-overload
            // value, let the brighter current tick own that location.
            if (!overloaded && milestone == currentLoadCount)
            {
                continue;
            }

            int localIndex =
                milestone - windowBase - 1;

            if (localIndex < 0 ||
                localIndex >= visibleSlotCount)
            {
                continue;
            }

            float angleDegrees =
                GetSlotAngleDegrees(
                    localIndex,
                    visibleSlotCount,
                    bottomDegrees,
                    topDegrees
                );

            DrawLoadTickAndLabel(
                cam,
                centerWorld,
                rotation,
                centerScreen,
                angleDegrees,
                layoutProfile.LoadMilestoneTickLengthPixels,
                layoutProfile.LoadMilestoneTickThicknessPixels,
                layoutProfile.LoadMilestoneTickColor,
                layoutProfile.LoadMilestoneTextColor,
                layoutProfile.LoadMilestoneFontSizePixels,
                layoutProfile.LoadMilestoneLabelOffsetPixels,
                milestone.ToString()
            );
        }
    }

    private void DrawCurrentLoadTick(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        Vector3 centerScreen,
        int windowBase,
        int visibleSlotCount,
        float bottomDegrees,
        float topDegrees,
        int currentLoadCount,
        int overloadCount,
        Color overloadColor)
    {
        if (!layoutProfile.ShowCurrentLoadTick) return;

        int pointerIndex;

        if (overloadCount > 0)
        {
            // Overload pointer moves TOP -> BOTTOM.
            //
            // visible=10:
            // overload 1  -> index 9 (top)
            // overload 10 -> index 0 (bottom)
            // overload 11 -> index 9 (top again)
            int loopPosition =
                (overloadCount - 1) % visibleSlotCount;

            pointerIndex =
                visibleSlotCount - 1 - loopPosition;
        }
        else if (currentLoadCount <= 0)
        {
            pointerIndex = 0;
        }
        else
        {
            int localValue =
                currentLoadCount - windowBase;

            pointerIndex = Mathf.Clamp(
                localValue - 1,
                0,
                visibleSlotCount - 1
            );
        }

        float angleDegrees =
            GetSlotAngleDegrees(
                pointerIndex,
                visibleSlotCount,
                bottomDegrees,
                topDegrees
            );

        Color tickColor =
            overloadCount > 0
                ? overloadColor
                : layoutProfile.CurrentLoadTickColor;

        DrawLoadTickAndLabel(
            cam,
            centerWorld,
            rotation,
            centerScreen,
            angleDegrees,
            layoutProfile.CurrentLoadTickLengthPixels,
            layoutProfile.CurrentLoadTickThicknessPixels,
            tickColor,
            layoutProfile.CurrentLoadTextColor,
            layoutProfile.CurrentLoadFontSizePixels,
            layoutProfile.CurrentLoadLabelOffsetPixels,
            currentLoadCount.ToString()
        );
    }

    private void DrawLoadTickAndLabel(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        Vector3 centerScreen,
        float angleDegrees,
        float tickLengthPixels,
        float tickThicknessPixels,
        Color tickColor,
        Color textColor,
        float fontSizePixels,
        float labelOffsetPixels,
        string label)
    {
        float angleRadians =
            angleDegrees * Mathf.Deg2Rad;

        Vector2 radial =
            ShapesMath.AngToDir(angleRadians);

        float outerRadius =
            layoutProfile.LoadRadiusPixels +
            layoutProfile.LoadTrackThicknessPixels * 0.5f;

        Vector2 tickStartScreen =
            new Vector2(
                centerScreen.x + radial.x * outerRadius,
                centerScreen.y + radial.y * outerRadius
            );

        Vector2 tickEndScreen =
            tickStartScreen +
            radial * tickLengthPixels;

        Vector3 startScreen3 =
            new Vector3(
                tickStartScreen.x,
                tickStartScreen.y,
                centerScreen.z
            );

        Vector3 endScreen3 =
            new Vector3(
                tickEndScreen.x,
                tickEndScreen.y,
                centerScreen.z
            );

        Draw.Line(
            cam.ScreenToWorldPoint(startScreen3),
            cam.ScreenToWorldPoint(endScreen3),
            tickThicknessPixels,
            tickColor
        );

        Vector2 labelScreen =
            tickEndScreen +
            radial * labelOffsetPixels;

        Vector3 labelScreen3 =
            new Vector3(
                labelScreen.x,
                labelScreen.y,
                centerScreen.z
            );

        Vector3 labelWorld =
            cam.ScreenToWorldPoint(labelScreen3);

        // Shapes text lives on the same near-camera plane as the rest of the
        // HUD now, so it needs no separate depth workaround.
        Draw.FontSize =
            PixelsToWorldSizeAtDepth(
                cam,
                labelWorld,
                fontSizePixels
            );

        Color previousColor = Draw.Color;
        Draw.Color = textColor;

        Draw.Text(
            labelWorld,
            rotation,
            label,
            TextAlign.Right
        );

        Draw.Color = previousColor;
    }

    #endregion

    #region Hype Meter

    private void DrawHypeMeter(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        float normalizedValue)
    {
        DrawFullRoundedArc(
            cam,
            centerWorld,
            rotation,
            layoutProfile.HypeRadiusPixels,
            layoutProfile.HypeTrackThicknessPixels,
            layoutProfile.HypeCenterAngleDegrees,
            layoutProfile.HypeSpanDegrees,
            layoutProfile.HypeTrackColor
        );

        float value =
            Mathf.Clamp01(normalizedValue);

        if (value <= 0f) return;

        float halfSpan =
            layoutProfile.HypeSpanDegrees * 0.5f;

        float bottomDegrees =
            layoutProfile.HypeCenterAngleDegrees - halfSpan;

        float topDegrees =
            layoutProfile.HypeCenterAngleDegrees + halfSpan;

        float fillEndDegrees =
            Mathf.Lerp(
                bottomDegrees,
                topDegrees,
                value
            );

        DrawRoundedArcSection(
            cam,
            centerWorld,
            rotation,
            layoutProfile.HypeRadiusPixels,
            layoutProfile.HypeFillThicknessPixels,
            bottomDegrees,
            fillEndDegrees,
            layoutProfile.HypeFillColor
        );
    }

    #endregion

    #region Rounded Arc Drawing

    private void DrawFullRoundedArc(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        float radiusPixels,
        float thicknessPixels,
        float centerAngleDegrees,
        float spanDegrees,
        Color color)
    {
        float halfSpan =
            spanDegrees * 0.5f;

        DrawRoundedArcSection(
            cam,
            centerWorld,
            rotation,
            radiusPixels,
            thicknessPixels,
            centerAngleDegrees - halfSpan,
            centerAngleDegrees + halfSpan,
            color
        );
    }

    private void DrawRoundedArcSection(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        float radiusPixels,
        float thicknessPixels,
        float startDegrees,
        float endDegrees,
        Color color)
    {
        if (thicknessPixels <= 0f) return;
        if (Mathf.Approximately(startDegrees, endDegrees)) return;

        float startRadians =
            startDegrees * Mathf.Deg2Rad;

        float endRadians =
            endDegrees * Mathf.Deg2Rad;

        Draw.Arc(
            centerWorld,
            rotation,
            radiusPixels,
            thicknessPixels,
            startRadians,
            endRadians,
            color
        );

        Vector3 centerScreen =
            cam.WorldToScreenPoint(centerWorld);

        Vector3 startWorld =
            ScreenPointOnArcToWorld(
                cam,
                centerScreen,
                radiusPixels,
                startRadians
            );

        Vector3 endWorld =
            ScreenPointOnArcToWorld(
                cam,
                centerScreen,
                radiusPixels,
                endRadians
            );

        float capRadiusPixels =
            thicknessPixels * 0.5f;

        DrawHalfCircleCap(
            startWorld,
            rotation,
            capRadiusPixels,
            startRadians - Mathf.PI,
            startRadians,
            color
        );

        DrawHalfCircleCap(
            endWorld,
            rotation,
            capRadiusPixels,
            endRadians,
            endRadians + Mathf.PI,
            color
        );
    }

    private void DrawHalfCircleCap(
        Vector3 centerWorld,
        Quaternion rotation,
        float capRadiusPixels,
        float startRadians,
        float endRadians,
        Color color)
    {
        if (capRadiusPixels <= 0f) return;

        Draw.Arc(
            centerWorld,
            rotation,
            capRadiusPixels * 0.5f,
            capRadiusPixels,
            startRadians,
            endRadians,
            color
        );
    }

    private Vector3 ScreenPointOnArcToWorld(
        Camera cam,
        Vector3 centerScreen,
        float radiusPixels,
        float angleRadians)
    {
        Vector2 direction =
            ShapesMath.AngToDir(angleRadians);

        Vector3 screenPoint =
            centerScreen;

        screenPoint.x +=
            direction.x * radiusPixels;

        screenPoint.y +=
            direction.y * radiusPixels;

        return cam.ScreenToWorldPoint(
            screenPoint
        );
    }

    #endregion

    #region Helpers

    private float GetSlotAngleDegrees(
        int slotIndex,
        int visibleSlotCount,
        float bottomDegrees,
        float topDegrees)
    {
        if (visibleSlotCount <= 1)
        {
            return Mathf.Lerp(
                bottomDegrees,
                topDegrees,
                0.5f
            );
        }

        float t =
            slotIndex /
            (visibleSlotCount - 1f);

        return Mathf.Lerp(
            bottomDegrees,
            topDegrees,
            t
        );
    }

    private Vector3 ScreenOffsetToWorld(
        Camera cam,
        Vector3 anchorWorld,
        Vector2 pixelOffset)
    {
        Vector3 screenPoint =
            cam.WorldToScreenPoint(anchorWorld);

        screenPoint.x += pixelOffset.x;
        screenPoint.y += pixelOffset.y;

        return cam.ScreenToWorldPoint(
            screenPoint
        );
    }

    private float PixelsToWorldSizeAtDepth(
        Camera cam,
        Vector3 worldPosition,
        float pixelSize)
    {
        Vector3 screenA =
            cam.WorldToScreenPoint(worldPosition);

        Vector3 screenB =
            screenA;

        screenB.y += pixelSize;

        Vector3 worldA =
            cam.ScreenToWorldPoint(screenA);

        Vector3 worldB =
            cam.ScreenToWorldPoint(screenB);

        return Vector3.Distance(
            worldA,
            worldB
        );
    }

    #endregion
}
