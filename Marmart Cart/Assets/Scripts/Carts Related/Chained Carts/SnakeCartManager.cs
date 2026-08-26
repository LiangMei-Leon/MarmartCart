using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeCartManager : MonoBehaviour, IAssistPlayerDataSource
{
    [Header("Distance Path Prototype")]
    [SerializeField]
    private SnakePathHistory pathHistory;

    [SerializeField]
    private bool useDistancePathForAllFollowers = true;

    [Tooltip(
        "Distance along the probe path from the physical probe " +
        "to the center of the first chained cart.")]
    [Min(0.1f)]
    [SerializeField]
    private float firstFollowerSpacing = 1.0f;

    [Tooltip(
        "Center-to-center path distance between later chained carts.")]
    [Min(0.1f)]
    [SerializeField]
    private float followerCartSpacing = 1.5f;


    [Header("Follower Rotation")]

    [Tooltip(
        "How strongly Cart 1 uses the physical hinge orientation " +
        "instead of the local path tangent.")]
    [UnityEngine.Range(0f, 1f)]
    [SerializeField]
    private float firstFollowerHingeInfluence = 0.7f;

    [Tooltip(
        "How much hinge influence is removed for every cart farther " +
        "back in the chain.")]
    [UnityEngine.Range(0f, 1f)]
    [SerializeField]
    private float hingeInfluenceFalloffPerCart = 0.2f;

    [Header("Move Backward")]
    [SerializeField] private SnakeMoveBackwardController moveBackwardController;

    [Header("Distance Path Debug")]
    [SerializeField]
    private bool drawFirstFollowerTarget = true;
    private bool firstFollowerUsedDistancePathThisTick;
    private bool hasFirstFollowerDebugTarget;
    private Vector3 firstFollowerDebugTarget;
    private MarkerManager leadingMarkerManager;
    private bool disabledLegacyLeaderMarker;

    [SerializeField] float distanceBetween = 0.2f; // The spawn rate time difference that creates an illusion of distance in between snake bodies
    //[SerializeField] float cartSpacing = 1f; // world space units

    [SerializeField] List<GameObject> bodyParts = new List<GameObject>();
    [SerializeField] List<GameObject> snakeBody = new List<GameObject>();
    [SerializeField] List<GameObject> cartsWithOutItem = new List<GameObject>();

    LeadingCartRaycaster LeadingCartRaycaster;
    [Header("Player")]
    [UnityEngine.Range(1, 4)]
    [SerializeField] private int playerIndex = 1; // 1..4

    [Header("Related Events")]
    [SerializeField] GameEvent setupCamera;

    float countUp = 0;

    [Header("PlayerInputManager")]
    [SerializeField] private PlayerInputManager playerInputManager;

    [SerializeField] private CashScoreManager cashScoreManager;
    //[SerializeField] private ComboDealsManager comboDealsManager;

    public bool needScaleup = false;

    [SerializeField] private int numOfCartsWithGroceryItem = 0;
    [SerializeField] private SfxManager sfxManager;

    [Header("Physical Joint Test")]
    [SerializeField]
    private PhysicalChainJointProbe physicalJointProbe;
    [Header("First Follower Rotation")]
    [UnityEngine.Range(0f, 1f)]
    [SerializeField]
    private float hingeRotationInfluence = 1f;
    private void Awake()
    {
        if (pathHistory == null)
            pathHistory = GetComponent<SnakePathHistory>();

        if (pathHistory == null)
            pathHistory = gameObject.AddComponent<SnakePathHistory>();

        if (physicalJointProbe == null)
        {
            physicalJointProbe = GetComponent<PhysicalChainJointProbe>();
        }

        if (moveBackwardController == null) moveBackwardController = GetComponent<SnakeMoveBackwardController>();
        if (moveBackwardController == null) moveBackwardController = gameObject.AddComponent<SnakeMoveBackwardController>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CreateBodyParts();
    }
    private void Update()
    {
        
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        ManageSnakeBody();

        bool bIsMovingBackward = moveBackwardController != null && moveBackwardController.TickMoveBackward();

        if (!bIsMovingBackward && pathHistory != null && pathHistory.IsInitialized)
            pathHistory.TickHistory();

        UpdateFollowerScale();

        if (useDistancePathForAllFollowers)
            MoveAllFollowersUsingDistancePath();
        else
            SnakeMovement();
    }

    void SnakeMovement()
    {
        if (snakeBody.Count <= 1)
            return;

        // -----------------------------------------
        // Scaling still applies to ALL followers,
        // regardless of which movement system they use.
        // -----------------------------------------

        for (
            int i = 1;
            i < snakeBody.Count;
            i++)
        {
            if (snakeBody[i] == null)
                continue;

            if (needScaleup)
            {
                snakeBody[i].transform.localScale =
                    new Vector3(
                        10f,
                        10f,
                        10f
                    );
            }
            else
            {
                snakeBody[i].transform.localScale =
                    new Vector3(
                        5f,
                        5f,
                        5f
                    );
            }
        }

        // -----------------------------------------
        // If Cart 1 successfully used the new system,
        // legacy movement begins with Cart 2.
        //
        // Otherwise fall back to old behavior entirely.
        // -----------------------------------------

        int legacyStartIndex =
            firstFollowerUsedDistancePathThisTick
                ? 2
                : 1;

        for (
            int i = legacyStartIndex;
            i < snakeBody.Count;
            i++)
        {
            MarkerManager markM =
                snakeBody[i - 1]
                    .GetComponent<MarkerManager>();

            if (markM == null ||
                markM.markerList.Count == 0)
            {
                continue;
            }

            snakeBody[i].transform.position =
                markM.markerList[0].position;

            snakeBody[i].transform.rotation =
                markM.markerList[0].rotation;

            markM.markerList.RemoveAt(0);
        }
    }
    private bool TryGetDistancePathTarget(
    int snakeIndex,
    out Vector3 targetPosition,
    out Quaternion targetRotation)
    {
        targetPosition =
            Vector3.zero;

        targetRotation =
            Quaternion.identity;

        // Index 0 is the leader.
        if (snakeIndex <= 0)
            return false;

        if (pathHistory == null ||
            !pathHistory.IsInitialized)
        {
            return false;
        }

        // ==================================================
        // DISTANCE BEHIND THE PHYSICAL PROBE
        //
        // Cart 1:
        //     firstFollowerSpacing
        //
        // Cart 2:
        //     firstFollowerSpacing + followerCartSpacing
        //
        // Cart 3:
        //     firstFollowerSpacing + followerCartSpacing * 2
        // ==================================================

        int followerIndex =
            snakeIndex - 1;

        float distanceBehindProbe =
            firstFollowerSpacing +
            followerIndex *
            followerCartSpacing;

        float targetProgress =
            pathHistory.HeadProgress -
            distanceBehindProbe;

        // ==================================================
        // PATH POSITION + PATH TANGENT ROTATION
        // ==================================================

        if (!pathHistory.TryGetPoseAtProgress(
                targetProgress,
                out targetPosition,
                out Quaternion pathRotation))
        {
            return false;
        }

        targetRotation =
            pathRotation;

        // ==================================================
        // PHYSICAL HINGE ROTATION
        //
        // Most important at the front.
        // Gradually fades farther down the chain.
        // ==================================================

        if (physicalJointProbe != null &&
            physicalJointProbe.ProbeTransform != null)
        {
            Vector3 hingeForward =
                physicalJointProbe
                    .ProbeTransform
                    .forward;

            hingeForward =
                Vector3.ProjectOnPlane(
                    hingeForward,
                    Vector3.up
                );

            if (hingeForward.sqrMagnitude >
                0.0001f)
            {
                Quaternion hingeRotation =
                    Quaternion.LookRotation(
                        hingeForward.normalized,
                        Vector3.up
                    );

                float hingeInfluence =
                    Mathf.Clamp01(
                        firstFollowerHingeInfluence -
                        followerIndex *
                        hingeInfluenceFalloffPerCart
                    );

                targetRotation =
                    Quaternion.Slerp(
                        pathRotation,
                        hingeRotation,
                        hingeInfluence
                    );
            }
        }

        return true;
    }
    void CreateBodyParts()
    {
        if (snakeBody.Count == 0)
        {
            GameObject tempCartInstance = Instantiate(bodyParts[0], transform.position, transform.rotation, transform);
            tempCartInstance.tag = GetPlayerTag(); // Set tag to the attached carts based on player index
            // Ensure MarkerManager is added
            if (!tempCartInstance.GetComponent<MarkerManager>())
            {
                tempCartInstance.AddComponent<MarkerManager>();
            }

            // Set as collected by the player
            var cartManager = tempCartInstance.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                cartManager.CollectByPlayer();
            }

            snakeBody.Add(tempCartInstance);
            LeadingCartRaycaster = tempCartInstance.GetComponent<LeadingCartRaycaster>();
            // Cache old marker system on the leader.
            leadingMarkerManager = tempCartInstance.GetComponent<MarkerManager>();

            // Find the authoritative movement Rigidbody.
            LeadingCartBehaviour leadingMovement = tempCartInstance.GetComponentInChildren<LeadingCartBehaviour>();
            Rigidbody leadingBody = leadingMovement != null ? leadingMovement.CartBody : null;
            CartControlScript leadingControl = tempCartInstance.GetComponentInChildren<CartControlScript>();
            Transform rearHitch = tempCartInstance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RearHitch");

            if (rearHitch != null)
            {
                if (physicalJointProbe != null)
                {
                    physicalJointProbe.Initialize(rearHitch);
                }
            }
            else
            {
                Debug.LogError("[SnakeCartManager] Could not find RearHitch " + "on leading cart prefab.", tempCartInstance);
            }

            if (physicalJointProbe.ProbeTransform != null)
            {
                pathHistory.Initialize(physicalJointProbe.ProbeTransform);
            }
            else
            {
                Debug.LogError("[SnakeCartManager] Physical probe did not create a ProbeTransform.", this);
            }
            if (leadingBody != null && leadingMovement != null && leadingControl != null)
            {
                moveBackwardController.Initialize(leadingBody, leadingMovement, leadingControl, pathHistory);
            }
            else
            {
                Debug.LogError("[SnakeCartManager] MoveBackward system could not initialize.", tempCartInstance);
            }
            setupCamera.Raise();
            bodyParts.RemoveAt(0);
            return;
        }

        MarkerManager markM = snakeBody[snakeBody.Count - 1].GetComponent<MarkerManager>();
        if (countUp == 0)
        {
            markM.ClearMarkerList();
        }
        countUp += Time.deltaTime;
        if (countUp >= distanceBetween)
        {
            int newSnakeIndex = snakeBody.Count;

            if (!TryGetDistancePathTarget(newSnakeIndex, out Vector3 spawnPosition, out Quaternion spawnRotation))
            {
                return;
            }

            GameObject tempCartInstance = Instantiate(bodyParts[0],spawnPosition,spawnRotation,transform);

            tempCartInstance.tag = GetPlayerTag();

            if (!tempCartInstance.GetComponent<MarkerManager>())
            {
                tempCartInstance.AddComponent<MarkerManager>();
            }

            var cartManager = tempCartInstance.GetComponent<ChainedCartManager>();

            if (cartManager != null)
            {
                cartManager.CollectByPlayer();
                cartManager.SetCartTeamColor();
            }

            snakeBody.Add(
                tempCartInstance
            );

            if (cartManager != null &&
                !cartManager.HasGroceryItem())
            {
                cartsWithOutItem.Add(
                    tempCartInstance
                );
            }

            bodyParts.RemoveAt(0);

            countUp = 0f;
        }
    }

    void ManageSnakeBody()
    {
        if (bodyParts.Count > 0)
        {
            CreateBodyParts();
        }
        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null)
            {
                snakeBody.RemoveAt(i);
                i = i - 1;
                break;
            }

            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager == null)
            {
                Debug.LogError("No Chained Cart Manager Component on " + snakeBody[i].name);
                continue;
            }

            // If this cart is no longer collected by the player
            if (!cartManager.isCollectedByPlayer)
            {
                // Detach this cart and all subsequent carts
                for (int j = i; j < snakeBody.Count; j++)
                {
                    if(snakeBody[j].GetComponent<ChainedCartManager>().HasGroceryItem())
                    {
                        numOfCartsWithGroceryItem--;
                   
                    }
                    snakeBody[j].transform.localScale = new Vector3(5f, 5f, 5f);
                    snakeBody[j].transform.SetParent(null); // Detach from parent
                    snakeBody[j].GetComponent<ChainedCartManager>().OnDetach();
                    cartsWithOutItem.Remove(snakeBody[j]);
                }

                // Remove all subsequent carts from the list
                snakeBody.RemoveRange(i, snakeBody.Count - i);

                break; // Exit the loop as we've detached all necessary carts
            }
        }

        // If no carts are left, destroy this script
        if (snakeBody.Count == 0)
        {
            Destroy(this);
        }
    }

    public void AddBodyParts(GameObject addedObj)
    {
        bodyParts.Add(addedObj);
        StartCoroutine(DelayedPlayVFX());
    }
    private void MoveAllFollowersUsingDistancePath()
    {
        if (!useDistancePathForAllFollowers)
            return;

        if (pathHistory == null ||
            !pathHistory.IsInitialized)
        {
            return;
        }

        // Index 0 = leader.
        for (
            int i = 1;
            i < snakeBody.Count;
            i++)
        {
            GameObject cart =
                snakeBody[i];

            if (cart == null)
                continue;

            if (!TryGetDistancePathTarget(
                    i,
                    out Vector3 targetPosition,
                    out Quaternion targetRotation))
            {
                continue;
            }

            cart.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation
            );
        }
    }
    private void UpdateFollowerScale()
    {
        for (
            int i = 1;
            i < snakeBody.Count;
            i++)
        {
            if (snakeBody[i] == null)
                continue;

            if (needScaleup)
            {
                snakeBody[i].transform.localScale =new Vector3(10f,10f,10f);
            }
            else
            {
                snakeBody[i].transform.localScale = new Vector3(6f,6f,6f);
            }
        }
    }
    private void UpdateLegacyLeaderMarkerState()
    {
        if (leadingMarkerManager == null &&
            snakeBody.Count > 0 &&
            snakeBody[0] != null)
        {
            leadingMarkerManager =
                snakeBody[0]
                    .GetComponent<MarkerManager>();
        }

        if (leadingMarkerManager == null)
            return;

        bool shouldDisableLegacyLeaderMarker = snakeBody.Count > 1;

        // -----------------------------------------
        // Turn old leader recording off once
        // Cart 1 has migrated to the new path.
        // -----------------------------------------

        if (shouldDisableLegacyLeaderMarker &&
            !disabledLegacyLeaderMarker)
        {
            leadingMarkerManager.ClearMarkerList();

            leadingMarkerManager.enabled =
                false;

            disabledLegacyLeaderMarker =
                true;

            return;
        }

        // -----------------------------------------
        // Re-enable if we switch the prototype off
        // or lose Cart 1.
        // -----------------------------------------

        if (!shouldDisableLegacyLeaderMarker &&
            disabledLegacyLeaderMarker)
        {
            leadingMarkerManager.enabled =
                true;

            leadingMarkerManager.ClearMarkerList();

            disabledLegacyLeaderMarker =
                false;
        }
    }
    private IEnumerator DelayedPlayVFX()
    {
        // Wait for 0.1 seconds
        yield return new WaitForSeconds(0.12f);

        // Ensure the snakeBody list has elements
        if (snakeBody.Count > 0)
        {
            // Reference the last object in the snakeBody list
            var lastCart = snakeBody[snakeBody.Count - 1];
            var cartManager = lastCart.GetComponent<ChainedCartManager>();

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
        else
        {
            Debug.LogError("SnakeBody list is empty. No VFX to play.");
        }
    }

    public void TemporarilyDisableDetaching()
    {
        LeadingCartRaycaster.TemporarilyDisableDetaching();
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
        yield return new WaitForSeconds(0.2f); // Delay

        foreach (var cart in this.GetSnakeBody())
        {
            var cartManager = cart.GetComponent<ChainedCartManager>();
            if (cartManager != null && cartManager.isBonusCart)
            {
                GameObject powerupObject = cart.transform.GetChild(4).gameObject;
                //Debug.Log(powerupObject.name);
                var powerup = powerupObject.GetComponent<IPowerup>();
                if (powerup != null)
                {
                    //Debug.Log("Fire");
                    powerup.ActivatePowerup();
                }
            }
        }
    }
    public int CheckOutNextCartWithItem()
    {
        // No chained carts, nothing to do
        if (snakeBody.Count <= 1)
            return snakeBody.Count;

        int pIndex = playerIndex;

        // Start from 1 to skip the leading cart
        for (int i = 1; i < snakeBody.Count; i++)
        {
            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager == null || !cartManager.HasGroceryItem())
                continue;

            // Get rarity from this cart
            bool isExpensiveItem = cartManager.isCarryingExpensiveGroceryItem();

            // Update internal counters
            numOfCartsWithGroceryItem = Mathf.Max(0, numOfCartsWithGroceryItem - 1);

            // Submit to cash score manager and handle combo streak there
            if (cashScoreManager != null)
            {
                cashScoreManager.RegisterItemCheckout(pIndex, isExpensiveItem);
            }

            // Play checkout SFX
            if (sfxManager != null)
            {
                sfxManager.PlaySFX("CheckoutSingle"); // sfx for each item checkout
            }

            // Remove this cart from the snake and destroy it
            GameObject removed = snakeBody[i];
            snakeBody.RemoveAt(i);
            Destroy(removed);

            // Return remaining number of carts in snake
            return numOfCartsWithGroceryItem;
        }

        // If we reach here, there was no cart with a grocery item
        return numOfCartsWithGroceryItem;
    }
    public void CollectNormalGroceryItem()
    {
        if (cartsWithOutItem.Count >= 1)
        {
            numOfCartsWithGroceryItem++;
            // Give a random cart (excluding leading cart) a grocery item
            int cartIndex = Random.Range(0, cartsWithOutItem.Count);
            ChainedCartManager cartManager = cartsWithOutItem[cartIndex].GetComponent<ChainedCartManager>();
            cartManager.EnableNormalGroveryItem();
            cartsWithOutItem.RemoveAt(cartIndex); // Remove from list to avoid duplicate assignment
        }
        else
        {
            // All carts already have grocery items.
            // Debug.Log("All carts already have grocery items.");
        }
    }
    public void CollectExpensiveGroceryItem()
    {
        if (cartsWithOutItem.Count >= 1)
        {
            numOfCartsWithGroceryItem++;
            // Give a random cart (excluding leading cart) a grocery item
            int cartIndex = Random.Range(0, cartsWithOutItem.Count);
            ChainedCartManager cartManager = cartsWithOutItem[cartIndex].GetComponent<ChainedCartManager>();
            cartManager.EnableExpensiveGroveryItem();
            cartsWithOutItem.RemoveAt(cartIndex); // Remove from list to avoid duplicate assignment
        }
        else
        {
            // All carts already have grocery items.
            // Debug.Log("All carts already have grocery items.");
        }
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
        sfxManager.PlaySFX("CheckoutCarts");
        for (int i = snakeBody.Count - 1; i >= 1; i--) // iterate backward, skip leading cart
        {
            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager != null && cartManager.HasGroceryItem())
            {
                numOfCartsWithGroceryItem--;
                GameObject removed = snakeBody[i];
                snakeBody.RemoveAt(i); // use RemoveAt for clarity
                Destroy(removed);
            }
        }
    }
    private string GetPlayerTag()
    {
        // Make sure these tags exist in Unity Tag Manager:
        // Player1, Player2, Player3, Player4
        return $"Player{playerIndex}";
    }

    /// IAssistPlayerDataSource implementation for MatchBalanceManager
    public int GetPlayerId()
    {
        return playerIndex;
    }

    public int GetCurrentScore()
    {
        return cashScoreManager.GetPlayerScore(playerIndex);
    }

    public int GetCurrentCartCount()
    {
        return snakeBody.Count;
    }
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        if (!drawFirstFollowerTarget)
            return;

        if (!hasFirstFollowerDebugTarget)
            return;

        Gizmos.color =
            Color.magenta;

        Gizmos.DrawSphere(
            firstFollowerDebugTarget,
            0.15f
        );
    }
}