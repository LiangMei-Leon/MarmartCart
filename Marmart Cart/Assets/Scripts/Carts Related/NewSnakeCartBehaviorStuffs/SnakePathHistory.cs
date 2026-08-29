using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared spatial path for normal forward snake movement.
///
/// The physical trailing probe writes path samples. Followers never consume
/// samples; they query a logical progress value behind HeadProgress.
///
/// After MoveBackward, the current chain geometry can be reseeded as a fresh
/// normal path so recovery does not snap any follower.
/// </summary>
public class SnakePathHistory : MonoBehaviour
{
    #region Data Types

    [System.Serializable]
    public struct PathPoint
    {
        public Vector3 position;
        public float distance;

        public PathPoint(Vector3 position, float distance)
        {
            this.position = position;
            this.distance = distance;
        }
    }

    private struct SeedAnchor
    {
        public Vector3 position;
        public float distance;

        public SeedAnchor(Vector3 position, float distance)
        {
            this.position = position;
            this.distance = distance;
        }
    }

    #endregion

    #region Recording Settings

    [Header("Recording")]
    [Min(0.001f)]
    [SerializeField] private float sampleSpacing = 0.1f;

    [Tooltip("Minimum probe displacement accepted as real path movement.")]
    [Min(0f)]
    [SerializeField] private float movementAcceptanceDistance = 0.01f;

    [Tooltip("Ignore suspension/bounce when measuring path distance.")]
    [SerializeField] private bool ignoreVerticalMotion = true;

    #endregion

    #region History / Rotation Settings

    [Header("History")]
    [Min(0f)]
    [SerializeField] private float initialBackfillDistance = 20f;

    [Min(1f)]
    [SerializeField] private float maxHistoryDistance = 80f;

    [Min(1)]
    [SerializeField] private int pruneBatchSize = 32;

    [Header("Follower Rotation")]
    [Min(0.01f)]
    [SerializeField] private float tangentProbeDistance = 0.35f;

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool drawPath = true;
    [SerializeField] private bool drawStoredSamples = true;

    [Min(1)]
    [SerializeField] private int drawEveryNthSample = 5;

    [Min(0.001f)]
    [SerializeField] private float sampleGizmoRadius = 0.04f;

    [Header("Runtime Debug - Read Only")]
    [SerializeField] private bool isInitialized;
    [SerializeField] private bool acceptedMovementThisTick;
    [SerializeField] private float currentPlanarSpeed;
    [SerializeField] private float headProgress;
    [SerializeField] private float recordedEndProgress;
    [SerializeField] private float distanceSinceLastSample;
    [SerializeField] private int currentSampleCount;

    #endregion

    #region Runtime

    private Transform pathSource;
    private Rigidbody leaderBody;

    private readonly List<PathPoint> samples = new List<PathPoint>(512);

    private Vector3 lastObservedPosition;
    private Vector3 livePathEndPosition;

    #endregion

    #region Public API

    public bool IsInitialized => isInitialized;
    public float HeadProgress => headProgress;
    public float RecordedEndProgress => recordedEndProgress;
    public float OldestProgress => samples.Count > 0 ? samples[0].distance : headProgress;
    public int SampleCount => samples.Count;
    public IReadOnlyList<PathPoint> Samples => samples;

    public void SetLeaderBody(Rigidbody body)
    {
        leaderBody = body;
    }

    public void Initialize(Transform newPathSource)
    {
        if (newPathSource == null)
        {
            Debug.LogError("[SnakePathHistory] Cannot initialize: Path Source is null.", this);
            return;
        }

        pathSource = newPathSource;
        ResetHistoryToSource();

        Debug.Log($"[SnakePathHistory] Initialized from {pathSource.name}.", this);
    }

    #endregion

    #region Initial / Recovery Seeding

    public void ResetHistoryToSource()
    {
        if (pathSource == null) return;

        samples.Clear();

        Vector3 sourcePosition = pathSource.position;
        Vector3 forward = Vector3.ProjectOnPlane(pathSource.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        if (initialBackfillDistance > 0f)
        {
            int backfillCount = Mathf.CeilToInt(initialBackfillDistance / sampleSpacing);

            for (int i = backfillCount; i >= 1; i--)
            {
                float distance = -i * sampleSpacing;
                samples.Add(new PathPoint(sourcePosition + forward * distance, distance));
            }
        }

        samples.Add(new PathPoint(sourcePosition, 0f));

        lastObservedPosition = sourcePosition;
        livePathEndPosition = sourcePosition;

        distanceSinceLastSample = 0f;
        recordedEndProgress = 0f;
        headProgress = 0f;

        acceptedMovementThisTick = false;
        currentPlanarSpeed = 0f;
        currentSampleCount = samples.Count;
        isInitialized = true;
    }

    /// <summary>
    /// Recovery handoff after reverse-tow.
    ///
    /// Current C1/C2/... world positions become exact anchors at their normal
    /// logical spacing values. This preserves the recovered shape without
    /// teleporting carts back to the old path.
    /// </summary>
    public bool ResetHistoryFromCurrentChain(Transform newPathSource, IReadOnlyList<Vector3> followerPositions, float firstFollowerSpacing, float followerSpacing)
    {
        if (newPathSource == null)
        {
            Debug.LogError("[SnakePathHistory] Recovery source is null.", this);
            return false;
        }

        if (followerPositions == null || followerPositions.Count == 0)
        {
            Debug.LogError("[SnakePathHistory] Recovery requires at least C1.", this);
            return false;
        }

        pathSource = newPathSource;

        float safeFirstSpacing = Mathf.Max(0.01f, firstFollowerSpacing);
        float safeFollowerSpacing = Mathf.Max(0.01f, followerSpacing);

        Vector3 sourcePosition = pathSource.position;
        float seedY = sourcePosition.y;

        List<SeedAnchor> followerAnchors = new List<SeedAnchor>(followerPositions.Count);

        for (int i = 0; i < followerPositions.Count; i++)
        {
            Vector3 position = followerPositions[i];
            position.y = seedY;

            float progress = -(safeFirstSpacing + i * safeFollowerSpacing);
            followerAnchors.Add(new SeedAnchor(position, progress));
        }

        List<SeedAnchor> anchors = new List<SeedAnchor>(followerPositions.Count + 2);

        SeedAnchor tail = followerAnchors[followerAnchors.Count - 1];
        Vector3 backwardDirection;

        if (followerAnchors.Count >= 2)
        {
            Vector3 previous = followerAnchors[followerAnchors.Count - 2].position;
            backwardDirection = Vector3.ProjectOnPlane(tail.position - previous, Vector3.up);
        }
        else
        {
            backwardDirection = Vector3.ProjectOnPlane(tail.position - sourcePosition, Vector3.up);
        }

        if (backwardDirection.sqrMagnitude < 0.0001f)
        {
            Vector3 sourceForward = Vector3.ProjectOnPlane(pathSource.forward, Vector3.up);
            backwardDirection = sourceForward.sqrMagnitude > 0.0001f ? -sourceForward.normalized : -Vector3.forward;
        }
        else
        {
            backwardDirection.Normalize();
        }

        if (initialBackfillDistance > 0.01f)
        {
            anchors.Add(new SeedAnchor(tail.position + backwardDirection * initialBackfillDistance, tail.distance - initialBackfillDistance));
        }

        // Stored samples are ordered oldest progress -> newest progress.
        for (int i = followerAnchors.Count - 1; i >= 0; i--) anchors.Add(followerAnchors[i]);

        anchors.Add(new SeedAnchor(sourcePosition, 0f));

        samples.Clear();

        AddSeedAnchor(anchors[0]);

        for (int i = 1; i < anchors.Count; i++) AddSeedSegment(anchors[i - 1], anchors[i]);

        lastObservedPosition = sourcePosition;
        livePathEndPosition = sourcePosition;

        distanceSinceLastSample = 0f;
        recordedEndProgress = 0f;
        headProgress = 0f;

        acceptedMovementThisTick = false;
        currentPlanarSpeed = 0f;
        currentSampleCount = samples.Count;
        isInitialized = true;

        return true;
    }

    private void AddSeedAnchor(SeedAnchor anchor)
    {
        if (samples.Count > 0 && Mathf.Abs(samples[samples.Count - 1].distance - anchor.distance) < 0.0001f)
        {
            samples[samples.Count - 1] = new PathPoint(anchor.position, anchor.distance);
            return;
        }

        samples.Add(new PathPoint(anchor.position, anchor.distance));
    }

    private void AddSeedSegment(SeedAnchor from, SeedAnchor to)
    {
        float progressSpan = to.distance - from.distance;

        if (progressSpan <= 0.00001f)
        {
            AddSeedAnchor(to);
            return;
        }

        int steps = Mathf.Max(1, Mathf.CeilToInt(progressSpan / sampleSpacing));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            AddSeedAnchor(new SeedAnchor(Vector3.Lerp(from.position, to.position, t), Mathf.Lerp(from.distance, to.distance, t)));
        }
    }

    #endregion

    #region Recording

    public void TickHistory()
    {
        if (!isInitialized || pathSource == null) return;
        RecordPathSourceMovement(pathSource.position);
    }

    private void RecordPathSourceMovement(Vector3 currentSourcePosition)
    {
        acceptedMovementThisTick = false;

        Vector3 frameDelta = currentSourcePosition - lastObservedPosition;
        if (ignoreVerticalMotion) frameDelta = Vector3.ProjectOnPlane(frameDelta, Vector3.up);

        float frameDisplacement = frameDelta.magnitude;

        currentPlanarSpeed = frameDisplacement / Mathf.Max(Time.fixedDeltaTime, 0.00001f);
        lastObservedPosition = currentSourcePosition;

        // Only reject tiny solver jitter. Legitimate hinge swing remains path data.
        if (frameDisplacement < movementAcceptanceDistance) return;

        Vector3 segmentDelta = currentSourcePosition - livePathEndPosition;
        if (ignoreVerticalMotion) segmentDelta = Vector3.ProjectOnPlane(segmentDelta, Vector3.up);

        float segmentLength = segmentDelta.magnitude;
        if (segmentLength <= 0.00001f) return;

        acceptedMovementThisTick = true;

        AppendSegment(livePathEndPosition, currentSourcePosition, segmentLength);

        livePathEndPosition = currentSourcePosition;
        recordedEndProgress = samples[samples.Count - 1].distance + distanceSinceLastSample;
        headProgress = recordedEndProgress;
        currentSampleCount = samples.Count;

        PruneHistory();
    }

    private void AppendSegment(Vector3 segmentStartPosition, Vector3 segmentEndPosition, float segmentLength)
    {
        float remainingLength = segmentLength;
        Vector3 remainingStart = segmentStartPosition;

        while (distanceSinceLastSample + remainingLength >= sampleSpacing)
        {
            float distanceNeeded = sampleSpacing - distanceSinceLastSample;
            float t = remainingLength > 0.00001f ? distanceNeeded / remainingLength : 1f;

            Vector3 newPosition = Vector3.Lerp(remainingStart, segmentEndPosition, Mathf.Clamp01(t));
            PathPoint previous = samples[samples.Count - 1];

            samples.Add(new PathPoint(newPosition, previous.distance + sampleSpacing));

            remainingStart = newPosition;
            remainingLength -= distanceNeeded;
            distanceSinceLastSample = 0f;
        }

        distanceSinceLastSample += Mathf.Max(0f, remainingLength);
    }

    #endregion

    #region Path Queries

    public bool TryGetPoseAtProgress(float targetProgress, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!TryGetPositionAtProgress(targetProgress, out position)) return false;

        float oldest = samples[0].distance;
        float newest = recordedEndProgress;

        targetProgress = Mathf.Clamp(targetProgress, oldest, newest);

        float beforeProgress = Mathf.Max(oldest, targetProgress - tangentProbeDistance);
        float afterProgress = Mathf.Min(newest, targetProgress + tangentProbeDistance);

        if (!TryGetPositionAtProgress(beforeProgress, out Vector3 beforePosition)) return false;
        if (!TryGetPositionAtProgress(afterProgress, out Vector3 afterPosition)) return false;

        Vector3 tangent = Vector3.ProjectOnPlane(afterPosition - beforePosition, Vector3.up);

        if (tangent.sqrMagnitude < 0.0001f) tangent = GetFallbackTangent();
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;

        rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        return true;
    }

    public bool TryGetPositionAtProgress(float targetProgress, out Vector3 position)
    {
        position = Vector3.zero;

        if (!isInitialized || samples.Count == 0) return false;

        targetProgress = Mathf.Clamp(targetProgress, samples[0].distance, recordedEndProgress);

        PathPoint newestStored = samples[samples.Count - 1];

        if (targetProgress >= newestStored.distance)
        {
            float liveDistance = recordedEndProgress - newestStored.distance;

            if (liveDistance <= 0.00001f)
            {
                position = newestStored.position;
                return true;
            }

            float t = Mathf.Clamp01((targetProgress - newestStored.distance) / liveDistance);
            position = Vector3.Lerp(newestStored.position, livePathEndPosition, t);
            return true;
        }

        int low = 0;
        int high = samples.Count - 1;

        while (low < high)
        {
            int mid = (low + high) / 2;

            if (samples[mid].distance < targetProgress) low = mid + 1;
            else high = mid;
        }

        int upperIndex = low;

        if (upperIndex == 0)
        {
            position = samples[0].position;
            return true;
        }

        PathPoint lower = samples[upperIndex - 1];
        PathPoint upper = samples[upperIndex];

        float distanceRange = upper.distance - lower.distance;
        float t2 = distanceRange > 0.00001f ? (targetProgress - lower.distance) / distanceRange : 0f;

        position = Vector3.Lerp(lower.position, upper.position, t2);
        return true;
    }

    private Vector3 GetFallbackTangent()
    {
        if (samples.Count >= 2)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(samples[samples.Count - 1].position - samples[samples.Count - 2].position, Vector3.up);
            if (tangent.sqrMagnitude > 0.0001f) return tangent.normalized;
        }

        if (leaderBody != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(leaderBody.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
        }

        if (pathSource != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(pathSource.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
        }

        return Vector3.forward;
    }

    #endregion

    #region History Pruning

    private void PruneHistory()
    {
        if (samples.Count <= 2) return;

        float keepFrom = headProgress - maxHistoryDistance;
        int removeCount = 0;

        while (removeCount < samples.Count - 2 && samples[removeCount + 1].distance < keepFrom) removeCount++;

        if (removeCount >= pruneBatchSize) samples.RemoveRange(0, removeCount);

        currentSampleCount = samples.Count;
    }

    #endregion

    #region Debug Drawing / Validation

    private void OnDrawGizmos()
    {
        if (!drawPath || !Application.isPlaying || !isInitialized || samples.Count == 0) return;

        Gizmos.color = Color.cyan;

        for (int i = 1; i < samples.Count; i++) Gizmos.DrawLine(samples[i - 1].position, samples[i].position);

        Gizmos.DrawLine(samples[samples.Count - 1].position, livePathEndPosition);

        if (drawStoredSamples)
        {
            Gizmos.color = Color.yellow;
            int stride = Mathf.Max(1, drawEveryNthSample);

            for (int i = 0; i < samples.Count; i += stride) Gizmos.DrawSphere(samples[i].position, sampleGizmoRadius);
        }

        if (TryGetPositionAtProgress(headProgress, out Vector3 activeHeadPosition))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(activeHeadPosition, sampleGizmoRadius * 2f);
        }
    }

    private void OnValidate()
    {
        sampleSpacing = Mathf.Max(0.001f, sampleSpacing);
        movementAcceptanceDistance = Mathf.Max(0f, movementAcceptanceDistance);
        tangentProbeDistance = Mathf.Max(0.01f, tangentProbeDistance);
        initialBackfillDistance = Mathf.Max(0f, initialBackfillDistance);
        maxHistoryDistance = Mathf.Max(initialBackfillDistance + sampleSpacing * 2f, maxHistoryDistance);
        pruneBatchSize = Mathf.Max(1, pruneBatchSize);
        drawEveryNthSample = Mathf.Max(1, drawEveryNthSample);
    }

    #endregion
}