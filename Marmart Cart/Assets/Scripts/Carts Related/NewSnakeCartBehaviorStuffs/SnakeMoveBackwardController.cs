using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MoveBackward has two deliberately separate modes:
///
/// 1) Leader only:
///    Drives the Rigidbody backward with a short speed curve.
///
/// 2) Leader + followers:
///    Tail retreats along old path history, C1->Tail follow a temporary reverse
///    path, and C1 pulls the Leader through a slack reverse-tow constraint.
///    The final pose is then converted into a fresh normal path.
/// </summary>
public class SnakeMoveBackwardController : MonoBehaviour
{
    #region Chain Reverse Settings

    [Header("Chain Move Backward")]
    [Min(0.1f)]
    [SerializeField] private float moveBackwardDistance = 3f;

    [Min(0.05f)]
    [SerializeField] private float moveBackwardDuration = 0.7f;

    [Tooltip("X = normalized time, Y = normalized tail travel.")]
    [SerializeField] private AnimationCurve moveBackwardMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Min(0f)]
    [SerializeField] private float moveBackwardCooldown = 1.25f;

    [Header("Reverse Follower Path")]
    [Min(0.01f)]
    [SerializeField] private float tangentSampleDistance = 0.2f;

    [Min(0f)]
    [SerializeField] private float followerRotationFollowSpeed = 12f;

    [Tooltip("If old path history is too short, continue backward from the oldest available tangent.")]
    [SerializeField] private bool allowStraightExtensionWhenHistoryRunsOut = true;

    [Header("C1 -> Leader Reverse Tow")]
    [Tooltip("Extra separation C1 may create before it begins pulling the Leader.")]
    [Min(0f)]
    [SerializeField] private float reverseTowExtraSlack = 0.75f;

    [Min(0f)]
    [SerializeField] private float towActivationTolerance = 0.01f;

    [Min(0f)]
    [SerializeField] private float leaderTowRotationFollowSpeed = 10f;

    [SerializeField] private bool rotateLeaderFromReverseTow = true;

    #endregion

    #region Single Cart Settings

    [Header("Single Cart Move Backward")]
    [Tooltip("Maximum backward planar speed when the Leader has no followers.")]
    [Min(0.1f)]
    [SerializeField] private float singleCartReverseSpeed = 6f;

    [Tooltip("Duration of the lone-cart reverse drive.")]
    [Min(0.05f)]
    [SerializeField] private float singleCartReverseDuration = 0.7f;

    [Tooltip("X = normalized time, Y = normalized backward speed.")]
    [SerializeField]
    private AnimationCurve singleCartReverseSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1f),
        new Keyframe(0.75f, 1f),
        new Keyframe(1f, 0f)
    );

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool drawTemporaryPath = true;
    [SerializeField] private bool drawReverseTow = true;
    [SerializeField] private bool debugMoveBackward = false;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isInitialized;
    [SerializeField] private bool isMovingBackward;
    [SerializeField] private bool isSingleCartReversing;

    [SerializeField] private float requestedMoveBackwardDistance;
    [SerializeField] private float movedTailDistance;
    [SerializeField] private float tailMainStartProgress;
    [SerializeField] private float availableOldPathDistance;
    [SerializeField] private float temporaryHeadProgress;

    [Header("Reverse Tow Runtime - Read Only")]
    [SerializeField] private float startingLeaderToC1Distance;
    [SerializeField] private float activeReverseTowDistance;
    [SerializeField] private float currentLeaderToC1Distance;
    [SerializeField] private bool reverseTowIsTaut;
    [SerializeField] private float leaderMovedDistance;

    #endregion

    #region References / Runtime

    private SnakeCartManager snakeManager;
    private SnakePathHistory normalPath;

    private Rigidbody leaderBody;
    private CartControlScript cartControl;

    private TemporaryPath temporaryPath;
    private float[] followerOffsetsFromTemporaryHead;

    private int expectedSnakeCount;

    private float elapsedTime;
    private float singleCartReverseElapsed;
    private float nextAllowedMoveBackwardTime;

    private bool leaderWasKinematic;
    private float rewindPlaneY;

    private Vector3 leaderStartPosition;
    private Vector3 oldestPathPosition;
    private Vector3 oldestPathBackwardDirection;
    private Vector3 noHistoryExtensionOrigin;
    private Vector3 noHistoryExtensionDirection;

    #endregion

    #region Public API / Events

    /// <summary>
    /// True for either the lone-cart reverse drive or the full reverse-tow mode.
    /// </summary>
    public bool IsMovingBackward => isMovingBackward || isSingleCartReversing;

    public System.Action OnMoveBackwardStarted;
    public System.Action OnMoveBackwardFinished;

    public void Initialize(SnakeCartManager manager, Rigidbody body, CartControlScript control, SnakePathHistory pathHistory)
    {
        if (manager == null || body == null || control == null || pathHistory == null)
        {
            Debug.LogError("[SnakeMoveBackwardController] Missing initialization reference.", this);
            return;
        }

        if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;

        snakeManager = manager;
        leaderBody = body;
        cartControl = control;
        normalPath = pathHistory;

        cartControl.OnMoveBackwardPressed += TryBeginMoveBackward;

        temporaryPath ??= new TemporaryPath();
        isInitialized = true;
    }

    public bool PlayBackward(float distance)
    {
        if (!isInitialized || IsMovingBackward) return false;
        if (Time.time < nextAllowedMoveBackwardTime || distance <= 0f) return false;

        List<GameObject> snakeBody = snakeManager.GetSnakeBody();
        if (snakeBody == null || snakeBody.Count == 0) return false;

        if (snakeBody.Count == 1)
        {
            BeginSingleCartReverse();
            return true;
        }

        if (normalPath == null || !normalPath.IsInitialized) return false;
        return BeginChainMoveBackward(distance, snakeBody);
    }

    [ContextMenu("TEST - Move Backward")]
    private void TestMoveBackward()
    {
        PlayBackward(moveBackwardDistance);
    }

    #endregion

    #region Tick / Input

    private void TryBeginMoveBackward()
    {
        PlayBackward(moveBackwardDistance);
    }

    /// <summary>
    /// Returns TRUE only while the 1+ follower reverse-tow mode owns the full
    /// snake. The lone-cart speed curve returns FALSE so the normal hinge/path
    /// may continue recording that cart's real motion.
    /// </summary>
    public bool TickMoveBackward()
    {
        if (!isInitialized) return false;

        if (isSingleCartReversing)
        {
            TickSingleCartReverse();
            return false;
        }

        if (!isMovingBackward) return false;

        TickChainMoveBackward();
        return true;
    }

    #endregion

    #region Single Cart Reverse

    private void BeginSingleCartReverse()
    {
        singleCartReverseElapsed = 0f;
        isSingleCartReversing = true;
        OnMoveBackwardStarted?.Invoke();
    }

    private void TickSingleCartReverse()
    {
        singleCartReverseElapsed += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(singleCartReverseElapsed / singleCartReverseDuration);
        float speedMultiplier = Mathf.Max(0f, singleCartReverseSpeedCurve.Evaluate(normalizedTime));
        float reverseSpeed = singleCartReverseSpeed * speedMultiplier;

        Vector3 forward = Vector3.ProjectOnPlane(leaderBody.transform.forward, Vector3.up);

        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();

            // Only planar velocity is authored. Vertical velocity and angular
            // velocity remain physical so the cart can still pivot naturally.
            float verticalVelocity = Vector3.Dot(leaderBody.linearVelocity, Vector3.up);
            leaderBody.linearVelocity = -forward * reverseSpeed + Vector3.up * verticalVelocity;
        }

        if (normalizedTime < 1f) return;

        isSingleCartReversing = false;
        nextAllowedMoveBackwardTime = Time.time + moveBackwardCooldown;
        OnMoveBackwardFinished?.Invoke();
    }

    #endregion

    #region Chain Reverse Start

    private bool BeginChainMoveBackward(float distance, List<GameObject> snakeBody)
    {
        if (snakeBody == null || snakeBody.Count < 2) return false;

        expectedSnakeCount = snakeBody.Count;
        rewindPlaneY = leaderBody.position.y;

        int tailIndex = snakeBody.Count - 1;

        tailMainStartProgress = snakeManager.GetDistancePathProgressForSnakeIndex(tailIndex);
        tailMainStartProgress = Mathf.Clamp(tailMainStartProgress, normalPath.OldestProgress, normalPath.HeadProgress);

        availableOldPathDistance = Mathf.Max(0f, tailMainStartProgress - normalPath.OldestProgress);
        requestedMoveBackwardDistance = distance;

        if (!allowStraightExtensionWhenHistoryRunsOut && availableOldPathDistance <= 0.05f) return false;
        if (!BuildTemporaryFollowerPath(snakeBody)) return false;

        PrepareOldPathExtensionData(snakeBody);

        GameObject firstFollower = snakeBody[1];
        Vector3 leaderToC1 = Vector3.ProjectOnPlane(leaderBody.position - firstFollower.transform.position, Vector3.up);

        startingLeaderToC1Distance = leaderToC1.magnitude;
        activeReverseTowDistance = startingLeaderToC1Distance + reverseTowExtraSlack;

        movedTailDistance = 0f;
        elapsedTime = 0f;
        temporaryHeadProgress = temporaryPath.HeadProgress;

        currentLeaderToC1Distance = startingLeaderToC1Distance;
        reverseTowIsTaut = false;

        leaderStartPosition = leaderBody.position;
        leaderMovedDistance = 0f;

        // Reverse-tow owns the Leader pose, so remove old motion and temporarily
        // make the Rigidbody kinematic. Normal drift/controller state is untouched.
        leaderBody.linearVelocity = Vector3.zero;
        leaderBody.angularVelocity = Vector3.zero;

        leaderWasKinematic = leaderBody.isKinematic;
        leaderBody.isKinematic = true;

        isMovingBackward = true;
        OnMoveBackwardStarted?.Invoke();

        if (debugMoveBackward)
        {
            Debug.Log($"[MoveBackward] START | carts:{snakeBody.Count} | requested:{requestedMoveBackwardDistance:F2} | startTow:{startingLeaderToC1Distance:F2} | activeTow:{activeReverseTowDistance:F2}", this);
        }

        return true;
    }

    #endregion

    #region Temporary Reverse Path

    /// <summary>
    /// Seeds only C1 -> C2 -> ... -> Tail. The Leader and normal physical probe
    /// are intentionally excluded from this temporary path.
    /// </summary>
    private bool BuildTemporaryFollowerPath(List<GameObject> snakeBody)
    {
        if (snakeBody == null || snakeBody.Count < 2 || snakeBody[1] == null) return false;

        temporaryPath.Clear();

        followerOffsetsFromTemporaryHead = new float[snakeBody.Count];
        float[] followerInitialProgress = new float[snakeBody.Count];

        Vector3 c1Position = FlattenToRewindPlane(snakeBody[1].transform.position);
        temporaryPath.Reset(c1Position);
        followerInitialProgress[1] = 0f;

        for (int i = 2; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null) return false;

            Vector3 position = FlattenToRewindPlane(snakeBody[i].transform.position);
            followerInitialProgress[i] = temporaryPath.Append(position);
        }

        float initialHead = temporaryPath.HeadProgress;

        for (int i = 1; i < snakeBody.Count; i++) followerOffsetsFromTemporaryHead[i] = initialHead - followerInitialProgress[i];

        temporaryHeadProgress = initialHead;
        return true;
    }

    private void PrepareOldPathExtensionData(List<GameObject> snakeBody)
    {
        GameObject tail = snakeBody[snakeBody.Count - 1];

        noHistoryExtensionOrigin = FlattenToRewindPlane(tail.transform.position);

        Vector3 endTangent = temporaryPath.GetEndTangent();
        if (endTangent.sqrMagnitude < 0.0001f) endTangent = -Vector3.ProjectOnPlane(tail.transform.forward, Vector3.up);
        if (endTangent.sqrMagnitude < 0.0001f) endTangent = -Vector3.forward;

        noHistoryExtensionDirection = endTangent.normalized;

        if (availableOldPathDistance > 0.001f && normalPath.TryGetPoseAtProgress(normalPath.OldestProgress, out Vector3 oldestPosition, out Quaternion oldestRotation))
        {
            oldestPathPosition = FlattenToRewindPlane(oldestPosition);

            Vector3 oldestForward = Vector3.ProjectOnPlane(oldestRotation * Vector3.forward, Vector3.up);
            if (oldestForward.sqrMagnitude < 0.0001f) oldestForward = -noHistoryExtensionDirection;

            oldestPathBackwardDirection = -oldestForward.normalized;
        }
        else
        {
            oldestPathPosition = noHistoryExtensionOrigin;
            oldestPathBackwardDirection = noHistoryExtensionDirection;
        }
    }

    private bool TryGetTailDestination(float desiredTailTravel, out Vector3 position)
    {
        position = Vector3.zero;

        if (desiredTailTravel <= availableOldPathDistance + 0.0001f)
        {
            float desiredProgress = Mathf.Max(normalPath.OldestProgress, tailMainStartProgress - desiredTailTravel);

            if (!normalPath.TryGetPositionAtProgress(desiredProgress, out position)) return false;

            position = FlattenToRewindPlane(position);
            return true;
        }

        if (!allowStraightExtensionWhenHistoryRunsOut) return false;

        float extensionDistance = desiredTailTravel - availableOldPathDistance;

        if (availableOldPathDistance > 0.001f) position = oldestPathPosition + oldestPathBackwardDirection * extensionDistance;
        else position = noHistoryExtensionOrigin + noHistoryExtensionDirection * desiredTailTravel;

        position = FlattenToRewindPlane(position);
        return true;
    }

    #endregion

    #region Chain Reverse Tick

    private void TickChainMoveBackward()
    {
        List<GameObject> snakeBody = snakeManager.GetSnakeBody();

        if (snakeBody == null || snakeBody.Count != expectedSnakeCount)
        {
            Debug.LogWarning("[MoveBackward] Snake count changed during reverse tow. Finishing recovery immediately.", this);
            FinishChainMoveBackward();
            return;
        }

        elapsedTime += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(elapsedTime / moveBackwardDuration);
        float curveValue = Mathf.Clamp01(moveBackwardMotionCurve.Evaluate(normalizedTime));
        float desiredMovedDistance = Mathf.Max(movedTailDistance, requestedMoveBackwardDistance * curveValue);

        if (desiredMovedDistance > movedTailDistance + 0.00001f)
        {
            if (!TryGetTailDestination(desiredMovedDistance, out Vector3 tailDestination))
            {
                FinishChainMoveBackward();
                return;
            }

            temporaryPath.Append(tailDestination);
            movedTailDistance = desiredMovedDistance;
            temporaryHeadProgress = temporaryPath.HeadProgress;
        }

        MoveFollowerChain(snakeBody);
        ApplyReverseTowToLeader(snakeBody[1]);

        if (normalizedTime >= 1f || movedTailDistance >= requestedMoveBackwardDistance - 0.001f) FinishChainMoveBackward();
    }

    private void MoveFollowerChain(List<GameObject> snakeBody)
    {
        float head = temporaryPath.HeadProgress;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            float cartProgress = head - followerOffsetsFromTemporaryHead[i];

            if (!temporaryPath.TryGetPose(cartProgress, tangentSampleDistance, out Vector3 cartPosition, out Vector3 cartTangent)) continue;

            cartPosition = FlattenToRewindPlane(cartPosition);

            Quaternion targetRotation = cart.transform.rotation;

            if (cartTangent.sqrMagnitude > 0.0001f)
            {
                // Temporary tangent points in reverse-travel direction, so carts
                // visually face the opposite direction while backing along it.
                Quaternion desiredRotation = Quaternion.LookRotation(-cartTangent, Vector3.up);
                float rotationT = 1f - Mathf.Exp(-followerRotationFollowSpeed * Time.fixedDeltaTime);
                targetRotation = Quaternion.Slerp(cart.transform.rotation, desiredRotation, rotationT);
            }

            cart.transform.SetPositionAndRotation(cartPosition, targetRotation);
        }
    }

    #endregion

    #region Reverse Tow

    /// <summary>
    /// C1 acts as a temporary tractor for the Leader. The connection has slack:
    /// C1 may retreat by Extra Slack before the maximum tow distance becomes taut.
    /// </summary>
    private void ApplyReverseTowToLeader(GameObject firstFollower)
    {
        if (firstFollower == null || leaderBody == null) return;

        Vector3 c1Position = FlattenToRewindPlane(firstFollower.transform.position);
        Vector3 currentLeaderPosition = FlattenToRewindPlane(leaderBody.position);

        Vector3 c1ToLeader = Vector3.ProjectOnPlane(currentLeaderPosition - c1Position, Vector3.up);
        currentLeaderToC1Distance = c1ToLeader.magnitude;

        float tautThreshold = activeReverseTowDistance + towActivationTolerance;

        if (currentLeaderToC1Distance <= tautThreshold)
        {
            reverseTowIsTaut = false;
            return;
        }

        reverseTowIsTaut = true;

        Vector3 towDirection = c1ToLeader.sqrMagnitude > 0.0001f ? c1ToLeader.normalized : Vector3.ProjectOnPlane(leaderBody.transform.forward, Vector3.up).normalized;

        if (towDirection.sqrMagnitude < 0.0001f) towDirection = Vector3.forward;

        Vector3 targetLeaderPosition = c1Position + towDirection * activeReverseTowDistance;
        targetLeaderPosition.y = rewindPlaneY;

        leaderBody.MovePosition(targetLeaderPosition);

        if (rotateLeaderFromReverseTow)
        {
            Vector3 desiredForward = Vector3.ProjectOnPlane(targetLeaderPosition - c1Position, Vector3.up);

            if (desiredForward.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
                float rotationT = 1f - Mathf.Exp(-leaderTowRotationFollowSpeed * Time.fixedDeltaTime);
                leaderBody.MoveRotation(Quaternion.Slerp(leaderBody.rotation, desiredRotation, rotationT));
            }
        }

        leaderMovedDistance = Vector3.ProjectOnPlane(targetLeaderPosition - leaderStartPosition, Vector3.up).magnitude;
    }

    #endregion

    #region Recovery

    private void FinishChainMoveBackward()
    {
        if (!isMovingBackward) return;

        // Build a new normal path from the exact final reverse-tow geometry while
        // the Leader is still kinematic, then hand movement back to normal physics.
        bool recoveryBuilt = snakeManager.CompleteMoveBackwardRecovery();

        leaderBody.isKinematic = leaderWasKinematic;

        if (!leaderBody.isKinematic)
        {
            leaderBody.linearVelocity = Vector3.zero;
            leaderBody.angularVelocity = Vector3.zero;
        }

        isMovingBackward = false;
        nextAllowedMoveBackwardTime = Time.time + moveBackwardCooldown;

        temporaryPath.Clear();
        OnMoveBackwardFinished?.Invoke();

        if (!recoveryBuilt) Debug.LogError("[MoveBackward] Recovery path failed to build.", this);

        if (debugMoveBackward)
        {
            Debug.Log($"[MoveBackward] FINISH | recoveryBuilt:{recoveryBuilt} | tailMoved:{movedTailDistance:F2} | leaderMoved:{leaderMovedDistance:F2}", this);
        }
    }

    #endregion

    #region Helpers / Cleanup

    private Vector3 FlattenToRewindPlane(Vector3 position)
    {
        position.y = rewindPlaneY;
        return position;
    }

    private void OnDestroy()
    {
        if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;
    }

    #endregion

    #region Debug Drawing

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (drawTemporaryPath && temporaryPath != null && temporaryPath.Count >= 2)
        {
            Gizmos.color = Color.magenta;

            for (int i = 1; i < temporaryPath.Count; i++) Gizmos.DrawLine(temporaryPath.GetPosition(i - 1), temporaryPath.GetPosition(i));
        }

        if (drawReverseTow && snakeManager != null)
        {
            List<GameObject> snakeBody = snakeManager.GetSnakeBody();

            if (snakeBody != null && snakeBody.Count >= 2 && snakeBody[1] != null && leaderBody != null)
            {
                Gizmos.color = reverseTowIsTaut ? Color.green : Color.yellow;
                Gizmos.DrawLine(snakeBody[1].transform.position, leaderBody.position);
            }
        }
    }

    #endregion

    #region Temporary Path Type

    private class TemporaryPath
    {
        private struct Point
        {
            public Vector3 position;
            public float distance;

            public Point(Vector3 position, float distance)
            {
                this.position = position;
                this.distance = distance;
            }
        }

        private readonly List<Point> points = new List<Point>(256);

        public int Count => points.Count;
        public float HeadProgress => points.Count > 0 ? points[points.Count - 1].distance : 0f;

        public void Clear()
        {
            points.Clear();
        }

        public void Reset(Vector3 position)
        {
            points.Clear();
            points.Add(new Point(position, 0f));
        }

        public float Append(Vector3 position)
        {
            if (points.Count == 0)
            {
                Reset(position);
                return 0f;
            }

            Point lastPoint = points[points.Count - 1];
            Vector3 delta = Vector3.ProjectOnPlane(position - lastPoint.position, Vector3.up);
            float distance = delta.magnitude;

            if (distance <= 0.00001f) return lastPoint.distance;

            float newDistance = lastPoint.distance + distance;
            points.Add(new Point(position, newDistance));
            return newDistance;
        }

        public Vector3 GetEndTangent()
        {
            if (points.Count < 2) return Vector3.zero;

            Vector3 tangent = Vector3.ProjectOnPlane(points[points.Count - 1].position - points[points.Count - 2].position, Vector3.up);
            if (tangent.sqrMagnitude > 0.0001f) tangent.Normalize();

            return tangent;
        }

        public bool TryGetPose(float progress, float tangentDistance, out Vector3 position, out Vector3 tangent)
        {
            position = Vector3.zero;
            tangent = Vector3.zero;

            if (!TryGetPosition(progress, out position)) return false;

            float sampleDistance = Mathf.Max(0.01f, tangentDistance);

            TryGetPosition(progress - sampleDistance, out Vector3 before);
            TryGetPosition(progress + sampleDistance, out Vector3 after);

            tangent = Vector3.ProjectOnPlane(after - before, Vector3.up);

            if (tangent.sqrMagnitude < 0.0001f && points.Count >= 2)
            {
                tangent = Vector3.ProjectOnPlane(points[points.Count - 1].position - points[points.Count - 2].position, Vector3.up);
            }

            if (tangent.sqrMagnitude > 0.0001f) tangent.Normalize();
            return true;
        }

        public bool TryGetPosition(float progress, out Vector3 position)
        {
            position = Vector3.zero;

            if (points.Count == 0) return false;

            if (points.Count == 1)
            {
                position = points[0].position;
                return true;
            }

            progress = Mathf.Clamp(progress, points[0].distance, HeadProgress);

            int low = 0;
            int high = points.Count - 1;

            while (low < high)
            {
                int mid = (low + high) / 2;

                if (points[mid].distance < progress) low = mid + 1;
                else high = mid;
            }

            int upperIndex = low;

            if (upperIndex == 0)
            {
                position = points[0].position;
                return true;
            }

            Point lower = points[upperIndex - 1];
            Point upper = points[upperIndex];

            float range = upper.distance - lower.distance;
            float t = range > 0.00001f ? (progress - lower.distance) / range : 0f;

            position = Vector3.Lerp(lower.position, upper.position, t);
            return true;
        }

        public Vector3 GetPosition(int index)
        {
            return index >= 0 && index < points.Count ? points[index].position : Vector3.zero;
        }
    }

    #endregion
}
