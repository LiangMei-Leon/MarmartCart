using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Owns the player's cart chain, distance-path following, cart creation/removal,
/// grocery bookkeeping, stall vulnerability, and MoveBackward handoff.
/// </summary>
public class SnakeCartManager : MonoBehaviour, IAssistPlayerDataSource
{
    #region Distance Path

    [Header("Distance Path")]
    [SerializeField] private SnakePathHistory pathHistory;

    [Tooltip("Logical path distance from the physical probe to Cart 1.")]
    [Min(0.1f)]
    [SerializeField] private float firstFollowerSpacing = 1f;

    [Tooltip("Logical center-to-center path distance between later follower carts.")]
    [Min(0.1f)]
    [SerializeField] private float followerCartSpacing = 1.5f;

    [Tooltip("Extra path history kept behind the current tail requirement. Prevents long chains from reaching the oldest stored path point.")]
    [Min(0f)]
    [SerializeField] private float pathHistorySafetyBuffer = 3f;

    [Header("Follower Rotation")]
    [Range(0f, 1f)]
    [SerializeField] private float firstFollowerHingeInfluence = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float hingeInfluenceFalloffPerCart = 0.2f;

    [Tooltip("How quickly live hinge influence fades back in after MoveBackward recovery.")]
    [Min(0f)]
    [SerializeField] private float recoveryHingeRestoreSpeed = 5f;

    #endregion

    #region MoveBackward / Physical Probe

    [Header("Move Backward")]
    [SerializeField] private SnakeMoveBackwardController moveBackwardController;

    [Header("Physical Joint")]
    [SerializeField] private PhysicalChainJointProbe physicalJointProbe;

    #endregion

    #region Stall Vulnerability

    [Header("Stall Vulnerability")]
    [Tooltip("Number of followers nearest the Leader that become Vulnerable when the Leader enters Stall.")]
    [Min(0)]
    [SerializeField] private int vulnerableFollowerCount = 2;

    [Header("Vulnerability Runtime - Read Only")]
    [SerializeField] private bool vulnerabilitySessionActive;
    [SerializeField] private int currentVulnerableFollowerCount;

    private LeadingCartStallController leadingStallController;

    #endregion

    #region Cart Creation / Chain State

    [Header("Cart Creation")]
    [FormerlySerializedAs("distanceBetween")]
    [Tooltip("Delay before a queued follower cart is spawned into the chain.")]
    [Min(0f)]
    [SerializeField] private float followerSpawnDelay = 0.2f;

    [Tooltip("Delay after a follower actually spawns before its collect VFX plays.")]
    [Min(0f)]
    [SerializeField] private float collectVfxDelay = 0.12f;

    [Header("Pending Follower Join")]
    [Tooltip("Temporary compact spacing used when a newly collected cart first appears behind the current tail.")]
    [Min(0.01f)]
    [SerializeField] private float pendingCompactSpacing = 0.3f;

    [Tooltip("Small tolerance added when checking whether enough path distance has opened for the next pending cart to become a normal follower.")]
    [Min(0f)]
    [SerializeField] private float pendingPromotionDistanceTolerance = 0.05f;

    [SerializeField] private List<GameObject> bodyParts = new List<GameObject>();
    [SerializeField] private List<GameObject> snakeBody = new List<GameObject>();

    [FormerlySerializedAs("cartsWithOutItem")]
    [SerializeField] private List<GameObject> cartsWithoutItem = new List<GameObject>();

    [Header("Follower Scale")]
    [SerializeField] private Vector3 normalScale = new Vector3(6f, 6f, 6f);
    [SerializeField] private Vector3 upScale = new Vector3(10f, 10f, 10f);

    [Tooltip("Reserved for future chain shrink/down-scale behavior.")]
    [SerializeField] private Vector3 downScale = new Vector3(3f, 3f, 3f);

    public bool needScaleup = false;

    #endregion

    #region Player / Services

    [Header("Player")]
    [Range(1, 4)]
    [SerializeField] private int playerIndex = 1;

    [Header("Related Events")]
    [SerializeField] private GameEvent setupCamera;

    [Header("Gameplay Services")]
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private SfxManager sfxManager;

    #endregion

    #region Runtime

    private class PendingFollower
    {
        public GameObject cart;
        public ChainedCartManager manager;
        public Rigidbody body;
        public Vector3 heldPosition;
        public Quaternion heldRotation;
        public float heldPathProgress;
        public bool hasValidHeldPathProgress;
    }

    private readonly Dictionary<GameObject, ChainedCartManager> cartManagerCache = new Dictionary<GameObject, ChainedCartManager>();
    private readonly List<Vector3> recoveryFollowerPositions = new List<Vector3>(16);
    private readonly List<PendingFollower> pendingFollowers = new List<PendingFollower>(8);

    private Rigidbody leadingCartBody;
    private Transform leadingRearHitch;

    private float followerSpawnTimer;
    private float recoveryHingeBlend = 1f;

    [Header("Path Coverage Runtime - Read Only")]
    [SerializeField] private float requiredPathHistoryDistance;

    [Header("Pending Followers Runtime - Read Only")]
    [SerializeField] private int pendingFollowerCount;
    [SerializeField] private float pendingPromotionStartHeadProgress;
    [SerializeField] private float pendingRequiredForwardTravel;
    [SerializeField] private float pendingForwardTravelSinceBaseline;
    [SerializeField] private bool pendingFollowersDockedToTail;
    [SerializeField] private bool pendingPromotionPausedByStall;

    private float pendingStallPauseHeadProgress;

    private bool followerScaleDirty = true;
    private bool lastNeedScaleup;

    [FormerlySerializedAs("numOfCartsWithGroceryItem")]
    [SerializeField] private int groceryItemCartCount;

    #endregion

    #region Debug

    [Header("Distance Path Debug")]
    [SerializeField] private bool drawFirstFollowerTarget = true;

    private bool hasFirstFollowerDebugTarget;
    private Vector3 firstFollowerDebugTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (pathHistory == null) pathHistory = GetComponent<SnakePathHistory>();
        if (pathHistory == null) pathHistory = gameObject.AddComponent<SnakePathHistory>();

        if (physicalJointProbe == null) physicalJointProbe = GetComponent<PhysicalChainJointProbe>();

        if (moveBackwardController == null) moveBackwardController = GetComponent<SnakeMoveBackwardController>();
        if (moveBackwardController == null) moveBackwardController = gameObject.AddComponent<SnakeMoveBackwardController>();

        if (physicalJointProbe == null) Debug.LogError("[SnakeCartManager] PhysicalChainJointProbe is missing.", this);

        if (moveBackwardController != null)
        {
            moveBackwardController.OnMoveBackwardFinished -= HandleMoveBackwardFinished;
            moveBackwardController.OnMoveBackwardFinished += HandleMoveBackwardFinished;
        }

        lastNeedScaleup = needScaleup;
        CacheExistingCartManagers();
    }

    private void Start()
    {
        CreateBodyParts();
    }

    private void FixedUpdate()
    {
        UpdateRequiredPathHistory();
        ManageSnakeBody();
        ManagePendingFollowerOwnership();

        bool moveBackwardTicked = moveBackwardController != null && moveBackwardController.TickMoveBackward();
        bool isMovingBackward = moveBackwardController != null && moveBackwardController.IsMovingBackward;

        if (isMovingBackward)
        {
            DockPendingFollowersToCurrentTail();
        }

        UpdateFollowerScaleIfNeeded();

        bool moveBackwardOwnsSnake = moveBackwardTicked && snakeBody.Count > 1;
        if (moveBackwardOwnsSnake) return;

        UpdateRecoveryHingeBlend();

        if (pathHistory != null && pathHistory.IsInitialized) pathHistory.TickHistory();

        if (!isMovingBackward)
        {
            UpdatePendingFollowerPromotion();
        }

        MoveAllFollowersUsingDistancePath();
    }

    private void OnDestroy()
    {
        if (moveBackwardController != null) moveBackwardController.OnMoveBackwardFinished -= HandleMoveBackwardFinished;

        if (leadingStallController != null)
        {
            leadingStallController.OnStallStarted -= HandleLeaderStalled;
            leadingStallController.OnStallEnded -= HandleLeaderStallEnded;
        }
    }

    #endregion

    #region Dynamic Path Coverage

    /// <summary>
    /// Keeps enough path history for the current chain plus any followers that
    /// have already been collected and are waiting in bodyParts.
    ///
    /// This removes the old practical chain-length limit where followers beyond
    /// the oldest stored path sample all clamped onto the same position.
    /// </summary>
    private void UpdateRequiredPathHistory()
    {
        if (pathHistory == null || !pathHistory.IsInitialized) return;

        int totalCartCountToSupport = snakeBody.Count + pendingFollowers.Count + bodyParts.Count;

        if (totalCartCountToSupport <= 1)
        {
            requiredPathHistoryDistance = pathHistorySafetyBuffer;
            pathHistory.SetRequiredHistoryDistance(requiredPathHistoryDistance);
            return;
        }

        int followerCount = totalCartCountToSupport - 1;

        float tailDistanceBehindProbe =
            firstFollowerSpacing +
            Mathf.Max(0, followerCount - 1) * followerCartSpacing;

        requiredPathHistoryDistance = tailDistanceBehindProbe + pathHistorySafetyBuffer;
        pathHistory.SetRequiredHistoryDistance(requiredPathHistoryDistance);
    }

    #endregion

    #region Normal Distance-Path Movement

    private void MoveAllFollowersUsingDistancePath()
    {
        hasFirstFollowerDebugTarget = false;

        if (pathHistory == null || !pathHistory.IsInitialized) return;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;
            if (!TryGetDistancePathTarget(i, out Vector3 targetPosition, out Quaternion targetRotation)) continue;

            cart.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }
    }

    private bool TryGetDistancePathTarget(int snakeIndex, out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = Vector3.zero;
        targetRotation = Quaternion.identity;

        if (snakeIndex <= 0 || pathHistory == null || !pathHistory.IsInitialized) return false;

        int followerIndex = snakeIndex - 1;
        float distanceBehindProbe = firstFollowerSpacing + followerIndex * followerCartSpacing;
        float targetProgress = pathHistory.HeadProgress - distanceBehindProbe;

        if (!pathHistory.TryGetPoseAtProgress(targetProgress, out targetPosition, out Quaternion pathRotation)) return false;

        if (leadingCartBody != null) targetPosition.y = leadingCartBody.position.y;

        targetRotation = pathRotation;

        if (physicalJointProbe != null && physicalJointProbe.ProbeTransform != null)
        {
            Vector3 hingeForward = Vector3.ProjectOnPlane(physicalJointProbe.ProbeTransform.forward, Vector3.up);

            if (hingeForward.sqrMagnitude > 0.0001f)
            {
                Quaternion hingeRotation = Quaternion.LookRotation(hingeForward.normalized, Vector3.up);
                float hingeInfluence = Mathf.Clamp01(firstFollowerHingeInfluence - followerIndex * hingeInfluenceFalloffPerCart);
                targetRotation = Quaternion.Slerp(pathRotation, hingeRotation, hingeInfluence * recoveryHingeBlend);
            }
        }

        if (snakeIndex == 1)
        {
            hasFirstFollowerDebugTarget = true;
            firstFollowerDebugTarget = targetPosition;
        }

        return true;
    }

    private void UpdateRecoveryHingeBlend()
    {
        recoveryHingeBlend = Mathf.MoveTowards(recoveryHingeBlend, 1f, recoveryHingeRestoreSpeed * Time.fixedDeltaTime);
    }

    #endregion

    #region Cart Creation

    private void CreateBodyParts()
    {
        if (bodyParts.Count == 0) return;

        if (snakeBody.Count == 0)
        {
            CreateLeaderCart();
            return;
        }

        CreateFollowerCart();
    }

    private void CreateLeaderCart()
    {
        GameObject leaderInstance = Instantiate(bodyParts[0], transform.position, transform.rotation, transform);
        leaderInstance.tag = GetPlayerTag();

        ChainedCartManager leaderCartManager = leaderInstance.GetComponent<ChainedCartManager>();

        if (leaderCartManager != null)
        {
            leaderCartManager.CollectByPlayer();
            CacheCartManager(leaderInstance, leaderCartManager);
        }

        snakeBody.Add(leaderInstance);

        leadingCartBody = leaderInstance.GetComponent<Rigidbody>();

        CartControlScript leadingControl = leaderInstance.GetComponentInChildren<CartControlScript>(true);
        LeadingCartBehaviour[] leadingMovements = leaderInstance.GetComponentsInChildren<LeadingCartBehaviour>(true);
        leadingStallController = leaderInstance.GetComponentInChildren<LeadingCartStallController>(true);

        leadingRearHitch = leaderInstance.transform.Find("RearHitch");

        if (leadingCartBody == null) Debug.LogError("[SnakeCartManager] Leading Cart Base must have its Rigidbody on the prefab root.", leaderInstance);
        if (leadingControl == null) Debug.LogError("[SnakeCartManager] Could not find CartControlScript on the leading cart prefab.", leaderInstance);
        if (leadingMovements == null || leadingMovements.Length == 0) Debug.LogError("[SnakeCartManager] Could not find LeadingCartBehaviour virtual wheels.", leaderInstance);
        if (leadingStallController == null) Debug.LogError("[SnakeCartManager] Could not find LeadingCartStallController on the leading cart prefab.", leaderInstance);

        if (leadingStallController != null)
        {
            leadingStallController.OnStallStarted -= HandleLeaderStalled;
            leadingStallController.OnStallStarted += HandleLeaderStalled;

            leadingStallController.OnStallEnded -= HandleLeaderStallEnded;
            leadingStallController.OnStallEnded += HandleLeaderStallEnded;
        }

        if (leadingRearHitch == null)
        {
            Debug.LogError("[SnakeCartManager] Could not find direct-child RearHitch on the leading cart prefab.", leaderInstance);
        }
        else if (physicalJointProbe != null)
        {
            physicalJointProbe.Initialize(leadingRearHitch);
        }

        if (physicalJointProbe != null && physicalJointProbe.ProbeTransform != null)
        {
            pathHistory.Initialize(physicalJointProbe.ProbeTransform);
            pathHistory.SetLeaderBody(leadingCartBody);
        }
        else
        {
            Debug.LogError("[SnakeCartManager] Physical probe did not create a ProbeTransform.", this);
        }

        if (leadingCartBody != null &&
            leadingControl != null &&
            leadingMovements != null &&
            leadingMovements.Length > 0 &&
            pathHistory != null &&
            physicalJointProbe != null)
        {
            moveBackwardController.Initialize(this, leadingCartBody, leadingMovements, leadingControl, pathHistory, physicalJointProbe);
        }
        else
        {
            Debug.LogError("[SnakeCartManager] MoveBackward controller could not initialize.", leaderInstance);
        }

        if (setupCamera != null) setupCamera.Raise();

        bodyParts.RemoveAt(0);
        followerScaleDirty = true;
    }

    private void CreateFollowerCart()
    {
        followerSpawnTimer += Time.fixedDeltaTime;
        if (followerSpawnTimer < followerSpawnDelay) return;

        UpdateRequiredPathHistory();

        bool isMovingBackward = moveBackwardController != null && moveBackwardController.IsMovingBackward;
        bool canUseDirectFirstFollower = snakeBody.Count == 1 && pendingFollowers.Count == 0 && !isMovingBackward;

        if (canUseDirectFirstFollower)
        {
            CreateActiveFirstFollowerCart();
            return;
        }

        CreatePendingFollowerCart();
    }

    /// <summary>
    /// The first follower remains the one special case that joins the normal
    /// distance path immediately. Every later follower enters through the
    /// compact PendingFollower stage first.
    ///
    /// If the first follower is collected during MoveBackward, it also uses the
    /// pending stage temporarily so the active reverse topology cannot change.
    /// </summary>
    private void CreateActiveFirstFollowerCart()
    {
        int newSnakeIndex = snakeBody.Count;

        if (!TryGetDistancePathTarget(newSnakeIndex, out Vector3 spawnPosition, out Quaternion spawnRotation)) return;

        if (leadingCartBody != null) spawnPosition.y = leadingCartBody.position.y;

        if (!TrySpawnOwnedFollower(bodyParts[0], spawnPosition, spawnRotation, out GameObject newCart, out ChainedCartManager newCartManager)) return;

        snakeBody.Add(newCart);

        FinishFollowerSpawn(newCart, newCartManager);

        if (!vulnerabilitySessionActive &&
            leadingStallController != null &&
            leadingStallController.IsStalled)
        {
            BeginVulnerabilitySession();
        }
    }

    /// <summary>
    /// Later collected carts become visible immediately in a compact stack
    /// behind the current tail, but are NOT inserted into snakeBody yet.
    ///
    /// Forward:
    /// - the pending cart holds its world pose,
    /// - the active path train moves away,
    /// - once enough logical path distance opens, the oldest pending cart is
    ///   promoted into snakeBody without a spawn/snap surprise.
    ///
    /// MoveBackward:
    /// - pending carts are soft-docked behind the active tail every physics tick,
    /// - snakeBody topology remains unchanged,
    /// - MoveBackward therefore cannot be cancelled by ordinary collection.
    /// </summary>
    private void CreatePendingFollowerCart()
    {
        bool isMovingBackward = moveBackwardController != null && moveBackwardController.IsMovingBackward;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float heldPathProgress = 0f;
        bool hasValidHeldPathProgress = false;

        if (isMovingBackward)
        {
            GetNextDockedPendingPose(out spawnPosition, out spawnRotation);
        }
        else
        {
            TryGetNextPendingHeldPose(out spawnPosition, out spawnRotation, out heldPathProgress, out hasValidHeldPathProgress);
        }

        if (leadingCartBody != null) spawnPosition.y = leadingCartBody.position.y;

        if (!TrySpawnOwnedFollower(bodyParts[0], spawnPosition, spawnRotation, out GameObject newCart, out ChainedCartManager newCartManager)) return;

        PendingFollower pending = new PendingFollower
        {
            cart = newCart,
            manager = newCartManager,
            body = newCart.GetComponent<Rigidbody>(),
            heldPosition = spawnPosition,
            heldRotation = spawnRotation,
            heldPathProgress = heldPathProgress,
            hasValidHeldPathProgress = hasValidHeldPathProgress
        };

        bool wasEmpty = pendingFollowers.Count == 0;
        pendingFollowers.Add(pending);
        pendingFollowerCount = pendingFollowers.Count;

        FinishFollowerSpawn(newCart, newCartManager);

        if (wasEmpty) ResetPendingPromotionBaseline();

        if (isMovingBackward)
        {
            DockPendingFollowersToCurrentTail();
        }
    }

    private bool TrySpawnOwnedFollower(
        GameObject sourcePrefab,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject newCart,
        out ChainedCartManager newCartManager)
    {
        newCart = null;
        newCartManager = null;

        if (sourcePrefab == null) return false;

        newCart = Instantiate(sourcePrefab, spawnPosition, spawnRotation, transform);
        newCart.tag = GetPlayerTag();

        newCartManager = newCart.GetComponent<ChainedCartManager>();

        if (newCartManager == null)
        {
            Debug.LogError("[SnakeCartManager] Spawned follower is missing ChainedCartManager.", newCart);
            Destroy(newCart);
            return false;
        }

        newCartManager.CollectByPlayer();
        CacheCartManager(newCart, newCartManager);

        return true;
    }

    private void FinishFollowerSpawn(GameObject newCart, ChainedCartManager newCartManager)
    {
        if (newCartManager != null && !newCartManager.HasGroceryItem()) cartsWithoutItem.Add(newCart);

        bodyParts.RemoveAt(0);
        followerSpawnTimer = 0f;
        followerScaleDirty = true;

        if (newCartManager != null) StartCoroutine(PlayCollectVfxDelayed(newCartManager));
    }

    private bool TryGetNextPendingHeldPose(
        out Vector3 position,
        out Quaternion rotation,
        out float heldPathProgress,
        out bool hasValidHeldPathProgress)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        heldPathProgress = 0f;
        hasValidHeldPathProgress = false;

        if (pathHistory != null && pathHistory.IsInitialized)
        {
            float anchorProgress;

            if (pendingFollowers.Count > 0 &&
                pendingFollowers[pendingFollowers.Count - 1] != null &&
                pendingFollowers[pendingFollowers.Count - 1].hasValidHeldPathProgress)
            {
                anchorProgress = pendingFollowers[pendingFollowers.Count - 1].heldPathProgress;
            }
            else if (snakeBody.Count <= 1)
            {
                // The first pending follower is measured from the physical path
                // probe because that is also the origin of firstFollowerSpacing.
                anchorProgress = pathHistory.HeadProgress;
            }
            else
            {
                anchorProgress = GetDistancePathProgressForSnakeIndex(snakeBody.Count - 1);
            }

            heldPathProgress = anchorProgress - pendingCompactSpacing;

            if (pathHistory.TryGetPoseAtProgress(heldPathProgress, out position, out rotation))
            {
                if (leadingCartBody != null) position.y = leadingCartBody.position.y;
                hasValidHeldPathProgress = true;
                return true;
            }
        }

        GetNextDockedPendingPose(out position, out rotation);
        return true;
    }

    private void GetNextDockedPendingPose(out Vector3 position, out Quaternion rotation)
    {
        Transform anchor = GetPendingDockAnchor();

        if (anchor == null)
        {
            position = transform.position;
            rotation = transform.rotation;
            return;
        }

        Vector3 backward = -Vector3.ProjectOnPlane(anchor.forward, Vector3.up);

        if (backward.sqrMagnitude < 0.0001f) backward = -Vector3.forward;
        backward.Normalize();

        position = anchor.position + backward * pendingCompactSpacing;

        if (leadingCartBody != null) position.y = leadingCartBody.position.y;

        rotation = anchor.rotation;
    }

    private Transform GetPendingDockAnchor()
    {
        if (pendingFollowers.Count > 0)
        {
            PendingFollower lastPending = pendingFollowers[pendingFollowers.Count - 1];
            if (lastPending != null && lastPending.cart != null) return lastPending.cart.transform;
        }

        if (snakeBody.Count > 0 && snakeBody[snakeBody.Count - 1] != null)
        {
            return snakeBody[snakeBody.Count - 1].transform;
        }

        return leadingCartBody != null ? leadingCartBody.transform : null;
    }

    private IEnumerator PlayCollectVfxDelayed(ChainedCartManager cartManager)
    {
        if (collectVfxDelay > 0f) yield return new WaitForSeconds(collectVfxDelay);
        if (cartManager != null) cartManager.PlayVFX();
    }

    #endregion

    #region Pending Follower Join

    private void ManagePendingFollowerOwnership()
    {
        for (int i = 0; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];

            if (pending == null || pending.cart == null)
            {
                pendingFollowers.RemoveAt(i);
                pendingFollowerCount = pendingFollowers.Count;
                ResetPendingPromotionBaseline();
                i--;
                continue;
            }

            if (pending.manager == null) pending.manager = GetCartManager(pending.cart);

            if (pending.manager != null && pending.manager.isCollectedByPlayer) continue;

            // Pending followers are ordered behind the active tail. If one
            // loses ownership, it and every pending cart behind it become loose.
            DetachPendingFollowersFromIndex(i);
            break;
        }
    }

    private void UpdatePendingFollowerPromotion()
    {
        pendingFollowerCount = pendingFollowers.Count;

        if (pendingFollowers.Count == 0)
        {
            pendingRequiredForwardTravel = 0f;
            pendingForwardTravelSinceBaseline = 0f;
            return;
        }

        HoldPendingFollowersAtStoredPose();

        if (pathHistory == null || !pathHistory.IsInitialized) return;

        // A stalled cart is intentionally not opening the train spacing.
        // Pending carts stay exactly where they were collected until either
        // MoveBackward starts or normal movement genuinely resumes.
        if (leadingStallController != null && leadingStallController.IsStalled)
        {
            BeginPendingPromotionStallPause();
            return;
        }

        if (pendingPromotionPausedByStall) EndPendingPromotionStallPause();

        if (moveBackwardController != null && moveBackwardController.IsMovingBackward) return;

        float normalSpacing = snakeBody.Count <= 1 ? firstFollowerSpacing : followerCartSpacing;
        pendingRequiredForwardTravel = Mathf.Max(0f, normalSpacing - pendingCompactSpacing);
        pendingForwardTravelSinceBaseline = Mathf.Max(0f, pathHistory.HeadProgress - pendingPromotionStartHeadProgress);

        if (pendingForwardTravelSinceBaseline + pendingPromotionDistanceTolerance < pendingRequiredForwardTravel) return;

        PromoteFirstPendingFollower();
    }

    private void PromoteFirstPendingFollower()
    {
        if (pendingFollowers.Count == 0) return;

        PendingFollower pending = pendingFollowers[0];

        if (pending == null || pending.cart == null)
        {
            pendingFollowers.RemoveAt(0);
            pendingFollowerCount = pendingFollowers.Count;
            ResetPendingPromotionBaseline();
            return;
        }

        if (pending.manager == null) pending.manager = GetCartManager(pending.cart);

        if (pending.manager == null || !pending.manager.isCollectedByPlayer)
        {
            DetachPendingFollowersFromIndex(0);
            return;
        }

        int newSnakeIndex = snakeBody.Count;

        // The compact-gap timing is based on logical path distance, so this
        // target should already be almost identical to the cart's held pose.
        // Assigning the exact target here removes accumulated numerical error.
        if (TryGetDistancePathTarget(newSnakeIndex, out Vector3 targetPosition, out Quaternion targetRotation))
        {
            pending.cart.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        snakeBody.Add(pending.cart);
        pendingFollowers.RemoveAt(0);

        pendingFollowerCount = pendingFollowers.Count;
        followerScaleDirty = true;

        ResetPendingPromotionBaseline();
    }

    private void BeginPendingPromotionStallPause()
    {
        if (pendingPromotionPausedByStall) return;
        if (pendingFollowers.Count == 0 || pathHistory == null || !pathHistory.IsInitialized) return;

        pendingPromotionPausedByStall = true;
        pendingStallPauseHeadProgress = pathHistory.HeadProgress;
    }

    private void EndPendingPromotionStallPause()
    {
        if (!pendingPromotionPausedByStall) return;

        if (pathHistory != null && pathHistory.IsInitialized)
        {
            float pathProgressCreatedWhileStalled = Mathf.Max(0f, pathHistory.HeadProgress - pendingStallPauseHeadProgress);

            // Shift the baseline forward by any probe/path jitter accumulated
            // during Stall so that stalled time never helps a pending cart earn
            // its normal train spacing.
            pendingPromotionStartHeadProgress += pathProgressCreatedWhileStalled;
        }

        pendingPromotionPausedByStall = false;
        pendingStallPauseHeadProgress = 0f;
    }

    private void HoldPendingFollowersAtStoredPose()
    {
        for (int i = 0; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending == null || pending.cart == null) continue;

            pending.cart.transform.SetPositionAndRotation(pending.heldPosition, pending.heldRotation);

            if (pending.body == null) pending.body = pending.cart.GetComponent<Rigidbody>();

            if (pending.body != null)
            {
                pending.body.linearVelocity = Vector3.zero;
                pending.body.angularVelocity = Vector3.zero;
            }
        }
    }

    private void ResetPendingPromotionBaseline()
    {
        pendingPromotionStartHeadProgress = pathHistory != null && pathHistory.IsInitialized ? pathHistory.HeadProgress : 0f;
        pendingForwardTravelSinceBaseline = 0f;
        pendingPromotionPausedByStall = false;
        pendingStallPauseHeadProgress = 0f;

        float normalSpacing = snakeBody.Count <= 1 ? firstFollowerSpacing : followerCartSpacing;
        pendingRequiredForwardTravel = pendingFollowers.Count > 0 ? Mathf.Max(0f, normalSpacing - pendingCompactSpacing) : 0f;
    }

    /// <summary>
    /// During MoveBackward, pending carts behave like a compact soft-parented
    /// stack behind the active tail. They are deliberately NOT inserted into
    /// snakeBody, so the reverse controller's fixed active topology remains
    /// unchanged even when new carts are collected.
    /// </summary>
    private void DockPendingFollowersToCurrentTail()
    {
        if (pendingFollowers.Count == 0) return;

        Transform anchor = GetActiveTailTransform();
        if (anchor == null) return;

        pendingFollowersDockedToTail = true;

        for (int i = 0; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending == null || pending.cart == null) continue;

            Vector3 backward = -Vector3.ProjectOnPlane(anchor.forward, Vector3.up);

            if (backward.sqrMagnitude < 0.0001f) backward = -Vector3.forward;
            backward.Normalize();

            Vector3 targetPosition = anchor.position + backward * pendingCompactSpacing;
            if (leadingCartBody != null) targetPosition.y = leadingCartBody.position.y;

            Quaternion targetRotation = anchor.rotation;

            pending.cart.transform.SetPositionAndRotation(targetPosition, targetRotation);

            if (pending.body == null) pending.body = pending.cart.GetComponent<Rigidbody>();
            if (pending.body != null)
            {
                pending.body.linearVelocity = Vector3.zero;
                pending.body.angularVelocity = Vector3.zero;
            }

            pending.heldPosition = targetPosition;
            pending.heldRotation = targetRotation;
            pending.heldPathProgress = 0f;
            pending.hasValidHeldPathProgress = false;

            anchor = pending.cart.transform;
        }
    }

    /// <summary>
    /// After MoveBackward recovery, the normal path may have been rebuilt.
    /// Re-seat every pending cart into a compact stack on that fresh path,
    /// then let normal forward progress unzip them one by one.
    /// </summary>
    private void RedockPendingFollowersToCurrentPath()
    {
        pendingFollowersDockedToTail = false;

        if (pendingFollowers.Count == 0)
        {
            ResetPendingPromotionBaseline();
            return;
        }

        if (pathHistory == null || !pathHistory.IsInitialized)
        {
            ResetPendingPromotionBaseline();
            return;
        }

        float anchorProgress = snakeBody.Count <= 1
            ? pathHistory.HeadProgress
            : GetDistancePathProgressForSnakeIndex(snakeBody.Count - 1);

        Transform fallbackAnchor = GetActiveTailTransform();

        for (int i = 0; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending == null || pending.cart == null) continue;

            float heldProgress = anchorProgress - pendingCompactSpacing * (i + 1);

            if (pathHistory.TryGetPoseAtProgress(heldProgress, out Vector3 position, out Quaternion rotation))
            {
                if (leadingCartBody != null) position.y = leadingCartBody.position.y;

                pending.cart.transform.SetPositionAndRotation(position, rotation);

                if (pending.body == null) pending.body = pending.cart.GetComponent<Rigidbody>();
                if (pending.body != null)
                {
                    pending.body.linearVelocity = Vector3.zero;
                    pending.body.angularVelocity = Vector3.zero;
                }

                pending.heldPosition = position;
                pending.heldRotation = rotation;
                pending.heldPathProgress = heldProgress;
                pending.hasValidHeldPathProgress = true;

                fallbackAnchor = pending.cart.transform;
                continue;
            }

            if (fallbackAnchor != null)
            {
                Vector3 backward = -Vector3.ProjectOnPlane(fallbackAnchor.forward, Vector3.up);

                if (backward.sqrMagnitude < 0.0001f) backward = -Vector3.forward;
                backward.Normalize();

                Vector3 fallbackPosition = fallbackAnchor.position + backward * pendingCompactSpacing;
                if (leadingCartBody != null) fallbackPosition.y = leadingCartBody.position.y;

                pending.cart.transform.SetPositionAndRotation(fallbackPosition, fallbackAnchor.rotation);

                if (pending.body == null) pending.body = pending.cart.GetComponent<Rigidbody>();
                if (pending.body != null)
                {
                    pending.body.linearVelocity = Vector3.zero;
                    pending.body.angularVelocity = Vector3.zero;
                }

                pending.heldPosition = fallbackPosition;
                pending.heldRotation = fallbackAnchor.rotation;
                pending.heldPathProgress = 0f;
                pending.hasValidHeldPathProgress = false;

                fallbackAnchor = pending.cart.transform;
            }
        }

        ResetPendingPromotionBaseline();
    }

    private Transform GetActiveTailTransform()
    {
        if (snakeBody.Count > 0 && snakeBody[snakeBody.Count - 1] != null)
        {
            return snakeBody[snakeBody.Count - 1].transform;
        }

        return leadingCartBody != null ? leadingCartBody.transform : null;
    }

    #endregion

    #region Stall Vulnerability

    private void HandleLeaderStalled()
    {
        BeginVulnerabilitySession();
        BeginPendingPromotionStallPause();
    }

    private void HandleLeaderStallEnded()
    {
        EndPendingPromotionStallPause();

        // Natural escape from Stall has no MoveBackward-finished event, so
        // vulnerability must clear here. When Stall ends because MoveBackward
        // successfully started, the reverse controller is already active and
        // vulnerability intentionally remains until recovery finishes.
        bool moveBackwardOwnsRecovery = moveBackwardController != null && moveBackwardController.IsMovingBackward;

        if (!moveBackwardOwnsRecovery)
        {
            EndVulnerabilitySession();
        }
    }

    private void HandleMoveBackwardFinished()
    {
        EndVulnerabilitySession();
        RedockPendingFollowersToCurrentPath();
    }

    private void BeginVulnerabilitySession()
    {
        if (vulnerabilitySessionActive) return;
        if (vulnerableFollowerCount <= 0 || snakeBody.Count <= 1) return;

        ClearFollowerVulnerability();

        int endIndex = Mathf.Min(snakeBody.Count - 1, vulnerableFollowerCount);
        int markedCount = 0;

        for (int i = 1; i <= endIndex; i++)
        {
            ChainedCartManager cartManager = GetCartManager(snakeBody[i]);
            if (cartManager == null || !cartManager.isCollectedByPlayer) continue;

            cartManager.SetVulnerable(true);

            if (cartManager.IsVulnerable) markedCount++;
        }

        vulnerabilitySessionActive = markedCount > 0;
        currentVulnerableFollowerCount = markedCount;
    }

    private void EndVulnerabilitySession()
    {
        ClearFollowerVulnerability();
        vulnerabilitySessionActive = false;
        currentVulnerableFollowerCount = 0;
    }

    private void ClearFollowerVulnerability()
    {
        for (int i = 1; i < snakeBody.Count; i++)
        {
            ChainedCartManager cartManager = GetCartManager(snakeBody[i]);
            if (cartManager != null) cartManager.SetVulnerable(false);
        }

        currentVulnerableFollowerCount = 0;
    }

    private int CountVulnerableFollowers()
    {
        int count = 0;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            ChainedCartManager cartManager = GetCartManager(snakeBody[i]);
            if (cartManager != null && cartManager.IsVulnerable) count++;
        }

        return count;
    }

    #endregion

    #region Chain Management

    private void ManageSnakeBody()
    {
        if (bodyParts.Count > 0) CreateBodyParts();

        for (int i = 1; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];

            if (cart == null)
            {
                snakeBody.RemoveAt(i);
                followerScaleDirty = true;
                currentVulnerableFollowerCount = CountVulnerableFollowers();

                if (pendingFollowers.Count > 0 &&
                    (moveBackwardController == null || !moveBackwardController.IsMovingBackward))
                {
                    RedockPendingFollowersToCurrentPath();
                }

                break;
            }

            ChainedCartManager cartManager = GetCartManager(cart);

            if (cartManager == null)
            {
                Debug.LogError($"[SnakeCartManager] ChainedCartManager missing on {cart.name}.", cart);
                continue;
            }

            if (cartManager.isCollectedByPlayer) continue;

            DetachChainFromIndex(i);
            break;
        }

        if (snakeBody.Count == 0) Destroy(this);
    }

    private void DetachChainFromIndex(int startIndex)
    {
        if (startIndex < 1 || startIndex >= snakeBody.Count) return;

        for (int i = startIndex; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            ChainedCartManager cartManager = GetCartManager(cart);

            if (cartManager != null && cartManager.HasGroceryItem())
            {
                groceryItemCartCount = Mathf.Max(0, groceryItemCartCount - 1);
            }

            cart.transform.localScale = normalScale;
            cart.transform.SetParent(null);

            if (cartManager != null && cartManager.isCollectedByPlayer) cartManager.OnDetach();

            cartsWithoutItem.Remove(cart);
            UncacheCartManager(cart);
        }

        snakeBody.RemoveRange(startIndex, snakeBody.Count - startIndex);

        // Pending carts are always logically behind the active tail, so any
        // active-chain cut also cuts every pending cart.
        DetachPendingFollowersFromIndex(0);

        followerScaleDirty = true;
        currentVulnerableFollowerCount = CountVulnerableFollowers();

        if (snakeBody.Count <= 1 && vulnerabilitySessionActive)
        {
            currentVulnerableFollowerCount = 0;
        }

        ResetPendingPromotionBaseline();
    }

    private int DetachPendingFollowersFromIndex(int startIndex)
    {
        if (startIndex < 0 || startIndex >= pendingFollowers.Count) return 0;

        int detachedCount = pendingFollowers.Count - startIndex;

        for (int i = startIndex; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending == null || pending.cart == null) continue;

            ChainedCartManager cartManager = pending.manager != null ? pending.manager : GetCartManager(pending.cart);

            if (cartManager != null && cartManager.HasGroceryItem())
            {
                groceryItemCartCount = Mathf.Max(0, groceryItemCartCount - 1);
            }

            pending.cart.transform.localScale = normalScale;
            pending.cart.transform.SetParent(null);

            if (cartManager != null && cartManager.isCollectedByPlayer) cartManager.OnDetach();

            cartsWithoutItem.Remove(pending.cart);
            UncacheCartManager(pending.cart);
        }

        pendingFollowers.RemoveRange(startIndex, pendingFollowers.Count - startIndex);
        pendingFollowerCount = pendingFollowers.Count;
        pendingFollowersDockedToTail = false;
        followerScaleDirty = true;

        ResetPendingPromotionBaseline();
        return detachedCount;
    }

    public int DetachAllFollowers()
    {
        int activeFollowerCount = Mathf.Max(0, snakeBody.Count - 1);
        int pendingCount = pendingFollowers.Count;
        int detachedCount = activeFollowerCount + pendingCount;

        if (activeFollowerCount > 0)
        {
            DetachChainFromIndex(1);
        }
        else if (pendingCount > 0)
        {
            DetachPendingFollowersFromIndex(0);
        }

        if (detachedCount > 0 && sfxManager != null) sfxManager.PlaySFX("Detach");

        return detachedCount;
    }

    /// <summary>
    /// Cuts the defender's chain starting AT the vulnerable follower that was hit.
    /// The hit vulnerable cart, every active cart behind it, and every pending
    /// cart behind the active tail become loose.
    /// </summary>
    public int DetachFromVulnerableCart(ChainedCartManager vulnerableCart)
    {
        if (vulnerableCart == null || !vulnerableCart.isCollectedByPlayer || !vulnerableCart.IsVulnerable) return 0;

        int vulnerableIndex = FindSnakeIndex(vulnerableCart);
        if (vulnerableIndex <= 0) return 0;

        int detachedCount = snakeBody.Count - vulnerableIndex + pendingFollowers.Count;
        DetachChainFromIndex(vulnerableIndex);

        if (detachedCount > 0 && sfxManager != null) sfxManager.PlaySFX("Detach");

        return detachedCount;
    }

    private int FindSnakeIndex(ChainedCartManager cartManager)
    {
        if (cartManager == null) return -1;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (GetCartManager(snakeBody[i]) == cartManager) return i;
        }

        return -1;
    }

    #endregion

    #region Follower Scale

    private void UpdateFollowerScaleIfNeeded()
    {
        if (needScaleup != lastNeedScaleup)
        {
            lastNeedScaleup = needScaleup;
            followerScaleDirty = true;
        }

        if (!followerScaleDirty) return;

        Vector3 targetScale = needScaleup ? upScale : normalScale;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] != null) snakeBody[i].transform.localScale = targetScale;
        }

        for (int i = 0; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending != null && pending.cart != null) pending.cart.transform.localScale = targetScale;
        }

        followerScaleDirty = false;
    }

    #endregion

    #region MoveBackward Recovery / API

    public bool CompleteMoveBackwardRecovery()
    {
        if (snakeBody == null || snakeBody.Count < 2) return false;
        if (leadingRearHitch == null || physicalJointProbe == null || pathHistory == null) return false;

        Physics.SyncTransforms();
        physicalJointProbe.Initialize(leadingRearHitch);

        if (physicalJointProbe.ProbeTransform == null)
        {
            Debug.LogError("[SnakeCartManager] Recovery probe rebuild failed.", this);
            return false;
        }

        recoveryFollowerPositions.Clear();

        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null) return false;
            recoveryFollowerPositions.Add(snakeBody[i].transform.position);
        }

        bool seeded = pathHistory.ResetHistoryFromCurrentChain(
            physicalJointProbe.ProbeTransform,
            recoveryFollowerPositions,
            firstFollowerSpacing,
            followerCartSpacing
        );

        if (!seeded)
        {
            Debug.LogError("[SnakeCartManager] Failed to seed normal path from recovered chain pose.", this);
            return false;
        }

        pathHistory.SetLeaderBody(leadingCartBody);
        recoveryHingeBlend = 0f;

        Physics.SyncTransforms();
        return true;
    }

    public bool PlayMoveBackward(float distance)
    {
        return moveBackwardController != null && moveBackwardController.PlayBackward(distance);
    }

    public bool IsMovingBackward()
    {
        return moveBackwardController != null && moveBackwardController.IsMovingBackward;
    }

    public float GetDistancePathProgressForSnakeIndex(int snakeIndex)
    {
        if (pathHistory == null || !pathHistory.IsInitialized) return 0f;
        if (snakeIndex <= 0) return pathHistory.HeadProgress;

        int followerIndex = snakeIndex - 1;
        float distanceBehindProbe = firstFollowerSpacing + followerIndex * followerCartSpacing;

        return pathHistory.HeadProgress - distanceBehindProbe;
    }

    #endregion

    #region Chain API

    public void AddBodyParts(GameObject addedObj)
    {
        if (addedObj == null) return;
        bodyParts.Add(addedObj);
    }

    /// <summary>
    /// Gameplay-facing owned cart count. Includes compact pending followers
    /// because they are already visibly collected and owned by the player.
    /// </summary>
    public int GetSnakeBodyLength()
    {
        return snakeBody.Count + pendingFollowers.Count;
    }

    /// <summary>
    /// Active path-following topology only. MoveBackward intentionally uses
    /// this stable list and does not include pending followers.
    /// </summary>
    public int GetActiveSnakeBodyLength()
    {
        return snakeBody.Count;
    }

    public int GetPendingFollowerCount()
    {
        return pendingFollowers.Count;
    }

    public List<GameObject> GetSnakeBody()
    {
        return snakeBody;
    }

    #endregion

    #region Checkout / Grocery Items

    public int CheckOutNextCartWithItem()
    {
        if (snakeBody.Count <= 1 && pendingFollowers.Count == 0) return groceryItemCartCount;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            ChainedCartManager cartManager = GetCartManager(cart);
            if (cartManager == null || !cartManager.HasGroceryItem()) continue;

            RegisterCheckoutForCart(cartManager);
            RemoveAndDestroySnakeCartAt(i);
            return groceryItemCartCount;
        }

        // Pending carts are already visibly owned and can receive grocery items,
        // so checkout must also be able to consume them.
        for (int i = 0; i < pendingFollowers.Count; i++)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending == null || pending.cart == null) continue;

            ChainedCartManager cartManager = pending.manager != null ? pending.manager : GetCartManager(pending.cart);
            if (cartManager == null || !cartManager.HasGroceryItem()) continue;

            RegisterCheckoutForCart(cartManager);
            RemoveAndDestroyPendingCartAt(i);
            return groceryItemCartCount;
        }

        return groceryItemCartCount;
    }

    private void RegisterCheckoutForCart(ChainedCartManager cartManager)
    {
        if (cartManager == null) return;

        bool isExpensiveItem = cartManager.isCarryingExpensiveGroceryItem();
        groceryItemCartCount = Mathf.Max(0, groceryItemCartCount - 1);

        if (cashScoreManager != null) cashScoreManager.RegisterItemCheckout(playerIndex, isExpensiveItem);
        if (sfxManager != null) sfxManager.PlaySFX("CheckoutSingle");
    }

    public void CollectNormalGroceryItem()
    {
        if (!TryTakeRandomEmptyCart(out ChainedCartManager cartManager)) return;

        groceryItemCartCount++;
        cartManager.EnableNormalGroveryItem();
    }

    public void CollectExpensiveGroceryItem()
    {
        if (!TryTakeRandomEmptyCart(out ChainedCartManager cartManager)) return;

        groceryItemCartCount++;
        cartManager.EnableExpensiveGroveryItem();
    }

    private bool TryTakeRandomEmptyCart(out ChainedCartManager cartManager)
    {
        cartManager = null;

        while (cartsWithoutItem.Count > 0)
        {
            int cartIndex = Random.Range(0, cartsWithoutItem.Count);
            GameObject cart = cartsWithoutItem[cartIndex];

            cartsWithoutItem.RemoveAt(cartIndex);

            if (cart == null) continue;

            cartManager = GetCartManager(cart);
            if (cartManager != null) return true;
        }

        return false;
    }

    public void IncreaseNumOfCartsWithItem()
    {
        groceryItemCartCount++;
    }

    public int GetCurrentNumOfCartsWithItem()
    {
        return groceryItemCartCount;
    }

    public bool HasEmptyCartForGroceryItem()
    {
        return cartsWithoutItem.Count > 0;
    }

    public void RemoveAllCartsWithItem()
    {
        if (sfxManager != null) sfxManager.PlaySFX("CheckoutCarts");

        for (int i = snakeBody.Count - 1; i >= 1; i--)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            ChainedCartManager cartManager = GetCartManager(cart);
            if (cartManager == null || !cartManager.HasGroceryItem()) continue;

            groceryItemCartCount = Mathf.Max(0, groceryItemCartCount - 1);
            RemoveAndDestroySnakeCartAt(i);
        }

        for (int i = pendingFollowers.Count - 1; i >= 0; i--)
        {
            PendingFollower pending = pendingFollowers[i];
            if (pending == null || pending.cart == null) continue;

            ChainedCartManager cartManager = pending.manager != null ? pending.manager : GetCartManager(pending.cart);
            if (cartManager == null || !cartManager.HasGroceryItem()) continue;

            groceryItemCartCount = Mathf.Max(0, groceryItemCartCount - 1);
            RemoveAndDestroyPendingCartAt(i);
        }
    }

    private void RemoveAndDestroySnakeCartAt(int index)
    {
        if (index < 1 || index >= snakeBody.Count) return;

        GameObject cart = snakeBody[index];

        snakeBody.RemoveAt(index);
        cartsWithoutItem.Remove(cart);
        UncacheCartManager(cart);

        followerScaleDirty = true;
        currentVulnerableFollowerCount = CountVulnerableFollowers();

        if (cart != null) Destroy(cart);

        if (pendingFollowers.Count > 0 &&
            (moveBackwardController == null || !moveBackwardController.IsMovingBackward))
        {
            RedockPendingFollowersToCurrentPath();
        }
    }

    private void RemoveAndDestroyPendingCartAt(int index)
    {
        if (index < 0 || index >= pendingFollowers.Count) return;

        PendingFollower pending = pendingFollowers[index];
        GameObject cart = pending != null ? pending.cart : null;

        pendingFollowers.RemoveAt(index);
        pendingFollowerCount = pendingFollowers.Count;

        if (cart != null)
        {
            cartsWithoutItem.Remove(cart);
            UncacheCartManager(cart);
            Destroy(cart);
        }

        followerScaleDirty = true;

        if (moveBackwardController != null && moveBackwardController.IsMovingBackward)
        {
            DockPendingFollowersToCurrentTail();
        }
        else
        {
            RedockPendingFollowersToCurrentPath();
        }
    }

    #endregion

    #region Cart Manager Cache

    private void CacheExistingCartManagers()
    {
        cartManagerCache.Clear();

        for (int i = 0; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            ChainedCartManager cartManager = cart.GetComponent<ChainedCartManager>();
            if (cartManager != null) cartManagerCache[cart] = cartManager;
        }
    }

    private void CacheCartManager(GameObject cart, ChainedCartManager cartManager)
    {
        if (cart == null || cartManager == null) return;
        cartManagerCache[cart] = cartManager;
    }

    private ChainedCartManager GetCartManager(GameObject cart)
    {
        if (cart == null) return null;

        if (cartManagerCache.TryGetValue(cart, out ChainedCartManager cachedManager) && cachedManager != null)
        {
            return cachedManager;
        }

        ChainedCartManager cartManager = cart.GetComponent<ChainedCartManager>();

        if (cartManager != null) cartManagerCache[cart] = cartManager;

        return cartManager;
    }

    private void UncacheCartManager(GameObject cart)
    {
        if (cart == null) return;
        cartManagerCache.Remove(cart);
    }

    #endregion

    #region Assist Player Data

    private string GetPlayerTag()
    {
        return $"Player{playerIndex}";
    }

    public int GetPlayerId()
    {
        return playerIndex;
    }

    public int GetCurrentScore()
    {
        return cashScoreManager != null ? cashScoreManager.GetPlayerScore(playerIndex) : 0;
    }

    public int GetCurrentCartCount()
    {
        return snakeBody.Count + pendingFollowers.Count;
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        firstFollowerSpacing = Mathf.Max(0.1f, firstFollowerSpacing);
        followerCartSpacing = Mathf.Max(0.1f, followerCartSpacing);
        pathHistorySafetyBuffer = Mathf.Max(0f, pathHistorySafetyBuffer);

        followerSpawnDelay = Mathf.Max(0f, followerSpawnDelay);
        collectVfxDelay = Mathf.Max(0f, collectVfxDelay);

        float smallestNormalSpacing = Mathf.Min(firstFollowerSpacing, followerCartSpacing);
        pendingCompactSpacing = Mathf.Clamp(pendingCompactSpacing, 0.01f, Mathf.Max(0.01f, smallestNormalSpacing * 0.95f));
        pendingPromotionDistanceTolerance = Mathf.Max(0f, pendingPromotionDistanceTolerance);
    }

    #endregion

    #region Debug Drawing

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !drawFirstFollowerTarget || !hasFirstFollowerDebugTarget) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(firstFollowerDebugTarget, 0.15f);
    }

    #endregion
}
