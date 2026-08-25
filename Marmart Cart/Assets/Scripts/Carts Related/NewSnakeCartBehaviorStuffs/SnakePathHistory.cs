using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records the leading cart's actual travelled path in world space.
///
/// Unlike the old MarkerManager:
/// - This history belongs to the whole snake, not to every cart.
/// - Progress is measured by travelled DISTANCE, not elapsed FixedUpdates.
/// - No meaningful leader movement = no path progress.
/// - Samples are approximately evenly spaced in world distance.
/// - The history is queried by cumulative distance and is NOT consumed.
///
/// Step A0:
/// This component only records/debugs the path.
/// It does not move follower carts yet.
/// </summary>
public class SnakePathHistory : MonoBehaviour
{
    [System.Serializable]
    public struct PathPose
    {
        public Vector3 position;
        public Quaternion rotation;

        /// <summary>
        /// Cumulative path distance.
        /// The leader begins at distance 0.
        /// Seeded history behind the leader uses negative values.
        /// </summary>
        public float distance;

        public PathPose(Vector3 position, Quaternion rotation, float distance)
        {
            this.position = position;
            this.rotation = rotation;
            this.distance = distance;
        }
    }

    #region Settings

    [Header("Path Recording")]

    [Tooltip(
        "World-space distance between stored path samples. " +
        "Eventually this should be much smaller than the distance between two carts.")]
    [Min(0.001f)]
    [SerializeField] private float sampleSpacing = 0.1f;

    [Tooltip(
        "Leader must move at least this far from the last accepted movement point " +
        "before that movement counts as path progress. " +
        "This prevents tiny Rigidbody jitter while stalled from growing the path.")]
    [Min(0f)]
    [SerializeField] private float movementAcceptanceDistance = 0.01f;

    [Tooltip(
        "Creates a straight history behind the leader when initialized. " +
        "Later this allows carts to already exist behind the leader before " +
        "the player has driven forward.")]
    [Min(0f)]
    [SerializeField] private float initialBackfillDistance = 20f;

    [Tooltip(
        "Maximum path distance we keep behind the current leader position. " +
        "This must eventually be longer than the maximum possible cart train.")]
    [Min(1f)]
    [SerializeField] private float maxHistoryDistance = 80f;

    [Tooltip(
        "Old samples are removed in batches instead of constantly deleting " +
        "one element from the beginning of the List.")]
    [Min(1)]
    [SerializeField] private int pruneBatchSize = 32;

    [Header("Movement Filtering")]

    [Tooltip(
    "Minimum planar Rigidbody speed required before displacement " +
    "is allowed to advance the snake path. " +
    "Filters collision-solver settling and tiny wall pivots.")]
    [Min(0f)]
    [SerializeField] private float minPathMotionSpeed = 0.5f;

    [Tooltip(
        "If true, vertical suspension/bouncing does not count as snake path travel.")]
    [SerializeField] private bool ignoreVerticalMotion = true;
    #endregion

    #region Debug Settings

    [Header("Debug Drawing")]

    [SerializeField] private bool drawPath = true;
    [SerializeField] private bool drawStoredSamples = true;

    [Min(1)]
    [SerializeField] private int drawEveryNthSample = 5;

    [Min(0.001f)]
    [SerializeField] private float sampleGizmoRadius = 0.04f;

    [Header("Runtime Debug - Read Only")]

    [SerializeField] private bool isInitialized;
    [SerializeField] private bool acceptedMovementThisTick;
    [SerializeField] private float currentDistance;
    [SerializeField] private float lastAcceptedSegmentDistance;
    [SerializeField] private int currentSampleCount;

    #endregion

    #region Runtime Data

    private Rigidbody leaderBody;

    private readonly List<PathPose> samples =
        new List<PathPose>(512);

    /// <summary>
    /// Last leader pose whose movement was accepted as meaningful.
    /// Small Rigidbody jitter around this point is ignored.
    /// </summary>
    private Vector3 acceptedPosition;
    private Quaternion acceptedRotation;

    /// <summary>
    /// Current accepted endpoint of the path.
    ///
    /// This may exist between two regularly-spaced stored samples.
    /// </summary>
    private Vector3 livePosition;
    private Quaternion liveRotation;

    /// <summary>
    /// Distance already travelled after the newest stored sample,
    /// but not yet enough to create another full sample.
    /// </summary>
    private float distanceSinceLastSample;

    private Vector3 lastObservedPosition;
    #endregion

    #region Public API

    public bool IsInitialized => isInitialized;

    public float CurrentDistance => currentDistance;

    public int SampleCount => samples.Count;

    public float SampleSpacing => sampleSpacing;

    public IReadOnlyList<PathPose> Samples => samples;

    /// <summary>
    /// Begin recording the supplied Rigidbody.
    /// </summary>
    public void Initialize(Rigidbody body)
    {
        if (body == null)
        {
            Debug.LogError(
                "[SnakePathHistory] Cannot initialize with a null Rigidbody.",
                this
            );

            return;
        }

        leaderBody = body;

        ResetHistoryToLeader();

        Debug.Log(
            $"[SnakePathHistory] Initialized using Rigidbody '{body.name}'.",
            this
        );
    }

    /// <summary>
    /// Clears the current path and rebuilds the initial path behind the leader.
    ///
    /// Later we will also use this for teleports / respawns / resets.
    /// </summary>
    public void ResetHistoryToLeader()
    {
        if (leaderBody == null)
            return;

        samples.Clear();

        Vector3 leaderPosition = leaderBody.position;
        Quaternion leaderRotation = leaderBody.rotation;

        Vector3 backwardsPathDirection =
            Vector3.ProjectOnPlane(
                leaderRotation * Vector3.forward,
                Vector3.up
            );

        if (backwardsPathDirection.sqrMagnitude < 0.0001f)
            backwardsPathDirection = Vector3.forward;

        backwardsPathDirection.Normalize();

        // --------------------------------------------------
        // Seed a straight path behind the starting cart.
        //
        // Example:
        //
        // -3m ---- -2m ---- -1m ---- 0m Leader
        //
        // This means later followers can immediately ask for
        // positions behind the leader.
        // --------------------------------------------------

        if (initialBackfillDistance > 0f)
        {
            int backfillSampleCount =
                Mathf.CeilToInt(
                    initialBackfillDistance / sampleSpacing
                );

            for (int i = backfillSampleCount; i >= 1; i--)
            {
                float distance = -i * sampleSpacing;

                Vector3 position =
                    leaderPosition +
                    backwardsPathDirection * distance;

                samples.Add(
                    new PathPose(
                        position,
                        leaderRotation,
                        distance
                    )
                );
            }
        }

        // Leader starts at path distance zero.
        samples.Add(
            new PathPose(
                leaderPosition,
                leaderRotation,
                0f
            )
        );

        acceptedPosition = leaderPosition;
        acceptedRotation = leaderRotation;
        lastObservedPosition = leaderPosition;

        livePosition = leaderPosition;
        liveRotation = leaderRotation;

        distanceSinceLastSample = 0f;
        currentDistance = 0f;

        acceptedMovementThisTick = false;
        lastAcceptedSegmentDistance = 0f;

        currentSampleCount = samples.Count;

        isInitialized = true;
    }

    /// <summary>
    /// Returns the path pose at a specific cumulative distance.
    ///
    /// This is not used by carts yet in A0.
    /// A1 will use this method for Cart 1.
    /// </summary>
    public bool TryGetPoseAtDistance(
        float targetDistance,
        out PathPose pose)
    {
        pose = default;

        if (!isInitialized || samples.Count == 0)
            return false;

        float oldestDistance = samples[0].distance;

        targetDistance =
            Mathf.Clamp(
                targetDistance,
                oldestDistance,
                currentDistance
            );

        PathPose newestStored =
            samples[samples.Count - 1];

        // --------------------------------------------------
        // Target lies between the newest stored sample and
        // the current accepted leader endpoint.
        // --------------------------------------------------

        if (targetDistance >= newestStored.distance)
        {
            float liveSegmentLength =
                currentDistance - newestStored.distance;

            if (liveSegmentLength <= 0.00001f)
            {
                pose = new PathPose(
                    newestStored.position,
                    newestStored.rotation,
                    targetDistance
                );

                return true;
            }

            float t =
                Mathf.Clamp01(
                    (targetDistance - newestStored.distance) /
                    liveSegmentLength
                );

            pose = new PathPose(
                Vector3.Lerp(
                    newestStored.position,
                    livePosition,
                    t
                ),
                Quaternion.Slerp(
                    newestStored.rotation,
                    liveRotation,
                    t
                ),
                targetDistance
            );

            return true;
        }

        // --------------------------------------------------
        // Binary-search the regularly stored samples.
        // --------------------------------------------------

        int low = 0;
        int high = samples.Count - 1;

        while (low < high)
        {
            int mid = (low + high) / 2;

            if (samples[mid].distance < targetDistance)
                low = mid + 1;
            else
                high = mid;
        }

        int upperIndex = low;

        if (upperIndex == 0)
        {
            PathPose first = samples[0];

            pose = new PathPose(
                first.position,
                first.rotation,
                targetDistance
            );

            return true;
        }

        int lowerIndex = upperIndex - 1;

        PathPose lower = samples[lowerIndex];
        PathPose upper = samples[upperIndex];

        float distanceRange =
            upper.distance - lower.distance;

        float interpolation =
            distanceRange > 0.00001f
                ? (targetDistance - lower.distance) /
                  distanceRange
                : 0f;

        pose = new PathPose(
            Vector3.Lerp(
                lower.position,
                upper.position,
                interpolation
            ),
            Quaternion.Slerp(
                lower.rotation,
                upper.rotation,
                interpolation
            ),
            targetDistance
        );

        return true;
    }

    #endregion

    #region Recording

    private void FixedUpdate()
    {
        if (!isInitialized || leaderBody == null)
            return;

        RecordLeaderPose(
            leaderBody.position,
            leaderBody.rotation
        );
    }

    private void RecordLeaderPose(
    Vector3 currentPosition,
    Quaternion currentRotation)
    {
        acceptedMovementThisTick = false;
        lastAcceptedSegmentDistance = 0f;

        // --------------------------------------------------
        // 1. Measure what physically happened this FixedUpdate.
        // --------------------------------------------------

        Vector3 frameDelta =
            currentPosition - lastObservedPosition;

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

        // --------------------------------------------------
        // 2. Read the Rigidbody's actual translational speed.
        //
        // This acts as our "is this meaningful locomotion?"
        // filter.
        // --------------------------------------------------

        Vector3 velocity =
            leaderBody.linearVelocity;

        if (ignoreVerticalMotion)
        {
            velocity =
                Vector3.ProjectOnPlane(
                    velocity,
                    Vector3.up
                );
        }

        float planarSpeed =
            velocity.magnitude;

        // IMPORTANT:
        // Always update this even when we reject the movement.
        //
        // Otherwise tiny rejected solver movements accumulate
        // and eventually become one accepted large movement.
        lastObservedPosition =
            currentPosition;

        // --------------------------------------------------
        // 3. Reject passive physics settling.
        // --------------------------------------------------

        bool hasMeaningfulSpeed =
            planarSpeed >= minPathMotionSpeed;

        bool hasMeaningfulDisplacement =
            frameDisplacement >= movementAcceptanceDistance;

        if (!hasMeaningfulSpeed ||
            !hasMeaningfulDisplacement)
        {
            // We intentionally discard this motion from path progress.
            //
            // Re-anchor where future VALID motion starts so that the
            // discarded settling displacement does not get added later.
            acceptedPosition =
                currentPosition;

            acceptedRotation =
                currentRotation;

            return;
        }

        // --------------------------------------------------
        // 4. Valid locomotion.
        //
        // Distance itself still comes from actual world displacement,
        // not velocity * deltaTime.
        // --------------------------------------------------

        Vector3 acceptedDelta =
            currentPosition - acceptedPosition;

        if (ignoreVerticalMotion)
        {
            acceptedDelta =
                Vector3.ProjectOnPlane(
                    acceptedDelta,
                    Vector3.up
                );
        }

        float acceptedSegmentLength =
            acceptedDelta.magnitude;

        if (acceptedSegmentLength <= 0.00001f)
            return;

        acceptedMovementThisTick = true;

        lastAcceptedSegmentDistance =
            acceptedSegmentLength;

        AppendAcceptedSegment(
            acceptedPosition,
            acceptedRotation,
            currentPosition,
            currentRotation,
            acceptedSegmentLength
        );

        acceptedPosition =
            currentPosition;

        acceptedRotation =
            currentRotation;

        livePosition =
            currentPosition;

        liveRotation =
            currentRotation;

        currentDistance =
            samples[samples.Count - 1].distance +
            distanceSinceLastSample;

        currentSampleCount =
            samples.Count;

        PruneHistory();
    }

    #endregion

    #region History Cleanup

    private void PruneHistory()
    {
        if (samples.Count <= 2)
            return;

        float keepFromDistance =
            currentDistance -
            maxHistoryDistance;

        int removeCount = 0;

        // Always preserve at least two stored points.
        while (
            removeCount < samples.Count - 2 &&
            samples[removeCount + 1].distance <
            keepFromDistance)
        {
            removeCount++;
        }

        // Avoid constantly shifting the List.
        if (removeCount >= pruneBatchSize)
        {
            samples.RemoveRange(
                0,
                removeCount
            );
        }

        currentSampleCount = samples.Count;
    }
    /// <summary>
    /// Converts one accepted movement segment into evenly spaced
    /// distance-based path samples.
    ///
    /// Example:
    /// sampleSpacing = 0.1m
    /// leader moves 0.35m this FixedUpdate
    ///
    /// → generates samples at 0.1m, 0.2m, 0.3m
    /// → remembers the remaining 0.05m for next update
    /// </summary>
    private void AppendAcceptedSegment(
        Vector3 segmentStartPosition,
        Quaternion segmentStartRotation,
        Vector3 segmentEndPosition,
        Quaternion segmentEndRotation,
        float segmentLength)
    {
        float remainingLength = segmentLength;

        Vector3 remainingStartPosition =
            segmentStartPosition;

        Quaternion remainingStartRotation =
            segmentStartRotation;

        while (
            distanceSinceLastSample +
            remainingLength >=
            sampleSpacing)
        {
            // How much farther do we need to travel
            // before reaching the next evenly spaced sample?
            float distanceNeeded =
                sampleSpacing -
                distanceSinceLastSample;

            float t =
                remainingLength > 0.00001f
                    ? distanceNeeded / remainingLength
                    : 1f;

            t = Mathf.Clamp01(t);

            // Find the exact position along this movement segment
            // where the next sample belongs.
            Vector3 newSamplePosition =
                Vector3.Lerp(
                    remainingStartPosition,
                    segmentEndPosition,
                    t
                );

            Quaternion newSampleRotation =
                Quaternion.Slerp(
                    remainingStartRotation,
                    segmentEndRotation,
                    t
                );

            PathPose previousSample =
                samples[samples.Count - 1];

            float newSampleDistance =
                previousSample.distance +
                sampleSpacing;

            samples.Add(
                new PathPose(
                    newSamplePosition,
                    newSampleRotation,
                    newSampleDistance
                )
            );

            // Continue processing the unused part of this same
            // FixedUpdate movement.
            remainingStartPosition =
                newSamplePosition;

            remainingStartRotation =
                newSampleRotation;

            remainingLength -=
                distanceNeeded;

            distanceSinceLastSample = 0f;
        }

        // Not enough movement left to create another complete
        // sample, so carry it into the next FixedUpdate.
        distanceSinceLastSample +=
            Mathf.Max(0f, remainingLength);
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

        Gizmos.color = Color.cyan;

        for (int i = 1; i < samples.Count; i++)
        {
            Gizmos.DrawLine(
                samples[i - 1].position,
                samples[i].position
            );
        }

        // Draw the small live section between the last
        // stored sample and the accepted leader endpoint.
        PathPose newest =
            samples[samples.Count - 1];

        Gizmos.DrawLine(
            newest.position,
            livePosition
        );

        if (drawStoredSamples)
        {
            Gizmos.color = Color.yellow;

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

        Gizmos.color = Color.green;

        Gizmos.DrawSphere(
            livePosition,
            sampleGizmoRadius * 1.5f
        );
    }

    #endregion

    private void OnValidate()
    {
        sampleSpacing =
            Mathf.Max(
                0.001f,
                sampleSpacing
            );

        movementAcceptanceDistance =
            Mathf.Max(
                0f,
                movementAcceptanceDistance
            );

        initialBackfillDistance =
            Mathf.Max(
                0f,
                initialBackfillDistance
            );

        maxHistoryDistance =
            Mathf.Max(
                maxHistoryDistance,
                initialBackfillDistance +
                sampleSpacing * 2f
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
}