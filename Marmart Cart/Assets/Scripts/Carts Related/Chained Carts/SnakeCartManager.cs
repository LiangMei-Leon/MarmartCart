using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the cart chain, normal distance-path following, and the handoff between
/// normal driving and the temporary MoveBackward reverse-tow system.
/// </summary>
public class SnakeCartManager : MonoBehaviour, IAssistPlayerDataSource
{
    #region Path Following Settings

    [Header("Distance Path")]
    [SerializeField] private SnakePathHistory pathHistory;
    [SerializeField] private bool useDistancePathForAllFollowers = true;

    [Tooltip("Logical path distance from the physical probe to Cart 1.")]
    [Min(0.1f)]
    [SerializeField] private float firstFollowerSpacing = 1.0f;

    [Tooltip("Logical center-to-center path distance between later carts.")]
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

    #region MoveBackward

    [Header("Move Backward")]
    [SerializeField] private SnakeMoveBackwardController moveBackwardController;

    #endregion

    #region Cart Creation / Chain State

    [Header("Cart Creation")]
    [SerializeField] private float distanceBetween = 0.2f;
    [SerializeField] private List<GameObject> bodyParts = new List<GameObject>();
    [SerializeField] private List<GameObject> snakeBody = new List<GameObject>();
    [SerializeField] private List<GameObject> cartsWithOutItem = new List<GameObject>();

    [Header("Follower Scale")]
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

    [Header("Physical Joint")]
    [SerializeField] private PhysicalChainJointProbe physicalJointProbe;

    #endregion

    #region Debug

    [Header("Distance Path Debug")]
    [SerializeField] private bool drawFirstFollowerTarget = true;

    private bool hasFirstFollowerDebugTarget;
    private Vector3 firstFollowerDebugTarget;

    #endregion

    #region Runtime

    private LeadingCartRaycaster leadingCartRaycaster;
    private MarkerManager leadingMarkerManager;

    private Rigidbody leadingCartBody;
    private Transform leadingRearHitch;

    private bool disabledLegacyLeaderMarker;
    private bool firstFollowerUsedDistancePathThisTick;

    private float countUp;
    private float recoveryHingeBlend = 1f;

    [SerializeField] private int numOfCartsWithGroceryItem = 0;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (pathHistory == null) pathHistory = GetComponent<SnakePathHistory>();
        if (pathHistory == null) pathHistory = gameObject.AddComponent<SnakePathHistory>();

        if (physicalJointProbe == null) physicalJointProbe = GetComponent<PhysicalChainJointProbe>();

        if (moveBackwardController == null) moveBackwardController = GetComponent<SnakeMoveBackwardController>();
        if (moveBackwardController == null) moveBackwardController = gameObject.AddComponent<SnakeMoveBackwardController>();
    }

    private void Start()
    {
        CreateBodyParts();
    }

    private void FixedUpdate()
    {
        ManageSnakeBody();
        UpdateLegacyLeaderMarkerState();

        // TRUE only for the 1+ follower reverse-tow mode. The lone-cart speed
        // curve does not own the chain, so normal probe/path recording continues.
        bool moveBackwardOwnsSnake = moveBackwardController != null && moveBackwardController.TickMoveBackward();

        UpdateFollowerScale();

        if (moveBackwardOwnsSnake) return;

        UpdateRecoveryHingeBlend();

        if (pathHistory != null && pathHistory.IsInitialized) pathHistory.TickHistory();

        if (useDistancePathForAllFollowers) MoveAllFollowersUsingDistancePath();
        else SnakeMovement();
    }

    #endregion

    #region Normal Distance-Path Movement

    private void MoveAllFollowersUsingDistancePath()
    {
        if (!useDistancePathForAllFollowers || pathHistory == null || !pathHistory.IsInitialized) return;

        firstFollowerUsedDistancePathThisTick = false;

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

        // Path owns X/Z and tangent. The authoritative Leader Rigidbody owns the
        // vertical plane so suspension/probe height never pushes followers down.
        if (leadingCartBody != null) targetPosition.y = leadingCartBody.position.y;

        targetRotation = pathRotation;

        // Cart 1 gets the strongest live physical-hinge influence. Farther carts
        // progressively become pure path-tangent followers.
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
            firstFollowerUsedDistancePathThisTick = true;
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

    #region Legacy Marker Movement

    private void SnakeMovement()
    {
        if (snakeBody.Count <= 1) return;

        int legacyStartIndex = firstFollowerUsedDistancePathThisTick ? 2 : 1;

        for (int i = legacyStartIndex; i < snakeBody.Count; i++)
        {
            MarkerManager markerManager = snakeBody[i - 1].GetComponent<MarkerManager>();
            if (markerManager == null || markerManager.markerList.Count == 0) continue;

            snakeBody[i].transform.position = markerManager.markerList[0].position;
            snakeBody[i].transform.rotation = markerManager.markerList[0].rotation;
            markerManager.markerList.RemoveAt(0);
        }
    }

    private void UpdateLegacyLeaderMarkerState()
    {
        if (leadingMarkerManager == null && snakeBody.Count > 0 && snakeBody[0] != null) leadingMarkerManager = snakeBody[0].GetComponent<MarkerManager>();
        if (leadingMarkerManager == null) return;

        bool shouldDisableLegacyLeaderMarker = snakeBody.Count > 1;

        if (shouldDisableLegacyLeaderMarker && !disabledLegacyLeaderMarker)
        {
            leadingMarkerManager.ClearMarkerList();
            leadingMarkerManager.enabled = false;
            disabledLegacyLeaderMarker = true;
            return;
        }

        if (!shouldDisableLegacyLeaderMarker && disabledLegacyLeaderMarker)
        {
            leadingMarkerManager.enabled = true;
            leadingMarkerManager.ClearMarkerList();
            disabledLegacyLeaderMarker = false;
        }
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

        if (!leaderInstance.GetComponent<MarkerManager>()) leaderInstance.AddComponent<MarkerManager>();

        ChainedCartManager cartManager = leaderInstance.GetComponent<ChainedCartManager>();
        if (cartManager != null) cartManager.CollectByPlayer();

        snakeBody.Add(leaderInstance);

        leadingCartRaycaster = leaderInstance.GetComponent<LeadingCartRaycaster>();
        leadingMarkerManager = leaderInstance.GetComponent<MarkerManager>();

        LeadingCartBehaviour[] leadingMovements = leaderInstance.GetComponentsInChildren<LeadingCartBehaviour>(true);
        Rigidbody leadingBody = leadingMovements != null && leadingMovements.Length > 0 ? leadingMovements[0].CartBody : null;
        CartControlScript leadingControl = leaderInstance.GetComponentInChildren<CartControlScript>(true);

        leadingCartBody = leadingBody;
        leadingRearHitch = leaderInstance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RearHitch");

        if (leadingRearHitch == null)
        {
            Debug.LogError("[SnakeCartManager] Could not find RearHitch on leading cart prefab.", leaderInstance);
        }
        else if (physicalJointProbe == null)
        {
            Debug.LogError("[SnakeCartManager] PhysicalChainJointProbe is missing.", this);
        }
        else
        {
            physicalJointProbe.Initialize(leadingRearHitch);
        }

        if (physicalJointProbe != null && physicalJointProbe.ProbeTransform != null)
        {
            pathHistory.Initialize(physicalJointProbe.ProbeTransform);
            pathHistory.SetLeaderBody(leadingBody);
        }
        else
        {
            Debug.LogError("[SnakeCartManager] Physical probe did not create a ProbeTransform.", this);
        }

        if (leadingBody != null && leadingControl != null && pathHistory != null)
        {
            moveBackwardController.Initialize(this, leadingBody, leadingControl, pathHistory);
        }
        else
        {
            Debug.LogError("[SnakeCartManager] MoveBackward controller could not initialize.", leaderInstance);
        }

        if (setupCamera != null) setupCamera.Raise();

        bodyParts.RemoveAt(0);
    }

    private void CreateFollowerCart()
    {
        MarkerManager markerManager = snakeBody[snakeBody.Count - 1].GetComponent<MarkerManager>();
        if (markerManager != null && countUp == 0f) markerManager.ClearMarkerList();

        countUp += Time.deltaTime;
        if (countUp < distanceBetween) return;

        int newSnakeIndex = snakeBody.Count;
        if (!TryGetDistancePathTarget(newSnakeIndex, out Vector3 spawnPosition, out Quaternion spawnRotation)) return;

        if (leadingCartBody != null) spawnPosition.y = leadingCartBody.position.y;

        GameObject newCart = Instantiate(bodyParts[0], spawnPosition, spawnRotation, transform);
        newCart.tag = GetPlayerTag();

        if (!newCart.GetComponent<MarkerManager>()) newCart.AddComponent<MarkerManager>();

        ChainedCartManager newCartManager = newCart.GetComponent<ChainedCartManager>();

        if (newCartManager != null)
        {
            newCartManager.CollectByPlayer();
            newCartManager.SetCartTeamColor();
        }

        snakeBody.Add(newCart);

        if (newCartManager != null && !newCartManager.HasGroceryItem()) cartsWithOutItem.Add(newCart);

        bodyParts.RemoveAt(0);
        countUp = 0f;
    }

    #endregion

    #region Chain Management

    private void ManageSnakeBody()
    {
        if (bodyParts.Count > 0) CreateBodyParts();

        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null)
            {
                snakeBody.RemoveAt(i);
                break;
            }

            ChainedCartManager cartManager = snakeBody[i].GetComponent<ChainedCartManager>();

            if (cartManager == null)
            {
                Debug.LogError("No Chained Cart Manager Component on " + snakeBody[i].name);
                continue;
            }

            if (cartManager.isCollectedByPlayer) continue;

            for (int j = i; j < snakeBody.Count; j++)
            {
                ChainedCartManager detachedManager = snakeBody[j].GetComponent<ChainedCartManager>();
                if (detachedManager != null && detachedManager.HasGroceryItem()) numOfCartsWithGroceryItem--;

                snakeBody[j].transform.localScale = new Vector3(6f, 6f, 6f);
                snakeBody[j].transform.SetParent(null);

                if (detachedManager != null) detachedManager.OnDetach();

                cartsWithOutItem.Remove(snakeBody[j]);
            }

            snakeBody.RemoveRange(i, snakeBody.Count - i);
            break;
        }

        if (snakeBody.Count == 0) Destroy(this);
    }

    private void UpdateFollowerScale()
    {
        Vector3 targetScale = needScaleup ? new Vector3(10f, 10f, 10f) : new Vector3(5f, 5f, 5f);

        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] != null) snakeBody[i].transform.localScale = targetScale;
        }
    }

    public void AddBodyParts(GameObject addedObj)
    {
        bodyParts.Add(addedObj);
        StartCoroutine(DelayedPlayVFX());
    }

    private IEnumerator DelayedPlayVFX()
    {
        yield return new WaitForSeconds(0.12f);

        if (snakeBody.Count == 0)
        {
            Debug.LogError("SnakeBody list is empty. No VFX to play.");
            yield break;
        }

        GameObject lastCart = snakeBody[snakeBody.Count - 1];
        ChainedCartManager cartManager = lastCart.GetComponent<ChainedCartManager>();

        if (cartManager != null)
        {
            Debug.Log("Playing VFX on: " + lastCart.name);
            cartManager.PlayVFX();
        }
        else
        {
            Debug.LogError("ChainedCartManager missing on: " + lastCart.name);
        }
    }

    #endregion

    #region MoveBackward Recovery / API

    /// <summary>
    /// Converts the current reverse-tow geometry into a fresh normal path.
    /// Each follower's current world position is seeded at its normal logical
    /// spacing, so returning to normal movement does not snap the chain.
    /// </summary>
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

        bool seeded = pathHistory.ResetHistoryFromCurrentChain(physicalJointProbe.ProbeTransform, followerPositions, firstFollowerSpacing, followerCartSpacing);

        if (!seeded)
        {
            Debug.LogError("[SnakeCartManager] Failed to seed normal path from recovered chain pose.", this);
            return false;
        }

        pathHistory.SetLeaderBody(leadingCartBody);

        // Fade the live physical-hinge rotation back in after recovery so C1
        // does not rotate abruptly on the first normal frame.
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

    #region Gameplay API

    public void TemporarilyDisableDetaching()
    {
        if (leadingCartRaycaster != null) leadingCartRaycaster.TemporarilyDisableDetaching();
    }

    public int GetSnakeBodyLength()
    {
        return snakeBody.Count;
    }

    public List<GameObject> GetSnakeBody()
    {
        return snakeBody;
    }

    public void TriggerAllPowerupsDelayed()
    {
        StartCoroutine(DelayedTrigger());
    }

    private IEnumerator DelayedTrigger()
    {
        yield return new WaitForSeconds(0.2f);

        foreach (GameObject cart in snakeBody)
        {
            ChainedCartManager cartManager = cart.GetComponent<ChainedCartManager>();

            if (cartManager != null && cartManager.isBonusCart)
            {
                GameObject powerupObject = cart.transform.GetChild(4).gameObject;
                IPowerup powerup = powerupObject.GetComponent<IPowerup>();
                if (powerup != null) powerup.ActivatePowerup();
            }
        }
    }

    #endregion

    #region Checkout / Grocery Items

    public int CheckOutNextCartWithItem()
    {
        if (snakeBody.Count <= 1) return snakeBody.Count;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            ChainedCartManager cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager == null || !cartManager.HasGroceryItem()) continue;

            bool isExpensiveItem = cartManager.isCarryingExpensiveGroceryItem();
            numOfCartsWithGroceryItem = Mathf.Max(0, numOfCartsWithGroceryItem - 1);

            if (cashScoreManager != null) cashScoreManager.RegisterItemCheckout(playerIndex, isExpensiveItem);
            if (sfxManager != null) sfxManager.PlaySFX("CheckoutSingle");

            GameObject removed = snakeBody[i];
            snakeBody.RemoveAt(i);
            Destroy(removed);

            return numOfCartsWithGroceryItem;
        }

        return numOfCartsWithGroceryItem;
    }

    public void CollectNormalGroceryItem()
    {
        if (cartsWithOutItem.Count < 1) return;

        numOfCartsWithGroceryItem++;
        int cartIndex = Random.Range(0, cartsWithOutItem.Count);

        ChainedCartManager cartManager = cartsWithOutItem[cartIndex].GetComponent<ChainedCartManager>();
        if (cartManager != null) cartManager.EnableNormalGroveryItem();

        cartsWithOutItem.RemoveAt(cartIndex);
    }

    public void CollectExpensiveGroceryItem()
    {
        if (cartsWithOutItem.Count < 1) return;

        numOfCartsWithGroceryItem++;
        int cartIndex = Random.Range(0, cartsWithOutItem.Count);

        ChainedCartManager cartManager = cartsWithOutItem[cartIndex].GetComponent<ChainedCartManager>();
        if (cartManager != null) cartManager.EnableExpensiveGroveryItem();

        cartsWithOutItem.RemoveAt(cartIndex);
    }

    public void IncreaseNumOfCartsWithItem()
    {
        numOfCartsWithGroceryItem++;
    }

    public int GetCurrentNumOfCartsWithItem()
    {
        return numOfCartsWithGroceryItem;
    }

    public bool HasEmptyCartForGroceryItem()
    {
        return cartsWithOutItem.Count > 0;
    }

    public void RemoveAllCartsWithItem()
    {
        if (sfxManager != null) sfxManager.PlaySFX("CheckoutCarts");

        for (int i = snakeBody.Count - 1; i >= 1; i--)
        {
            ChainedCartManager cartManager = snakeBody[i].GetComponent<ChainedCartManager>();

            if (cartManager != null && cartManager.HasGroceryItem())
            {
                numOfCartsWithGroceryItem--;

                GameObject removed = snakeBody[i];
                snakeBody.RemoveAt(i);
                Destroy(removed);
            }
        }
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
