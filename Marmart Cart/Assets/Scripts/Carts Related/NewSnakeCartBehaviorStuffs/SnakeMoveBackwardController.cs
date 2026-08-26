using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-shot stuck recovery mechanic.
///
/// The leading cart continuously records its recent physical trajectory.
///
/// When MoveBackward is triggered:
/// - normal player control temporarily stops
/// - leader travels backward along its own recorded trajectory
/// - SnakePathHistory moves its shared HeadProgress backward
/// - every chained cart therefore moves backward during the same physics ticks
/// - abandoned future path is deleted when the movement finishes
///
/// This is NOT normal reverse driving.
/// CartControlScript decides when MoveBackward is allowed.
/// </summary>
public class SnakeMoveBackwardController : MonoBehaviour
{
    [System.Serializable]
    private struct LeaderPathPoint
    {
        public Vector3 position;
        public Quaternion rotation;
        public float distance;

        public LeaderPathPoint(Vector3 position, Quaternion rotation, float distance)
        {
            this.position = position;
            this.rotation = rotation;
            this.distance = distance;
        }
    }

    [Header("Move Backward")]

    [Tooltip("How far the leading cart travels backward along its previous path.")]
    [Min(0.1f)]
    [SerializeField] private float moveBackwardDistance = 3f;

    [Tooltip("Base speed of the automatic backward movement.")]
    [Min(0.1f)]
    [SerializeField] private float moveBackwardSpeed = 10f;

    [Tooltip("Speed multiplier from movement start at 0 to movement finish at 1.")]
    [SerializeField]
    private AnimationCurve moveBackwardSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1.4f),
        new Keyframe(0.3f, 1f),
        new Keyframe(1f, 0.35f)
    );

    [Tooltip("How much follower path progress moves backward for each meter the leader moves backward.")]
    [Min(0f)]
    [SerializeField] private float chainMoveBackwardMultiplier = 1f;

    [Tooltip("Minimum delay before another MoveBackward can begin.")]
    [Min(0f)]
    [SerializeField] private float moveBackwardCooldown = 1.25f;

    [Header("Leader Path Recording")]

    [Tooltip("Distance between stored samples of the leading cart trajectory.")]
    [Min(0.01f)]
    [SerializeField] private float historySampleSpacing = 0.08f;

    [Tooltip("Maximum amount of leading-cart trajectory retained.")]
    [Min(1f)]
    [SerializeField] private float maxHistoryDistance = 20f;

    [Tooltip("Ignores microscopic Rigidbody solver movement.")]
    [Min(0f)]
    [SerializeField] private float minimumRecordMovement = 0.01f;

    [Header("Debug")]

    [SerializeField] private bool drawLeaderHistory = true;

    [Header("Runtime - Read Only")]

    [SerializeField] private bool isInitialized;
    [SerializeField] private bool isMovingBackward;
    [SerializeField] private float currentHistoryProgress;
    [SerializeField] private float moveBackwardCurrentProgress;
    [SerializeField] private float moveBackwardTargetProgress;
    [SerializeField] private float currentMoveBackwardCompletion;
    [SerializeField] private int historySampleCount;

    private Rigidbody leaderBody;
    private LeadingCartBehaviour leadingMovement;
    private CartControlScript cartControl;
    private SnakePathHistory snakePathHistory;

    private readonly List<LeaderPathPoint> history = new List<LeaderPathPoint>(512);

    private Vector3 lastObservedLeaderPosition;
    private Vector3 liveHistoryPosition;
    private Quaternion liveHistoryRotation;

    private float distanceSinceLastStoredSample;
    private float nextAllowedMoveBackwardTime;

    public bool IsMovingBackward => isMovingBackward;

    public System.Action OnMoveBackwardStarted;
    public System.Action OnMoveBackwardFinished;

    public void Initialize(Rigidbody body, LeadingCartBehaviour movement, CartControlScript control, SnakePathHistory pathHistory)
    {
        if (body == null || movement == null || control == null || pathHistory == null)
        {
            Debug.LogError("[SnakeMoveBackwardController] Missing required initialization reference.", this);
            return;
        }

        if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;

        leaderBody = body;
        leadingMovement = movement;
        cartControl = control;
        snakePathHistory = pathHistory;

        cartControl.OnMoveBackwardPressed += TryBeginMoveBackward;

        ResetLeaderHistory();

        isInitialized = true;
    }

    /// <summary>
    /// Called once per SnakeCartManager FixedUpdate.
    ///
    /// Returns true while this system owns the leading cart's movement.
    /// </summary>
    public bool TickMoveBackward()
    {
        if (!isInitialized || leaderBody == null) return false;

        if (isMovingBackward)
        {
            TickActiveMoveBackward();
            return true;
        }

        RecordLeaderHistory();
        return false;
    }

    private void TryBeginMoveBackward()
    {
        if (!isInitialized || isMovingBackward) return;
        if (Time.time < nextAllowedMoveBackwardTime) return;
        if (history.Count < 2) return;

        float oldestProgress = history[0].distance;
        float availableDistance = currentHistoryProgress - oldestProgress;

        if (availableDistance <= 0.05f) return;

        float actualDistance = Mathf.Min(moveBackwardDistance, availableDistance);

        moveBackwardCurrentProgress = currentHistoryProgress;
        moveBackwardTargetProgress = currentHistoryProgress - actualDistance;
        currentMoveBackwardCompletion = 0f;

        isMovingBackward = true;

        leadingMovement.SetSpeedToZero();

        leaderBody.linearVelocity = Vector3.zero;
        leaderBody.angularVelocity = Vector3.zero;

        cartControl.DisableControl();

        snakePathHistory.BeginMoveBackward();

        OnMoveBackwardStarted?.Invoke();
    }

    private void TickActiveMoveBackward()
    {
        float totalDistance = Mathf.Max(0.001f, currentHistoryProgress - moveBackwardTargetProgress);
        float remainingDistance = moveBackwardCurrentProgress - moveBackwardTargetProgress;

        currentMoveBackwardCompletion = Mathf.Clamp01(1f - remainingDistance / totalDistance);

        float speedMultiplier = Mathf.Max(0.05f, moveBackwardSpeedCurve.Evaluate(currentMoveBackwardCompletion));
        float requestedStep = moveBackwardSpeed * speedMultiplier * Time.fixedDeltaTime;
        float actualStep = Mathf.Min(requestedStep, remainingDistance);

        float newProgress = moveBackwardCurrentProgress - actualStep;

        if (!TryGetLeaderPoseAtProgress(newProgress, out Vector3 targetPosition, out Quaternion targetRotation))
        {
            FinishMoveBackward();
            return;
        }

        leaderBody.linearVelocity = Vector3.zero;
        leaderBody.angularVelocity = Vector3.zero;

        leaderBody.MovePosition(targetPosition);
        leaderBody.MoveRotation(targetRotation);

        float actualProgressMoved = moveBackwardCurrentProgress - newProgress;
        moveBackwardCurrentProgress = newProgress;

        snakePathHistory.MoveHeadBackwardBy(actualProgressMoved * chainMoveBackwardMultiplier);

        if (moveBackwardCurrentProgress <= moveBackwardTargetProgress + 0.001f) FinishMoveBackward();
    }

    private void FinishMoveBackward()
    {
        isMovingBackward = false;

        snakePathHistory.EndMoveBackwardAndTruncate();

        TruncateLeaderHistoryAt(moveBackwardCurrentProgress);

        leaderBody.linearVelocity = Vector3.zero;
        leaderBody.angularVelocity = Vector3.zero;

        // Keep this only if LeadingCartBehaviour already exposes ResetSpeed().
        leadingMovement.ResetSpeed();

        cartControl.EnableControl();

        nextAllowedMoveBackwardTime = Time.time + moveBackwardCooldown;

        OnMoveBackwardFinished?.Invoke();
    }

    private void ResetLeaderHistory()
    {
        history.Clear();

        Vector3 position = leaderBody.position;
        Quaternion rotation = leaderBody.rotation;

        currentHistoryProgress = 0f;
        distanceSinceLastStoredSample = 0f;

        lastObservedLeaderPosition = position;
        liveHistoryPosition = position;
        liveHistoryRotation = rotation;

        history.Add(new LeaderPathPoint(position, rotation, 0f));

        historySampleCount = history.Count;
    }

    private void RecordLeaderHistory()
    {
        Vector3 currentPosition = leaderBody.position;
        Quaternion currentRotation = leaderBody.rotation;

        Vector3 frameDelta = Vector3.ProjectOnPlane(currentPosition - lastObservedLeaderPosition, Vector3.up);
        float frameDistance = frameDelta.magnitude;

        lastObservedLeaderPosition = currentPosition;

        if (frameDistance < minimumRecordMovement)
        {
            liveHistoryPosition = currentPosition;
            liveHistoryRotation = currentRotation;
            return;
        }

        Vector3 segmentDelta = Vector3.ProjectOnPlane(currentPosition - liveHistoryPosition, Vector3.up);
        float segmentLength = segmentDelta.magnitude;

        if (segmentLength <= 0.00001f)
        {
            liveHistoryPosition = currentPosition;
            liveHistoryRotation = currentRotation;
            return;
        }

        AppendLeaderHistorySegment(liveHistoryPosition, liveHistoryRotation, currentPosition, currentRotation, segmentLength);

        liveHistoryPosition = currentPosition;
        liveHistoryRotation = currentRotation;

        currentHistoryProgress = history[history.Count - 1].distance + distanceSinceLastStoredSample;

        PruneLeaderHistory();

        historySampleCount = history.Count;
    }

    private void AppendLeaderHistorySegment(Vector3 startPosition, Quaternion startRotation, Vector3 endPosition, Quaternion endRotation, float segmentLength)
    {
        float remainingLength = segmentLength;
        Vector3 remainingStartPosition = startPosition;
        Quaternion remainingStartRotation = startRotation;

        while (distanceSinceLastStoredSample + remainingLength >= historySampleSpacing)
        {
            float distanceNeeded = historySampleSpacing - distanceSinceLastStoredSample;
            float t = remainingLength > 0.00001f ? distanceNeeded / remainingLength : 1f;

            Vector3 samplePosition = Vector3.Lerp(remainingStartPosition, endPosition, t);
            Quaternion sampleRotation = Quaternion.Slerp(remainingStartRotation, endRotation, t);

            float sampleDistance = history[history.Count - 1].distance + historySampleSpacing;

            history.Add(new LeaderPathPoint(samplePosition, sampleRotation, sampleDistance));

            remainingStartPosition = samplePosition;
            remainingStartRotation = sampleRotation;

            remainingLength -= distanceNeeded;
            distanceSinceLastStoredSample = 0f;
        }

        distanceSinceLastStoredSample += Mathf.Max(0f, remainingLength);
    }

    private bool TryGetLeaderPoseAtProgress(float progress, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (history.Count == 0) return false;

        progress = Mathf.Clamp(progress, history[0].distance, currentHistoryProgress);

        LeaderPathPoint newestStored = history[history.Count - 1];

        if (progress >= newestStored.distance)
        {
            float liveDistance = currentHistoryProgress - newestStored.distance;

            if (liveDistance <= 0.00001f)
            {
                position = newestStored.position;
                rotation = newestStored.rotation;
                return true;
            }

            float p = Mathf.Clamp01((progress - newestStored.distance) / liveDistance);

            position = Vector3.Lerp(newestStored.position, liveHistoryPosition, p);
            rotation = Quaternion.Slerp(newestStored.rotation, liveHistoryRotation, p);

            return true;
        }

        int low = 0;
        int high = history.Count - 1;

        while (low < high)
        {
            int mid = (low + high) / 2;

            if (history[mid].distance < progress) low = mid + 1;
            else high = mid;
        }

        int upperIndex = low;

        if (upperIndex == 0)
        {
            position = history[0].position;
            rotation = history[0].rotation;
            return true;
        }

        LeaderPathPoint lower = history[upperIndex - 1];
        LeaderPathPoint upper = history[upperIndex];

        float range = upper.distance - lower.distance;
        float t = range > 0.00001f ? (progress - lower.distance) / range : 0f;

        position = Vector3.Lerp(lower.position, upper.position, t);
        rotation = Quaternion.Slerp(lower.rotation, upper.rotation, t);

        return true;
    }

    private void TruncateLeaderHistoryAt(float progress)
    {
        if (!TryGetLeaderPoseAtProgress(progress, out Vector3 position, out Quaternion rotation)) return;

        int removeFromIndex = history.Count;

        for (int i = 0; i < history.Count; i++)
        {
            if (history[i].distance > progress)
            {
                removeFromIndex = i;
                break;
            }
        }

        if (removeFromIndex < history.Count) history.RemoveRange(removeFromIndex, history.Count - removeFromIndex);

        if (history.Count == 0 || Mathf.Abs(history[history.Count - 1].distance - progress) > 0.0001f)
        {
            history.Add(new LeaderPathPoint(position, rotation, progress));
        }
        else
        {
            history[history.Count - 1] = new LeaderPathPoint(position, rotation, progress);
        }

        currentHistoryProgress = progress;
        distanceSinceLastStoredSample = 0f;

        liveHistoryPosition = leaderBody.position;
        liveHistoryRotation = leaderBody.rotation;
        lastObservedLeaderPosition = leaderBody.position;

        historySampleCount = history.Count;
    }

    private void PruneLeaderHistory()
    {
        float keepFromProgress = currentHistoryProgress - maxHistoryDistance;

        int removeCount = 0;

        while (removeCount < history.Count - 2 && history[removeCount + 1].distance < keepFromProgress) removeCount++;

        if (removeCount > 0) history.RemoveRange(0, removeCount);
    }

    private void OnDrawGizmos()
    {
        if (!drawLeaderHistory || !Application.isPlaying || history.Count < 2) return;

        Gizmos.color = Color.red;

        for (int i = 1; i < history.Count; i++) Gizmos.DrawLine(history[i - 1].position, history[i].position);
    }

    private void OnDestroy()
    {
        if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;
    }
}