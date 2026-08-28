using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared spatial path for the snake.
///
/// Core idea:
///
/// PATH:
///     Describes where the leading cart has meaningfully travelled.
///
/// HEAD PROGRESS:
///     Describes where the active head of the snake currently is on that path.
///
/// Followers DO NOT consume samples.
/// They query:
///
///     HeadProgress - desiredDistanceBehind
///
/// Rotation is derived from the path tangent rather than copied
/// from the leading cart's rotation.
///
/// Forward locomotion recording only.
/// There is no MoveBackward/reverse control in this version.
/// </summary>
public class SnakePathHistory : MonoBehaviour
{
    #region Path Point

    [System.Serializable]
    public struct PathPoint
    {
        public Vector3 position;
        public float distance;

        public PathPoint(
            Vector3 position,
            float distance)
        {
            this.position = position;
            this.distance = distance;
        }
    }

    #endregion

    #region Recording Settings
    private Transform pathSource;
    [Header("Recording")]

    [Tooltip(
        "Distance between stored path samples in world units.")]
    [Min(0.001f)]
    [SerializeField]
    private float sampleSpacing = 0.1f;

    [Tooltip(
        "Minimum planar Rigidbody speed required before movement " +
        "is considered real snake-path progress.")]
    [Min(0f)]
    [SerializeField]
    private float minPathMotionSpeed = 0.5f;

    [Tooltip(
        "Minimum world displacement in one physics tick before " +
        "movement can advance the path.")]
    [Min(0f)]
    [SerializeField]
    private float movementAcceptanceDistance = 0.01f;

    [Tooltip(
        "Ignore suspension / bouncing when measuring path distance.")]
    [SerializeField]
    private bool ignoreVerticalMotion = true;
    #endregion

    #region History Settings

    [Header("History")]

    [Tooltip(
        "Straight path created behind the leader at startup.")]
    [Min(0f)]
    [SerializeField]
    private float initialBackfillDistance = 20f;

    [Tooltip(
        "Maximum amount of historical path retained.")]
    [Min(1f)]
    [SerializeField]
    private float maxHistoryDistance = 80f;

    [Tooltip(
        "Old samples are removed in batches.")]
    [Min(1)]
    [SerializeField]
    private int pruneBatchSize = 32;

    #endregion

    #region Rotation Settings

    [Header("Follower Rotation")]

    [Tooltip(
        "Distance sampled before/after a target point when calculating " +
        "the path tangent used for follower rotation.")]
    [Min(0.01f)]
    [SerializeField]
    private float tangentProbeDistance = 0.35f;

    #endregion

    #region Debug

    [Header("Debug")]

    [SerializeField]
    private bool drawPath = true;

    [SerializeField]
    private bool drawStoredSamples = true;

    [Min(1)]
    [SerializeField]
    private int drawEveryNthSample = 5;

    [Min(0.001f)]
    [SerializeField]
    private float sampleGizmoRadius = 0.04f;

    [Header("Runtime Debug - Read Only")]

    [SerializeField]
    private bool isInitialized;

    [SerializeField]
    private bool acceptedMovementThisTick;

    [SerializeField]
    private float currentPlanarSpeed;

    [SerializeField]
    private float headProgress;

    [SerializeField]
    private float recordedEndProgress;

    [SerializeField]
    private float distanceSinceLastSample;

    [SerializeField]
    private int currentSampleCount;

    #endregion

    #region Runtime

    private Rigidbody leaderBody;

    private readonly List<PathPoint> samples =
        new List<PathPoint>(512);

    /// <summary>
    /// Actual Rigidbody position observed last physics tick.
    /// Used only for detecting physical movement.
    /// </summary>
    private Vector3 lastObservedPosition;

    /// <summary>
    /// Spatial endpoint of the currently recorded path.
    ///
    /// IMPORTANT:
    /// This endpoint is allowed to re-anchor during tiny rejected
    /// stall/solver movement WITHOUT increasing progress.
    /// </summary>
    private Vector3 livePathEndPosition;

    #endregion

    #region Public API

    public bool IsInitialized => isInitialized;

    /// <summary>
    /// Current active position of the snake head along the path.
    ///
    /// Followers should use this value.
    /// </summary>
    public float HeadProgress => headProgress;

    /// <summary>
    /// Furthest currently recorded path distance.
    ///
    /// </summary>
    public float RecordedEndProgress => recordedEndProgress;

    public int SampleCount => samples.Count;

    public IReadOnlyList<PathPoint> Samples => samples;
    #endregion

    #region Initialize
    [SerializeField] private float pathPlaneY;
    public void Initialize(Transform newPathSource)
    {
        if (newPathSource == null)
        {
            Debug.LogError("[SnakePathHistory] Cannot initialize: Path Source is null.", this);
            return;
        }

        pathSource = newPathSource;

        ResetHistoryToSource();

        Debug.Log($"[SnakePathHistory] Initialized from {pathSource.name} on Y plane {pathPlaneY:F2}.", this);
    }

    public void ResetHistoryToSource()
    {
        if (pathSource == null)
            return;

        samples.Clear();

        Vector3 sourcePosition = FlattenToPathPlane(pathSource.position);

        Vector3 forward =
            Vector3.ProjectOnPlane(
                pathSource.forward,
                Vector3.up
            );

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        if (initialBackfillDistance > 0f)
        {
            int backfillCount =
                Mathf.CeilToInt(
                    initialBackfillDistance /
                    sampleSpacing
                );

            for (int i = backfillCount; i >= 1; i--)
            {
                float distance =
                    -i * sampleSpacing;

                Vector3 position =
                    sourcePosition +
                    forward * distance;

                samples.Add(
                    new PathPoint(
                        position,
                        distance
                    )
                );
            }
        }

        samples.Add(
            new PathPoint(
                sourcePosition,
                0f
            )
        );

        lastObservedPosition =
            sourcePosition;

        livePathEndPosition =
            sourcePosition;

        distanceSinceLastSample = 0f;

        recordedEndProgress = 0f;
        headProgress = 0f;

        acceptedMovementThisTick = false;
        currentSampleCount = samples.Count;

        isInitialized = true;
    }
    private Vector3 FlattenToPathPlane(Vector3 position)
    {
        position.y = pathPlaneY;
        return position;
    }
    #endregion

    #region Manual Physics Tick

    /// <summary>
    /// SnakeCartManager explicitly calls this before updating followers.
    ///
    /// We intentionally do NOT use FixedUpdate here because otherwise
    /// component execution order could make followers read either this
    /// frame's path or last frame's path unpredictably.
    /// </summary>
    public void TickHistory()
    {
        if (!isInitialized || pathSource == null) return;

        RecordPathSourceMovement(FlattenToPathPlane(pathSource.position));
    }

    #endregion

    #region Recording
    private void RecordPathSourceMovement(
    Vector3 currentSourcePosition)
    {
        acceptedMovementThisTick = false;

        Vector3 frameDelta =
            currentSourcePosition -
            lastObservedPosition;

        if (ignoreVerticalMotion)
        {
            frameDelta =
                Vector3.ProjectOnPlane(
                    frameDelta,
                    Vector3.up
                );
        }

        float frameDisplacement =
            frameDelta.magnitude;

        // Debug only if you want it.
        currentPlanarSpeed =
            frameDisplacement /
            Mathf.Max(
                Time.fixedDeltaTime,
                0.00001f
            );

        // Always follow the physical probe.
        lastObservedPosition =
            currentSourcePosition;

        // --------------------------------------------------
        // Only remove extremely tiny physics jitter.
        //
        // There is NO leader-speed test anymore.
        // Legitimate hinge swing is allowed to make path.
        // --------------------------------------------------

        if (frameDisplacement <
            movementAcceptanceDistance)
        {
            return;
        }

        Vector3 segmentDelta =
            currentSourcePosition -
            livePathEndPosition;

        if (ignoreVerticalMotion)
        {
            segmentDelta =
                Vector3.ProjectOnPlane(
                    segmentDelta,
                    Vector3.up
                );
        }

        float segmentLength =
            segmentDelta.magnitude;

        if (segmentLength <= 0.00001f)
            return;

        acceptedMovementThisTick = true;
        AppendSegment(
                    livePathEndPosition,
                    currentSourcePosition,
                    segmentLength
                );

        livePathEndPosition =
            currentSourcePosition;

        recordedEndProgress =
            samples[samples.Count - 1].distance +
            distanceSinceLastSample;

        headProgress =
            recordedEndProgress;

        currentSampleCount =
            samples.Count;

        PruneHistory();
    }

    private void AppendSegment(
        Vector3 segmentStartPosition,
        Vector3 segmentEndPosition,
        float segmentLength)
    {
        float remainingLength =
            segmentLength;

        Vector3 remainingStart =
            segmentStartPosition;

        while (
            distanceSinceLastSample +
            remainingLength >=
            sampleSpacing)
        {
            float distanceNeeded =
                sampleSpacing -
                distanceSinceLastSample;

            float t =
                remainingLength >
                0.00001f
                    ? distanceNeeded /
                      remainingLength
                    : 1f;

            t = Mathf.Clamp01(t);

            Vector3 newPosition =
                Vector3.Lerp(
                    remainingStart,
                    segmentEndPosition,
                    t
                );

            PathPoint previous =
                samples[samples.Count - 1];

            float newDistance =
                previous.distance +
                sampleSpacing;

            samples.Add(
                new PathPoint(
                    newPosition,
                    newDistance
                )
            );

            remainingStart =
                newPosition;

            remainingLength -=
                distanceNeeded;

            distanceSinceLastSample = 0f;
        }

        distanceSinceLastSample +=
            Mathf.Max(
                0f,
                remainingLength
            );
    }

    #endregion

    #region Path Query

    /// <summary>
    /// Gets a follower target from a spatial path distance.
    ///
    /// Position:
    ///     interpolated from the path
    ///
    /// Rotation:
    ///     derived from path tangent
    ///
    /// Leader rotation is NOT replayed.
    /// </summary>
    public bool TryGetPoseAtProgress(
        float targetProgress,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!TryGetPositionAtProgress(
            targetProgress,
            out position))
        {
            return false;
        }

        float oldest =
            samples[0].distance;

        float newest =
            recordedEndProgress;

        targetProgress =
            Mathf.Clamp(
                targetProgress,
                oldest,
                newest
            );

        float beforeProgress =
            Mathf.Max(
                oldest,
                targetProgress -
                tangentProbeDistance
            );

        float afterProgress =
            Mathf.Min(
                newest,
                targetProgress +
                tangentProbeDistance
            );

        if (!TryGetPositionAtProgress(
                beforeProgress,
                out Vector3 beforePosition))
        {
            return false;
        }

        if (!TryGetPositionAtProgress(
                afterProgress,
                out Vector3 afterPosition))
        {
            return false;
        }

        Vector3 tangent =
            afterPosition -
            beforePosition;

        tangent =
            Vector3.ProjectOnPlane(
                tangent,
                Vector3.up
            );

        // Fallback if we're sitting on a nearly zero-length region.
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent =
                GetFallbackTangent();
        }

        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.forward;

        rotation =
            Quaternion.LookRotation(
                tangent.normalized,
                Vector3.up
            );

        return true;
    }

    public bool TryGetPositionAtProgress(
        float targetProgress,
        out Vector3 position)
    {
        position = Vector3.zero;

        if (!isInitialized ||
            samples.Count == 0)
        {
            return false;
        }

        float oldest =
            samples[0].distance;

        targetProgress =
            Mathf.Clamp(
                targetProgress,
                oldest,
                recordedEndProgress
            );

        PathPoint newestStored =
            samples[samples.Count - 1];

        // ---------------------------------------------
        // Target lies inside the small unsampled
        // "live end" section.
        // ---------------------------------------------

        if (targetProgress >=
            newestStored.distance)
        {
            float liveDistance =
                recordedEndProgress -
                newestStored.distance;

            if (liveDistance <= 0.00001f)
            {
                position =
                    newestStored.position;

                return true;
            }

            float t =
                Mathf.Clamp01(
                    (targetProgress -
                     newestStored.distance) /
                    liveDistance
                );

            position =
                Vector3.Lerp(
                    newestStored.position,
                    livePathEndPosition,
                    t
                );

            return true;
        }

        // ---------------------------------------------
        // Binary search spatial samples.
        // ---------------------------------------------

        int low = 0;
        int high =
            samples.Count - 1;

        while (low < high)
        {
            int mid =
                (low + high) / 2;

            if (samples[mid].distance <
                targetProgress)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        int upperIndex = low;

        if (upperIndex == 0)
        {
            position =
                samples[0].position;

            return true;
        }

        int lowerIndex =
            upperIndex - 1;

        PathPoint lower =
            samples[lowerIndex];

        PathPoint upper =
            samples[upperIndex];

        float distanceRange =
            upper.distance -
            lower.distance;

        float t2 =
            distanceRange >
            0.00001f
                ? (targetProgress -
                   lower.distance) /
                  distanceRange
                : 0f;

        position =
            Vector3.Lerp(
                lower.position,
                upper.position,
                t2
            );

        return true;
    }

    private Vector3 GetFallbackTangent()
    {
        if (samples.Count >= 2)
        {
            Vector3 tangent =
                samples[samples.Count - 1].position -
                samples[samples.Count - 2].position;

            tangent =
                Vector3.ProjectOnPlane(
                    tangent,
                    Vector3.up
                );

            if (tangent.sqrMagnitude >
                0.0001f)
            {
                return tangent.normalized;
            }
        }

        if (leaderBody != null)
        {
            Vector3 forward =
                leaderBody.rotation *
                Vector3.forward;

            forward =
                Vector3.ProjectOnPlane(
                    forward,
                    Vector3.up
                );

            return forward.normalized;
        }

        return Vector3.forward;
    }

    #endregion
    #region Pruning

    private void PruneHistory()
    {
        if (samples.Count <= 2)
            return;

        float keepFrom =
            headProgress -
            maxHistoryDistance;

        int removeCount = 0;

        while (
            removeCount <
            samples.Count - 2 &&
            samples[removeCount + 1].distance <
            keepFrom)
        {
            removeCount++;
        }

        if (removeCount >=
            pruneBatchSize)
        {
            samples.RemoveRange(
                0,
                removeCount
            );
        }

        currentSampleCount =
            samples.Count;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!drawPath ||
            !Application.isPlaying ||
            !isInitialized ||
            samples.Count == 0)
        {
            return;
        }

        Gizmos.color =
            Color.cyan;

        for (
            int i = 1;
            i < samples.Count;
            i++)
        {
            Gizmos.DrawLine(
                samples[i - 1].position,
                samples[i].position
            );
        }

        Gizmos.DrawLine(
            samples[samples.Count - 1].position,
            livePathEndPosition
        );

        if (drawStoredSamples)
        {
            Gizmos.color =
                Color.yellow;

            int stride =
                Mathf.Max(
                    1,
                    drawEveryNthSample
                );

            for (
                int i = 0;
                i < samples.Count;
                i += stride)
            {
                Gizmos.DrawSphere(
                    samples[i].position,
                    sampleGizmoRadius
                );
            }
        }

        // Active snake head cursor.
        if (TryGetPositionAtProgress(
                headProgress,
                out Vector3 activeHeadPosition))
        {
            Gizmos.color =
                Color.green;

            Gizmos.DrawSphere(
                activeHeadPosition,
                sampleGizmoRadius * 2f
            );
        }
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        sampleSpacing =
            Mathf.Max(
                0.001f,
                sampleSpacing
            );

        minPathMotionSpeed =
            Mathf.Max(
                0f,
                minPathMotionSpeed
            );

        movementAcceptanceDistance =
            Mathf.Max(
                0f,
                movementAcceptanceDistance
            );

        tangentProbeDistance =
            Mathf.Max(
                0.01f,
                tangentProbeDistance
            );

        initialBackfillDistance =
            Mathf.Max(
                0f,
                initialBackfillDistance
            );

        maxHistoryDistance =
            Mathf.Max(
                initialBackfillDistance +
                sampleSpacing * 2f,
                maxHistoryDistance
            );

        pruneBatchSize =
            Mathf.Max(
                1,
                pruneBatchSize
            );

        drawEveryNthSample =
            Mathf.Max(
                1,
                drawEveryNthSample
            );
    }

    #endregion
}