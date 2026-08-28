using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tail-led MoveBackward recovery.
///
/// Normal:
/// Leader -> raw physical Probe -> C1 -> C2 -> ... -> Tail
///
/// MoveBackward:
/// - Stall freezes the ACTUAL physical probe in world space so wall-pivot hinge motion is not recorded.
/// - The current snake shape is copied into a temporary path.
/// - The tail becomes the temporary head.
/// - With zero chained carts, the frozen physical Probe itself becomes the temporary head.
/// - The temporary head travels backward along the OLD normal path.
/// - Every member advances through the same temporary path by the same distance.
/// - The frozen physical hinge is ignored during the action; a virtual probe follows the temporary path.
/// - At the end the physical hinge is rebuilt and SnakePathHistory is rebound to the new raw probe.
/// </summary>
public class SnakeMoveBackwardController : MonoBehaviour
{
    //#region Settings

    //[Header("Move Backward")]

    //[Tooltip("How far the temporary tail/head travels backward along the old normal path.")]
    //[Min(0.1f)]
    //[SerializeField] private float moveBackwardDistance = 3f;

    //[Tooltip("Total duration of the MoveBackward action.")]
    //[Min(0.05f)]
    //[SerializeField] private float moveBackwardDuration = 0.6f;

    //[Tooltip("Maps normalized time 0-1 to normalized travelled distance 0-1.")]
    //[SerializeField] private AnimationCurve moveBackwardMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    //[Tooltip("Minimum delay before another MoveBackward can begin.")]
    //[Min(0f)]
    //[SerializeField] private float moveBackwardCooldown = 1.25f;

    //[Header("Temporary Path")]

    //[Tooltip("Sampling distance when the tail extends the temporary path through the old normal path.")]
    //[Min(0.01f)]
    //[SerializeField] private float temporaryPathSampleSpacing = 0.1f;

    //[Tooltip("Distance used to calculate temporary-path tangents.")]
    //[Min(0.01f)]
    //[SerializeField] private float tangentSampleDistance = 0.2f;

    //[Header("Leader Rotation")]

    //[Tooltip("If enabled, the leading cart gradually aligns with the temporary chain path while backing out.")]
    //[SerializeField] private bool rotateLeaderAlongTemporaryPath = true;

    //[Tooltip("How quickly leader rotation follows the temporary path.")]
    //[Min(0f)]
    //[SerializeField] private float leaderRotationFollowSpeed = 10f;

    //#endregion

    //#region Debug

    //[Header("Debug")]

    //[SerializeField] private bool drawTemporaryPath = true;
    //[SerializeField] private bool debugMoveBackward = false;

    //[Header("Runtime - Read Only")]

    //[SerializeField] private bool isInitialized;
    //[SerializeField] private bool isMovingBackward;
    //[SerializeField] private bool stallProbeFrozen;
    //[SerializeField] private bool usingProbeAsTail;
    //[SerializeField] private float actualMoveBackwardDistance;
    //[SerializeField] private float movedDistance;
    //[SerializeField] private float tailMainPathProgress;
    //[SerializeField] private float temporaryHeadProgress;
    //[SerializeField] private Vector3 virtualProbePosition;

    //#endregion

    //#region References

    //private SnakeCartManager snakeManager;
    //private SnakePathHistory normalPath;
    //private PhysicalChainJointProbe physicalProbe;

    //private Rigidbody leaderBody;
    //private LeadingCartBehaviour[] leadingMovements;
    //private CartControlScript cartControl;

    //#endregion

    //#region Runtime

    //private TemporaryPath temporaryPath;

    //private float[] bodyOffsetsFromTemporaryHead;
    //private float probeOffsetFromTemporaryHead;

    //private float tailMainStartProgress;
    //private float elapsedTime;
    //private float nextAllowedMoveBackwardTime;

    //private int expectedSnakeCount;

    //private bool leaderWasKinematic;
    //private bool finalizeNextFixedUpdate;

    //#endregion

    //#region Public API

    //public bool IsMovingBackward => isMovingBackward;

    //public System.Action OnMoveBackwardStarted;
    //public System.Action OnMoveBackwardFinished;

    //public void Initialize(SnakeCartManager manager, Rigidbody body, LeadingCartBehaviour[] movements, CartControlScript control, SnakePathHistory pathHistory, PhysicalChainJointProbe probe)
    //{
    //    if (manager == null || body == null || movements == null || movements.Length == 0 || control == null || pathHistory == null || probe == null)
    //    {
    //        Debug.LogError("[SnakeMoveBackwardController] Missing initialization reference.", this);
    //        return;
    //    }

    //    if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;

    //    snakeManager = manager;
    //    leaderBody = body;
    //    leadingMovements = movements;
    //    cartControl = control;
    //    normalPath = pathHistory;
    //    physicalProbe = probe;

    //    cartControl.OnMoveBackwardPressed += TryBeginMoveBackward;

    //    temporaryPath = new TemporaryPath();

    //    isInitialized = true;
    //}

    ///// <summary>
    ///// Called once per FixedUpdate by SnakeCartManager.
    /////
    ///// This method also synchronizes the raw physical-probe freeze with
    ///// the existing canMoveBackward stalled-state permission.
    ///// </summary>
    //public bool TickMoveBackward()
    //{
    //    if (!isInitialized) return false;

    //    SyncStallProbeFreezeState();

    //    if (!isMovingBackward) return false;

    //    if (finalizeNextFixedUpdate)
    //    {
    //        FinishMoveBackward();
    //        return true;
    //    }

    //    TickActiveMoveBackward();
    //    return true;
    //}

    //#endregion

    //#region Stall Freeze

    ///// <summary>
    ///// No new stall-detector reference is required.
    /////
    ///// Existing stall logic already calls:
    /////     AllowMoveBackward()
    /////     DisallowMoveBackward()
    /////
    ///// We use that same permission state to freeze/unfreeze raw Probe.
    ///// </summary>
    //private void SyncStallProbeFreezeState()
    //{
    //    if (isMovingBackward || cartControl == null || physicalProbe == null || normalPath == null) return;

    //    bool shouldFreeze = cartControl.GetCanMoveBackward();

    //    if (shouldFreeze && !stallProbeFrozen)
    //    {
    //        physicalProbe.FreezeProbeInWorld();
    //        stallProbeFrozen = true;

    //        if (debugMoveBackward) Debug.Log("[MoveBackward] Stall entered. Raw physical probe frozen in world space.");
    //    }
    //    else if (!shouldFreeze && stallProbeFrozen)
    //    {
    //        Transform resumedProbe = physicalProbe.ResumeProbeFromCurrentHitch();

    //        if (resumedProbe != null)
    //        {
    //            normalPath.RebindPathSourceWithoutReset(resumedProbe);
    //            normalPath.ReanchorEndToCurrentSourceWithoutProgress();
    //        }

    //        stallProbeFrozen = false;

    //        if (debugMoveBackward) Debug.Log("[MoveBackward] Stall cleared. Raw physical hinge rebuilt and resumed.");
    //    }
    //}

    //#endregion

    //#region Start

    //private void TryBeginMoveBackward()
    //{
    //    if (!isInitialized || isMovingBackward) return;
    //    if (Time.time < nextAllowedMoveBackwardTime) return;
    //    if (normalPath == null || !normalPath.IsInitialized) return;

    //    List<GameObject> snakeBody = snakeManager.GetSnakeBody();

    //    if (snakeBody == null || snakeBody.Count == 0) return;

    //    expectedSnakeCount = snakeBody.Count;
    //    usingProbeAsTail = snakeBody.Count == 1;

    //    // Guarantee the source is frozen at the exact activation moment even if
    //    // the stalled permission was enabled between physics ticks.
    //    physicalProbe.FreezeProbeInWorld();
    //    stallProbeFrozen = true;

    //    if (usingProbeAsTail)
    //    {
    //        // No chained carts:
    //        // the frozen raw physical Probe itself becomes our temporary tail.
    //        tailMainStartProgress = normalPath.HeadProgress;
    //    }
    //    else
    //    {
    //        int tailIndex = snakeBody.Count - 1;
    //        tailMainStartProgress = snakeManager.GetDistancePathProgressForSnakeIndex(tailIndex);
    //    }

    //    tailMainStartProgress = Mathf.Clamp(tailMainStartProgress, normalPath.OldestProgress, normalPath.HeadProgress);

    //    float availableDistanceBehindTail = tailMainStartProgress - normalPath.OldestProgress;

    //    if (availableDistanceBehindTail <= 0.05f)
    //    {
    //        if (debugMoveBackward) Debug.Log("[MoveBackward] Not enough old path behind the temporary tail.");
    //        return;
    //    }

    //    actualMoveBackwardDistance = Mathf.Min(moveBackwardDistance, availableDistanceBehindTail);

    //    if (!BuildTemporaryPathFromCurrentSnake(snakeBody))
    //    {
    //        Debug.LogWarning("[MoveBackward] Failed to build temporary path.", this);
    //        return;
    //    }

    //    movedDistance = 0f;
    //    elapsedTime = 0f;
    //    tailMainPathProgress = tailMainStartProgress;
    //    finalizeNextFixedUpdate = false;

    //    StopLeadingMovement();

    //    leaderBody.linearVelocity = Vector3.zero;
    //    leaderBody.angularVelocity = Vector3.zero;

    //    leaderWasKinematic = leaderBody.isKinematic;
    //    leaderBody.isKinematic = true;

    //    normalPath.BeginMoveBackward();

    //    // The raw physical probe remains frozen and is ignored during MoveBackward.
    //    // virtualProbePosition is driven by the temporary path instead.
    //    isMovingBackward = true;

    //    OnMoveBackwardStarted?.Invoke();

    //    if (debugMoveBackward)
    //    {
    //        Debug.Log($"[MoveBackward] START | snakeCount:{snakeBody.Count} | probeAsTail:{usingProbeAsTail} | tailProgress:{tailMainStartProgress:F2} | distance:{actualMoveBackwardDistance:F2}");
    //    }
    //}

    ///// <summary>
    ///// Seeds the temporary path from CURRENT world positions:
    /////
    ///// Leader -> Probe -> C1 -> C2 -> ... -> Tail
    /////
    ///// With zero chained carts:
    /////
    ///// Leader -> Probe
    /////
    ///// Because every target is derived from this exact current shape,
    ///// MoveBackward has no activation-frame teleport.
    ///// </summary>
    //private bool BuildTemporaryPathFromCurrentSnake(List<GameObject> snakeBody)
    //{
    //    if (physicalProbe.ProbeTransform == null) return false;

    //    temporaryPath.Clear();

    //    bodyOffsetsFromTemporaryHead = new float[snakeBody.Count];
    //    float[] initialBodyProgress = new float[snakeBody.Count];

    //    temporaryPath.Reset(leaderBody.position);
    //    initialBodyProgress[0] = 0f;

    //    float initialProbeProgress = temporaryPath.Append(physicalProbe.ProbePosition);

    //    for (int i = 1; i < snakeBody.Count; i++)
    //    {
    //        if (snakeBody[i] == null) return false;

    //        initialBodyProgress[i] = temporaryPath.Append(snakeBody[i].transform.position);
    //    }

    //    float initialTemporaryHead = temporaryPath.HeadProgress;

    //    for (int i = 0; i < snakeBody.Count; i++)
    //    {
    //        bodyOffsetsFromTemporaryHead[i] = initialTemporaryHead - initialBodyProgress[i];
    //    }

    //    probeOffsetFromTemporaryHead = initialTemporaryHead - initialProbeProgress;
    //    temporaryHeadProgress = initialTemporaryHead;
    //    virtualProbePosition = physicalProbe.ProbePosition;

    //    return initialTemporaryHead > 0.01f;
    //}

    //#endregion

    //#region Active MoveBackward

    //private void TickActiveMoveBackward()
    //{
    //    List<GameObject> snakeBody = snakeManager.GetSnakeBody();

    //    if (snakeBody == null || snakeBody.Count != expectedSnakeCount)
    //    {
    //        Debug.LogWarning("[MoveBackward] Snake count changed during MoveBackward. Finishing recovery early.", this);
    //        finalizeNextFixedUpdate = true;
    //        return;
    //    }

    //    elapsedTime += Time.fixedDeltaTime;

    //    float normalizedTime = Mathf.Clamp01(elapsedTime / moveBackwardDuration);
    //    float curveValue = Mathf.Clamp01(moveBackwardMotionCurve.Evaluate(normalizedTime));

    //    // Prevent a badly authored curve from ever moving the temporary head forward again.
    //    float desiredMovedDistance = Mathf.Max(movedDistance, actualMoveBackwardDistance * curveValue);
    //    float desiredTailProgress = Mathf.Max(normalPath.OldestProgress, tailMainStartProgress - desiredMovedDistance);

    //    float actualMovementThisFrame = Mathf.Max(0f, tailMainPathProgress - desiredTailProgress);

    //    if (actualMovementThisFrame > 0.00001f)
    //    {
    //        AppendOldPathSectionToTemporaryPath(tailMainPathProgress, desiredTailProgress);

    //        normalPath.MoveHeadBackwardBy(actualMovementThisFrame);

    //        movedDistance += actualMovementThisFrame;
    //        tailMainPathProgress = desiredTailProgress;
    //        temporaryHeadProgress = temporaryPath.HeadProgress;
    //    }

    //    MoveEntireSnakeAlongTemporaryPath(snakeBody);

    //    if (normalizedTime >= 1f || movedDistance >= actualMoveBackwardDistance - 0.001f)
    //    {
    //        finalizeNextFixedUpdate = true;
    //    }
    //}

    ///// <summary>
    ///// The temporary tail follows the OLD normal path.
    /////
    ///// We sample the section instead of appending one long chord so tight turns
    ///// remain curved even when MoveBackward is intentionally fast.
    ///// </summary>
    //private void AppendOldPathSectionToTemporaryPath(float fromProgress, float toProgress)
    //{
    //    float spacing = Mathf.Max(0.01f, temporaryPathSampleSpacing);
    //    float sampleProgress = fromProgress;

    //    while (sampleProgress - spacing > toProgress)
    //    {
    //        sampleProgress -= spacing;

    //        if (normalPath.TryGetPositionAtProgress(sampleProgress, out Vector3 samplePosition))
    //        {
    //            temporaryPath.Append(samplePosition);
    //        }
    //    }

    //    if (normalPath.TryGetPositionAtProgress(toProgress, out Vector3 finalPosition))
    //    {
    //        temporaryPath.Append(finalPosition);
    //    }
    //}

    //private void MoveEntireSnakeAlongTemporaryPath(List<GameObject> snakeBody)
    //{
    //    float head = temporaryPath.HeadProgress;

    //    // -------------------------------------------------
    //    // VIRTUAL PROBE
    //    //
    //    // The REAL physical probe stays frozen during MoveBackward.
    //    // This virtual position is only part of the temporary tail-led path.
    //    // -------------------------------------------------

    //    float probeProgress = head - probeOffsetFromTemporaryHead;

    //    if (temporaryPath.TryGetPose(probeProgress, tangentSampleDistance, out Vector3 probePosition, out Vector3 ignoredProbeTangent))
    //    {
    //        virtualProbePosition = probePosition;
    //    }

    //    // -------------------------------------------------
    //    // LEADER
    //    // -------------------------------------------------

    //    float leaderProgress = head - bodyOffsetsFromTemporaryHead[0];

    //    if (temporaryPath.TryGetPose(leaderProgress, tangentSampleDistance, out Vector3 leaderPosition, out Vector3 leaderTangent))
    //    {
    //        leaderBody.MovePosition(leaderPosition);

    //        if (rotateLeaderAlongTemporaryPath && leaderTangent.sqrMagnitude > 0.0001f)
    //        {
    //            Quaternion targetRotation = Quaternion.LookRotation(-leaderTangent, Vector3.up);
    //            float rotationT = 1f - Mathf.Exp(-leaderRotationFollowSpeed * Time.fixedDeltaTime);
    //            leaderBody.MoveRotation(Quaternion.Slerp(leaderBody.rotation, targetRotation, rotationT));
    //        }
    //    }

    //    // -------------------------------------------------
    //    // CHAINED CARTS
    //    // -------------------------------------------------

    //    for (int i = 1; i < snakeBody.Count; i++)
    //    {
    //        GameObject cart = snakeBody[i];

    //        if (cart == null) continue;

    //        float cartProgress = head - bodyOffsetsFromTemporaryHead[i];

    //        if (!temporaryPath.TryGetPose(cartProgress, tangentSampleDistance, out Vector3 cartPosition, out Vector3 cartTangent))
    //        {
    //            continue;
    //        }

    //        Quaternion cartRotation = cart.transform.rotation;

    //        if (cartTangent.sqrMagnitude > 0.0001f)
    //        {
    //            // Path tangent is the backward travel direction.
    //            // Cart faces opposite it because it is backing up.
    //            cartRotation = Quaternion.LookRotation(-cartTangent, Vector3.up);
    //        }

    //        cart.transform.SetPositionAndRotation(cartPosition, cartRotation);
    //    }
    //}

    //#endregion

    //#region Finish

    //private void FinishMoveBackward()
    //{
    //    // Shared normal cursor already moved backward by movedDistance.
    //    // Delete the abandoned dead-end branch.
    //    normalPath.EndMoveBackwardAndTruncate();

    //    // Rebuild the ACTUAL physical hinge from the leader's new pose.
    //    // virtualProbePosition gives it the desired trailing direction.
    //    Transform rebuiltProbe = physicalProbe.ResetSimulationToCurrentHitch(virtualProbePosition);

    //    if (rebuiltProbe != null)
    //    {
    //        normalPath.RebindPathSourceWithoutReset(rebuiltProbe);
    //        normalPath.ReanchorEndToCurrentSourceWithoutProgress();
    //    }

    //    leaderBody.isKinematic = leaderWasKinematic;

    //    if (!leaderBody.isKinematic)
    //    {
    //        leaderBody.linearVelocity = Vector3.zero;
    //        leaderBody.angularVelocity = Vector3.zero;
    //    }

    //    ResetLeadingMovement();

    //    isMovingBackward = false;
    //    finalizeNextFixedUpdate = false;
    //    stallProbeFrozen = false;

    //    nextAllowedMoveBackwardTime = Time.time + moveBackwardCooldown;

    //    OnMoveBackwardFinished?.Invoke();

    //    if (debugMoveBackward)
    //    {
    //        Debug.Log($"[MoveBackward] FINISH | moved:{movedDistance:F2} | newNormalHead:{normalPath.HeadProgress:F2}");
    //    }

    //    temporaryPath.Clear();
    //}

    //#endregion

    //#region Leading Movement

    //private void StopLeadingMovement()
    //{
    //    if (leadingMovements == null) return;

    //    for (int i = 0; i < leadingMovements.Length; i++)
    //    {
    //        if (leadingMovements[i] != null) leadingMovements[i].SetSpeedToZero();
    //    }
    //}

    //private void ResetLeadingMovement()
    //{
    //    if (leadingMovements == null) return;

    //    for (int i = 0; i < leadingMovements.Length; i++)
    //    {
    //        if (leadingMovements[i] != null) leadingMovements[i].ResetSpeed();
    //    }
    //}

    //#endregion

    //#region Cleanup / Debug

    //private void OnDestroy()
    //{
    //    if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;
    //}

    //private void OnDrawGizmos()
    //{
    //    if (!drawTemporaryPath || !Application.isPlaying || temporaryPath == null || temporaryPath.Count < 2) return;

    //    Gizmos.color = Color.magenta;

    //    for (int i = 1; i < temporaryPath.Count; i++)
    //    {
    //        Gizmos.DrawLine(temporaryPath.GetPosition(i - 1), temporaryPath.GetPosition(i));
    //    }

    //    Gizmos.color = Color.white;
    //    Gizmos.DrawSphere(virtualProbePosition, 0.15f);
    //}

    //#endregion

    //#region Temporary Path

    //private class TemporaryPath
    //{
    //    private struct Point
    //    {
    //        public Vector3 position;
    //        public float distance;

    //        public Point(Vector3 position, float distance)
    //        {
    //            this.position = position;
    //            this.distance = distance;
    //        }
    //    }

    //    private readonly List<Point> points = new List<Point>(256);

    //    public int Count => points.Count;
    //    public float HeadProgress => points.Count > 0 ? points[points.Count - 1].distance : 0f;

    //    public void Clear()
    //    {
    //        points.Clear();
    //    }

    //    public void Reset(Vector3 position)
    //    {
    //        points.Clear();
    //        points.Add(new Point(position, 0f));
    //    }

    //    public float Append(Vector3 position)
    //    {
    //        if (points.Count == 0)
    //        {
    //            Reset(position);
    //            return 0f;
    //        }

    //        Point lastPoint = points[points.Count - 1];
    //        Vector3 delta = Vector3.ProjectOnPlane(position - lastPoint.position, Vector3.up);
    //        float distance = delta.magnitude;

    //        if (distance <= 0.00001f) return lastPoint.distance;

    //        float newDistance = lastPoint.distance + distance;

    //        points.Add(new Point(position, newDistance));

    //        return newDistance;
    //    }

    //    public bool TryGetPose(float progress, float tangentDistance, out Vector3 position, out Vector3 tangent)
    //    {
    //        position = Vector3.zero;
    //        tangent = Vector3.zero;

    //        if (!TryGetPosition(progress, out position)) return false;

    //        float sampleDistance = Mathf.Max(0.01f, tangentDistance);

    //        TryGetPosition(progress - sampleDistance, out Vector3 before);
    //        TryGetPosition(progress + sampleDistance, out Vector3 after);

    //        tangent = Vector3.ProjectOnPlane(after - before, Vector3.up);

    //        if (tangent.sqrMagnitude < 0.0001f && points.Count >= 2)
    //        {
    //            tangent = Vector3.ProjectOnPlane(points[points.Count - 1].position - points[points.Count - 2].position, Vector3.up);
    //        }

    //        if (tangent.sqrMagnitude > 0.0001f) tangent.Normalize();

    //        return true;
    //    }

    //    public bool TryGetPosition(float progress, out Vector3 position)
    //    {
    //        position = Vector3.zero;

    //        if (points.Count == 0) return false;

    //        if (points.Count == 1)
    //        {
    //            position = points[0].position;
    //            return true;
    //        }

    //        progress = Mathf.Clamp(progress, points[0].distance, points[points.Count - 1].distance);

    //        int low = 0;
    //        int high = points.Count - 1;

    //        while (low < high)
    //        {
    //            int mid = (low + high) / 2;

    //            if (points[mid].distance < progress) low = mid + 1;
    //            else high = mid;
    //        }

    //        int upperIndex = low;

    //        if (upperIndex == 0)
    //        {
    //            position = points[0].position;
    //            return true;
    //        }

    //        Point lower = points[upperIndex - 1];
    //        Point upper = points[upperIndex];

    //        float range = upper.distance - lower.distance;
    //        float t = range > 0.00001f ? (progress - lower.distance) / range : 0f;

    //        position = Vector3.Lerp(lower.position, upper.position, t);

    //        return true;
    //    }

    //    public Vector3 GetPosition(int index)
    //    {
    //        if (index < 0 || index >= points.Count) return Vector3.zero;

    //        return points[index].position;
    //    }
    //}

    //#endregion
}
