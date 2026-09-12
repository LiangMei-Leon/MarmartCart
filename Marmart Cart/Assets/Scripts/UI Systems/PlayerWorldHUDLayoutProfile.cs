using UnityEngine;

/// <summary>
/// Step 5D.2 visual profile.
///
/// LOAD now supports:
/// - minimum 10-slot visual baseline;
/// - 20-slot rolling capacity windows;
/// - dynamic capsule thickness;
/// - Pre/Empty/Filled/Overload states;
/// - decade milestone ticks;
/// - moving current-load tick + numeric label;
/// - looping overload pointer;
/// - overload severity gradient.
///
/// HYPE remains the stable background + current fill from Step 5B.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Player HUD/World HUD Layout Profile",
    fileName = "PlayerWorldHUDLayoutProfile"
)]
public class PlayerWorldHUDLayoutProfile : ScriptableObject
{
    #region Pair Placement

    [Header("Pair Placement")]
    [Min(0.01f)]
    [SerializeField] private float masterScale = 1f;

    [Min(0f)]
    [SerializeField] private float horizontalSeparationPixels = 24f;

    [SerializeField] private float verticalOffsetPixels = 0f;

    #endregion

    #region Render Plane

    [Header("Near-Camera Render Plane")]
    [Tooltip(
        "Render the complete world HUD on a fixed plane close to each gameplay camera, " +
        "while preserving the cart HUDWorldAnchor's screen X/Y. " +
        "This lets normal depth testing keep the HUD in front of level geometry without a ZTest override."
    )]
    [SerializeField] private bool useNearCameraRenderPlane = true;

    [Tooltip(
        "Distance in world units from the gameplay camera to the HUD render plane. " +
        "Automatically clamped beyond that camera's Near Clip Plane."
    )]
    [Min(0.01f)]
    [SerializeField] private float nearCameraRenderDistance = 0.5f;

    [Tooltip("Minimum safety distance beyond the camera Near Clip Plane.")]
    [Min(0.001f)]
    [SerializeField] private float nearClipSafetyPadding = 0.03f;

    #endregion

    #region Load Shell

    [Header("LOAD Meter - Left Shell")]
    [Min(1f)]
    [SerializeField] private float loadRadiusPixels = 82f;

    [Min(1f)]
    [SerializeField] private float loadTrackThicknessPixels = 38f;

    [Range(10f, 300f)]
    [SerializeField] private float loadSpanDegrees = 120f;

    [SerializeField] private float loadCenterAngleDegrees = 180f;

    #endregion

    #region Load Capsules

    [Header("Load Capsule Geometry")]
    [Tooltip("Radial length of each capsule across the Load track.")]
    [Min(1f)]
    [SerializeField] private float loadCapsuleLengthPixels = 15f;

    [Header("Load Visible Range")]
    [Tooltip("Minimum number of capsule positions shown before larger capacity exists.")]
    [Min(1)]
    [SerializeField] private int minimumVisualSlotCount = 10;

    [Tooltip(
        "Rolling window size once capacity exceeds the first page. " +
        "The intended design is 20."
    )]
    [Min(2)]
    [SerializeField] private int loadWindowSize = 20;

    [Header("Dynamic Capsule Thickness - Lerp Milestones")]
    [Min(1f)]
    [SerializeField] private float capsuleThicknessAt10Slots = 12f;

    [Min(1f)]
    [SerializeField] private float capsuleThicknessAt15Slots = 9f;

    [Min(1f)]
    [SerializeField] private float capsuleThicknessAt20Slots = 6f;

    [Header("Load Colors")]
    [SerializeField] private Color loadTrackColor = new Color(0.20f, 0.24f, 0.30f, 0.40f);

    [Tooltip("Subtle placeholder for capacity that has not been earned yet.")]
    [SerializeField] private Color loadPreSlotColor = new Color(0.52f, 0.56f, 0.62f, 0.12f);

    [Tooltip("Earned but unloaded Safe Capacity.")]
    [SerializeField] private Color loadEmptyColor = new Color(0.65f, 0.70f, 0.78f, 0.40f);

    [Tooltip("Loaded non-overload cargo.")]
    [SerializeField] private Color loadFilledColor = new Color(0.10f, 0.92f, 1f, 1f);

    #endregion

    #region Overload

    [Header("Load Overload Severity")]
    [Tooltip(
        "Overall overload amount maps through this Gradient. " +
        "Suggested: yellow-orange -> orange -> red -> deep red."
    )]
    [SerializeField] private Gradient loadOverloadSeverityGradient = CreateDefaultOverloadGradient();

    [Tooltip("Overload amount at which the gradient reaches its final/deepest color.")]
    [Min(1f)]
    [SerializeField] private float overloadAmountForMaximumSeverity = 30f;

    #endregion

    #region Load Ticks

    [Header("Load Milestone Ticks")]
    [SerializeField] private bool showLoadMilestoneTicks = true;

    [Tooltip("Milestone interval. 10 produces 10, 20, 30, 40, ...")]
    [Min(1)]
    [SerializeField] private int loadMilestoneInterval = 10;

    [Min(1f)]
    [SerializeField] private float loadMilestoneTickLengthPixels = 13f;

    [Min(0.5f)]
    [SerializeField] private float loadMilestoneTickThicknessPixels = 2f;

    [SerializeField] private Color loadMilestoneTickColor = new Color(0.82f, 0.86f, 0.92f, 0.65f);

    [Tooltip("Independent text color for the 10 / 20 / 30 / ... milestone labels.")]
    [SerializeField] private Color loadMilestoneTextColor = new Color(0.88f, 0.92f, 1f, 0.90f);

    [Min(1f)]
    [SerializeField] private float loadMilestoneFontSizePixels = 12f;

    [Min(0f)]
    [SerializeField] private float loadMilestoneLabelOffsetPixels = 6f;

    [Header("Current Load Tick")]
    [SerializeField] private bool showCurrentLoadTick = true;

    [Min(1f)]
    [SerializeField] private float currentLoadTickLengthPixels = 19f;

    [Min(0.5f)]
    [SerializeField] private float currentLoadTickThicknessPixels = 3f;

    [SerializeField] private Color currentLoadTickColor = Color.white;

    [Tooltip("Independent text color for the moving Current Load number.")]
    [SerializeField] private Color currentLoadTextColor = new Color(1f, 0.95f, 0.72f, 1f);

    [Min(1f)]
    [SerializeField] private float currentLoadFontSizePixels = 15f;

    [Min(0f)]
    [SerializeField] private float currentLoadLabelOffsetPixels = 8f;


    #endregion

    #region Hype Meter

    [Header("HYPE Meter - Right")]
    [Min(1f)]
    [SerializeField] private float hypeRadiusPixels = 82f;

    [Min(1f)]
    [SerializeField] private float hypeTrackThicknessPixels = 24f;

    [Min(1f)]
    [SerializeField] private float hypeFillThicknessPixels = 14f;

    [Range(10f, 300f)]
    [SerializeField] private float hypeSpanDegrees = 140f;

    [SerializeField] private float hypeCenterAngleDegrees = 0f;

    [Header("Hype Colors")]
    [SerializeField] private Color hypeTrackColor = new Color(0.20f, 0.24f, 0.30f, 0.60f);
    [SerializeField] private Color hypeFillColor = new Color(0.20f, 1f, 0.60f, 1f);

    [Header("Hype Drift SUCCESS Preview")]
    [Tooltip("Thickness of the potential success reward segment.")]
    [Min(1f)]
    [SerializeField] private float hypeRewardPreviewThicknessPixels = 10f;

    [Tooltip(
        "Optional radius offset relative to the main Hype centerline. " +
        "0 keeps it directly on the same bar."
    )]
    [SerializeField] private float hypeRewardPreviewRadiusOffsetPixels = 0f;

    [SerializeField] private Color hypeRewardPreviewColor = new Color(0.42f, 1f, 0.78f, 1f);

    [Header("Hype Drift FAILURE Preview")]
    [Tooltip("Thickness of the potential failure penalty segment.")]
    [Min(1f)]
    [SerializeField] private float hypePenaltyPreviewThicknessPixels = 10f;

    [Tooltip(
        "Optional radius offset relative to the main Hype centerline. " +
        "0 keeps it directly on the same bar."
    )]
    [SerializeField] private float hypePenaltyPreviewRadiusOffsetPixels = 0f;

    [SerializeField] private Color hypePenaltyPreviewColor = new Color(1f, 0.34f, 0.08f, 1f);

    #endregion

    #region Public API

    public float MasterScale => masterScale;
    public float HorizontalSeparationPixels => horizontalSeparationPixels * masterScale;
    public float VerticalOffsetPixels => verticalOffsetPixels * masterScale;

    public bool UseNearCameraRenderPlane => useNearCameraRenderPlane;
    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    public float LoadRadiusPixels => loadRadiusPixels * masterScale;
    public float LoadTrackThicknessPixels => loadTrackThicknessPixels * masterScale;
    public float LoadSpanDegrees => loadSpanDegrees;
    public float LoadCenterAngleDegrees => loadCenterAngleDegrees;
    public float LoadCapsuleLengthPixels => loadCapsuleLengthPixels * masterScale;

    public int MinimumVisualSlotCount => minimumVisualSlotCount;
    public int LoadWindowSize => loadWindowSize;

    public float CapsuleThicknessAt10Slots => capsuleThicknessAt10Slots * masterScale;
    public float CapsuleThicknessAt15Slots => capsuleThicknessAt15Slots * masterScale;
    public float CapsuleThicknessAt20Slots => capsuleThicknessAt20Slots * masterScale;

    public Color LoadTrackColor => loadTrackColor;
    public Color LoadPreSlotColor => loadPreSlotColor;
    public Color LoadEmptyColor => loadEmptyColor;
    public Color LoadFilledColor => loadFilledColor;

    public Gradient LoadOverloadSeverityGradient => loadOverloadSeverityGradient;
    public float OverloadAmountForMaximumSeverity => overloadAmountForMaximumSeverity;

    public bool ShowLoadMilestoneTicks => showLoadMilestoneTicks;
    public int LoadMilestoneInterval => loadMilestoneInterval;
    public float LoadMilestoneTickLengthPixels => loadMilestoneTickLengthPixels * masterScale;
    public float LoadMilestoneTickThicknessPixels => loadMilestoneTickThicknessPixels * masterScale;
    public Color LoadMilestoneTickColor => loadMilestoneTickColor;
    public Color LoadMilestoneTextColor => loadMilestoneTextColor;
    public float LoadMilestoneFontSizePixels => loadMilestoneFontSizePixels * masterScale;
    public float LoadMilestoneLabelOffsetPixels => loadMilestoneLabelOffsetPixels * masterScale;

    public bool ShowCurrentLoadTick => showCurrentLoadTick;
    public float CurrentLoadTickLengthPixels => currentLoadTickLengthPixels * masterScale;
    public float CurrentLoadTickThicknessPixels => currentLoadTickThicknessPixels * masterScale;
    public Color CurrentLoadTickColor => currentLoadTickColor;
    public Color CurrentLoadTextColor => currentLoadTextColor;
    public float CurrentLoadFontSizePixels => currentLoadFontSizePixels * masterScale;
    public float CurrentLoadLabelOffsetPixels => currentLoadLabelOffsetPixels * masterScale;

    public float HypeRadiusPixels => hypeRadiusPixels * masterScale;
    public float HypeTrackThicknessPixels => hypeTrackThicknessPixels * masterScale;
    public float HypeFillThicknessPixels => hypeFillThicknessPixels * masterScale;
    public float HypeSpanDegrees => hypeSpanDegrees;
    public float HypeCenterAngleDegrees => hypeCenterAngleDegrees;
    public Color HypeTrackColor => hypeTrackColor;
    public Color HypeFillColor => hypeFillColor;

    public float HypeRewardPreviewThicknessPixels => hypeRewardPreviewThicknessPixels * masterScale;
    public float HypeRewardPreviewRadiusOffsetPixels => hypeRewardPreviewRadiusOffsetPixels * masterScale;
    public Color HypeRewardPreviewColor => hypeRewardPreviewColor;

    public float HypePenaltyPreviewThicknessPixels => hypePenaltyPreviewThicknessPixels * masterScale;
    public float HypePenaltyPreviewRadiusOffsetPixels => hypePenaltyPreviewRadiusOffsetPixels * masterScale;
    public Color HypePenaltyPreviewColor => hypePenaltyPreviewColor;

    #endregion

    #region Load Helpers

    public float GetLoadCapsuleThicknessPixels(int visibleSlotCount)
    {
        int count = Mathf.Clamp(
            visibleSlotCount,
            minimumVisualSlotCount,
            loadWindowSize
        );

        if (count <= 10)
        {
            return CapsuleThicknessAt10Slots;
        }

        if (count <= 15)
        {
            float t = Mathf.InverseLerp(10f, 15f, count);

            return Mathf.Lerp(
                CapsuleThicknessAt10Slots,
                CapsuleThicknessAt15Slots,
                t
            );
        }

        float upperT = Mathf.InverseLerp(15f, 20f, count);

        return Mathf.Lerp(
            CapsuleThicknessAt15Slots,
            CapsuleThicknessAt20Slots,
            upperT
        );
    }

    public Color GetLoadOverloadColor(float overloadAmount)
    {
        if (loadOverloadSeverityGradient == null)
        {
            return new Color(1f, 0.18f, 0.04f, 1f);
        }

        float t = overloadAmountForMaximumSeverity > 0f
            ? Mathf.Clamp01(overloadAmount / overloadAmountForMaximumSeverity)
            : 1f;

        return loadOverloadSeverityGradient.Evaluate(t);
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        masterScale = Mathf.Max(0.01f, masterScale);
        horizontalSeparationPixels = Mathf.Max(0f, horizontalSeparationPixels);

        nearCameraRenderDistance = Mathf.Max(0.01f, nearCameraRenderDistance);
        nearClipSafetyPadding = Mathf.Max(0.001f, nearClipSafetyPadding);

        loadRadiusPixels = Mathf.Max(1f, loadRadiusPixels);
        loadTrackThicknessPixels = Mathf.Max(1f, loadTrackThicknessPixels);
        loadSpanDegrees = Mathf.Clamp(loadSpanDegrees, 10f, 300f);
        loadCapsuleLengthPixels = Mathf.Max(1f, loadCapsuleLengthPixels);

        minimumVisualSlotCount = Mathf.Max(1, minimumVisualSlotCount);
        loadWindowSize = Mathf.Max(minimumVisualSlotCount, loadWindowSize);

        capsuleThicknessAt10Slots = Mathf.Clamp(capsuleThicknessAt10Slots, 1f, loadTrackThicknessPixels);
        capsuleThicknessAt15Slots = Mathf.Clamp(capsuleThicknessAt15Slots, 1f, loadTrackThicknessPixels);
        capsuleThicknessAt20Slots = Mathf.Clamp(capsuleThicknessAt20Slots, 1f, loadTrackThicknessPixels);

        overloadAmountForMaximumSeverity = Mathf.Max(1f, overloadAmountForMaximumSeverity);

        loadMilestoneInterval = Mathf.Max(1, loadMilestoneInterval);
        loadMilestoneTickLengthPixels = Mathf.Max(1f, loadMilestoneTickLengthPixels);
        loadMilestoneTickThicknessPixels = Mathf.Max(0.5f, loadMilestoneTickThicknessPixels);
        loadMilestoneFontSizePixels = Mathf.Max(1f, loadMilestoneFontSizePixels);
        loadMilestoneLabelOffsetPixels = Mathf.Max(0f, loadMilestoneLabelOffsetPixels);

        currentLoadTickLengthPixels = Mathf.Max(1f, currentLoadTickLengthPixels);
        currentLoadTickThicknessPixels = Mathf.Max(0.5f, currentLoadTickThicknessPixels);
        currentLoadFontSizePixels = Mathf.Max(1f, currentLoadFontSizePixels);
        currentLoadLabelOffsetPixels = Mathf.Max(0f, currentLoadLabelOffsetPixels);

        hypeRadiusPixels = Mathf.Max(1f, hypeRadiusPixels);
        hypeTrackThicknessPixels = Mathf.Max(1f, hypeTrackThicknessPixels);
        hypeFillThicknessPixels = Mathf.Clamp(
            hypeFillThicknessPixels,
            1f,
            hypeTrackThicknessPixels
        );

        hypeRewardPreviewThicknessPixels = Mathf.Max(1f, hypeRewardPreviewThicknessPixels);
        hypePenaltyPreviewThicknessPixels = Mathf.Max(1f, hypePenaltyPreviewThicknessPixels);

        hypeSpanDegrees = Mathf.Clamp(hypeSpanDegrees, 10f, 300f);
    }

    #endregion

    #region Defaults

    private static Gradient CreateDefaultOverloadGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.68f, 0.05f), 0f),
                new GradientColorKey(new Color(1f, 0.36f, 0.02f), 0.33f),
                new GradientColorKey(new Color(1f, 0.10f, 0.02f), 0.67f),
                new GradientColorKey(new Color(0.55f, 0.01f, 0.01f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return gradient;
    }

    #endregion
}
