using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Owns the player's cart chain, normal distance-path following, cart creation/removal,
/// grocery-cart bookkeeping, and the handoff to SnakeMoveBackwardController.
///
/// The old MarkerManager follower system and LeadingCartRaycaster integration have
/// been removed. Distance-based SnakePathHistory is now the only follower system.
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

    [Header("Follower Rotation")]
    [Range(0f, 1f)]
    [SerializeField] private float firstFollowerHingeInfluence = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float hingeInfluenceFalloffPerCart = 0.2f;

    [Tooltip("How quickly live hinge rotation influence fades back in after MoveBackward recovery.")]
    [Min(0f)]
    [SerializeField] private float recoveryHingeRestoreSpeed = 5f;

    #endregion

    #region MoveBackward / Physical Probe

    [Header("Move Backward")]
    [SerializeField] private SnakeMoveBackwardController moveBackwardController;

    [Header("Physical Joint")]
    [SerializeField] private PhysicalChainJointProbe physicalJointProbe;

    #endregion

    #region Cart Creation / Chain State

    [Header("Cart Creation")]
    [FormerlySerializedAs("distanceBetween")]
    [Tooltip("Delay before a queued follower cart is spawned into the chain.")]
    [Min(0f)]
    [SerializeField] private float followerSpawnDelay = 0.2f;

    [Tooltip("Delay after a new follower is actually spawned before its collect VFX plays.")]
    [Min(0f)]
    [SerializeField] private float collectVfxDelay = 0.12f;

    [SerializeField] private List<GameObject> bodyParts = new List<GameObject>();
    [SerializeField] private List<GameObject> snakeBody = new List<GameObject>();

    [FormerlySerializedAs("cartsWithOutItem")]
    [SerializeField] private List<GameObject> cartsWithoutItem = new List<GameObject>();

    [Header("Follower Scale")]
    public bool needScaleup = false;
    [SerializeField] private Vector3 normalScale = new Vector3(6f, 6f, 6f);
    [SerializeField] private Vector3 upScale = new Vector3(10f, 10f, 10f);
    [SerializeField] private Vector3 downScale = new Vector3(3f, 3f, 3f);

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

    private readonly Dictionary<GameObject, ChainedCartManager> cartManagerCache = new Dictionary<GameObject, ChainedCartManager>();

    private Rigidbody leadingCartBody;
    private Transform leadingRearHitch;

    private float followerSpawnTimer;
    private float recoveryHingeBlend = 1f;

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

        lastNeedScaleup = needScaleup;
        CacheExistingCartManagers();
    }

    private void Start()
    {
        CreateBodyParts();
    }

    private void FixedUpdate()
    {
        ManageSnakeBody();

        bool moveBackwardTicked = moveBackwardController != null && moveBackwardController.TickMoveBackward();

        UpdateFollowerScaleIfNeeded();

        // A lone-cart reverse still needs its normal physical probe/path to keep
        // recording. Only the 1+ follower reverse-tow owns the whole snake.
        bool moveBackwardOwnsSnake = moveBackwardTicked && snakeBody.Count > 1;
        if (moveBackwardOwnsSnake) return;

        UpdateRecoveryHingeBlend();

        if (pathHistory != null && pathHistory.IsInitialized) pathHistory.TickHistory();

        MoveAllFollowersUsingDistancePath();
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
                hingeInfluence *= recoveryHingeBlend;
                targetRotation = Quaternion.Slerp(pathRotation, hingeRotation, hingeInfluence);
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

        // RearHitch is a direct child of the real physics root in the new Base prefab.
        leadingRearHitch = leaderInstance.transform.Find("RearHitch");

        if (leadingCartBody == null)
        {
            Debug.LogError("[SnakeCartManager] Leading Cart Base must have its Rigidbody on the prefab root.", leaderInstance);
        }

        if (leadingControl == null)
        {
            Debug.LogError("[SnakeCartManager] Could not find CartControlScript on the leading cart prefab.", leaderInstance);
        }

        if (leadingMovements == null || leadingMovements.Length == 0)
        {
            Debug.LogError("[SnakeCartManager] Could not find any LeadingCartBehaviour virtual wheels.", leaderInstance);
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

        int newSnakeIndex = snakeBody.Count;

        if (!TryGetDistancePathTarget(newSnakeIndex, out Vector3 spawnPosition, out Quaternion spawnRotation)) return;

        if (leadingCartBody != null) spawnPosition.y = leadingCartBody.position.y;

        GameObject newCart = Instantiate(bodyParts[0], spawnPosition, spawnRotation, transform);
        newCart.tag = GetPlayerTag();

        ChainedCartManager newCartManager = newCart.GetComponent<ChainedCartManager>();

        if (newCartManager != null)
        {
            newCartManager.CollectByPlayer();
            newCartManager.SetCartTeamColor();
            CacheCartManager(newCart, newCartManager);
        }
        else
        {
            Debug.LogError("[SnakeCartManager] Spawned follower is missing ChainedCartManager.", newCart);
        }

        snakeBody.Add(newCart);

        if (newCartManager != null && !newCartManager.HasGroceryItem()) cartsWithoutItem.Add(newCart);

        bodyParts.RemoveAt(0);
        followerSpawnTimer = 0f;
        followerScaleDirty = true;

        if (newCartManager != null) StartCoroutine(PlayCollectVfxDelayed(newCartManager));
    }

    private IEnumerator PlayCollectVfxDelayed(ChainedCartManager cartManager)
    {
        if (collectVfxDelay > 0f) yield return new WaitForSeconds(collectVfxDelay);

        if (cartManager != null) cartManager.PlayVFX();
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

            // C1 may already have been detached by the battle system.
            // Do not apply its detach impulse a second time.
            if (cartManager != null && cartManager.isCollectedByPlayer) cartManager.OnDetach();

            cartsWithoutItem.Remove(cart);
            UncacheCartManager(cart);
        }

        snakeBody.RemoveRange(startIndex, snakeBody.Count - startIndex);
        followerScaleDirty = true;
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

        List<Vector3> followerPositions = new List<Vector3>(snakeBody.Count - 1);

        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null) return false;
            followerPositions.Add(snakeBody[i].transform.position);
        }

        bool seeded = pathHistory.ResetHistoryFromCurrentChain(
            physicalJointProbe.ProbeTransform,
            followerPositions,
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

    public int GetSnakeBodyLength()
    {
        return snakeBody.Count;
    }

    /// <summary>
    /// Runtime chain list. Callers should treat this list as read-only.
    /// Index 0 is the leading cart.
    /// </summary>
    public List<GameObject> GetSnakeBody()
    {
        return snakeBody;
    }

    #endregion

    #region Checkout / Grocery Items

    public int CheckOutNextCartWithItem()
    {
        if (snakeBody.Count <= 1) return groceryItemCartCount;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            ChainedCartManager cartManager = GetCartManager(cart);
            if (cartManager == null || !cartManager.HasGroceryItem()) continue;

            bool isExpensiveItem = cartManager.isCarryingExpensiveGroceryItem();
            groceryItemCartCount = Mathf.Max(0, groceryItemCartCount - 1);

            if (cashScoreManager != null) cashScoreManager.RegisterItemCheckout(playerIndex, isExpensiveItem);
            if (sfxManager != null) sfxManager.PlaySFX("CheckoutSingle");

            RemoveAndDestroySnakeCartAt(i);

            return groceryItemCartCount;
        }

        return groceryItemCartCount;
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
    }

    private void RemoveAndDestroySnakeCartAt(int index)
    {
        if (index < 1 || index >= snakeBody.Count) return;

        GameObject cart = snakeBody[index];

        snakeBody.RemoveAt(index);
        cartsWithoutItem.Remove(cart);
        UncacheCartManager(cart);
        followerScaleDirty = true;

        if (cart != null) Destroy(cart);
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
        return snakeBody.Count;
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