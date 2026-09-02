using UnityEngine;

/// <summary>
/// Checkout lane entry/exit zone for the refactored cart architecture.
///
/// Player ownership is resolved through SnakeCartManager.
/// Only the player's LEADING cart may enter the checkout lane; collected
/// followers cannot trigger checkout even though they share the same
/// SnakeCartManager parent.
///
/// LeadingCartRaycaster is no longer used.
/// </summary>
[DisallowMultipleComponent]
public class CartPitZone : MonoBehaviour
{
    private const int MaxSupportedPlayers = 4;

    #region Entry Direction

    [Header("Entry Direction")]
    [SerializeField] private Vector3 requiredEntryDirection;

    [SerializeField, Range(0f, 1f)]
    private float directionThreshold = 0.7f;

    #endregion

    #region Checkout

    [Header("Checkout")]
    [Min(0f)]
    [SerializeField] private float ghostDurationAfterQuit = 3f;

    [Tooltip("1 or 2. Used by checkout UI/camera lane logic.")]
    [SerializeField] private int myLaneNumber = 1;

    #endregion

    #region Player UI / Cameras

    [Header("Per-Player UI (index 0..3 = P1..P4)")]
    [SerializeField] private GameObject[] playerPrompts = new GameObject[MaxSupportedPlayers];

    [Header("Per-Player Camera Managers (index 0..3 = P1..P4)")]
    [SerializeField] private PlayerCameraManager[] playerCameraManagers = new PlayerCameraManager[MaxSupportedPlayers];

    #endregion

    #region References

    [Header("Refs")]
    [SerializeField] private CashScoreManager cashScoreManager;

    private CheckOutManager checkOutManager;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool stationOccupied;
    [SerializeField] private int occupyingPlayerIndex;

    private SnakeCartManager enteredSnakeCartManager;
    private GameObject enteredLeader;
    private Rigidbody enteredLeaderBody;
    private CartControlScript enteredCartController;
    private LeadingCartBehaviour[] enteredWheelBehaviours;
    private LeadingCartBattleController enteredBattleController;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        checkOutManager = GetComponent<CheckOutManager>();

        if (checkOutManager == null)
        {
            Debug.LogError("[CartPitZone] CheckOutManager is missing on this checkout station.", this);
        }
    }

    private void Start()
    {
        if (checkOutManager != null) checkOutManager.SetMyPitZone(this);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(FreezeAllWheelBehaviorDelayed));
    }

    #endregion

    #region Pit Entry

    private void OnTriggerEnter(Collider other)
    {
        if (stationOccupied || checkOutManager == null || !checkOutManager.IsStationAvailable()) return;
        if (!TryResolveLeadingCart(other, out SnakeCartManager snakeManager, out GameObject leader)) return;

        // Player must actually have grocery carts to checkout.
        if (snakeManager.GetCurrentNumOfCartsWithItem() < 1) return;

        int playerIndex = snakeManager.GetPlayerId();
        if (playerIndex < 1 || playerIndex > MaxSupportedPlayers) return;

        int activePlayers = GMode.Instance != null ? GMode.Instance.PlayerCount() : 2;
        if (playerIndex > activePlayers) return;

        if (!PassesEntryDirection(leader.transform)) return;

        CartControlScript cartControl = leader.GetComponentInChildren<CartControlScript>(true);
        LeadingCartBehaviour[] wheelBehaviours = leader.GetComponentsInChildren<LeadingCartBehaviour>(true);
        LeadingCartBattleController battleController = leader.GetComponentInChildren<LeadingCartBattleController>(true);
        Rigidbody leaderBody = leader.GetComponent<Rigidbody>();

        if (cartControl == null)
        {
            Debug.LogError("[CartPitZone] Leading cart is missing CartControlScript.", leader);
            return;
        }

        if (wheelBehaviours == null || wheelBehaviours.Length == 0)
        {
            Debug.LogError("[CartPitZone] Leading cart has no LeadingCartBehaviour wheel components.", leader);
            return;
        }

        // Commit checkout state only after every required reference is valid.
        stationOccupied = true;
        occupyingPlayerIndex = playerIndex;

        enteredSnakeCartManager = snakeManager;
        enteredLeader = leader;
        enteredLeaderBody = leaderBody;
        enteredCartController = cartControl;
        enteredWheelBehaviours = wheelBehaviours;
        enteredBattleController = battleController;

        EnterCheckout();
    }

    private bool TryResolveLeadingCart(Collider other, out SnakeCartManager snakeManager, out GameObject leader)
    {
        snakeManager = null;
        leader = null;

        if (other == null) return false;

        snakeManager = other.GetComponentInParent<SnakeCartManager>();
        if (snakeManager == null) return false;

        var snakeBody = snakeManager.GetSnakeBody();
        if (snakeBody == null || snakeBody.Count == 0 || snakeBody[0] == null) return false;

        leader = snakeBody[0];

        // New leading cart prefab has the Rigidbody on its root.
        Rigidbody leaderBody = leader.GetComponent<Rigidbody>();

        // Primary check: physical collider belongs to the leader Rigidbody.
        if (leaderBody != null && other.attachedRigidbody == leaderBody) return true;

        // Fallback for a trigger/child collider with no attached Rigidbody.
        return other.transform == leader.transform || other.transform.IsChildOf(leader.transform);
    }

    private bool PassesEntryDirection(Transform leaderTransform)
    {
        if (leaderTransform == null) return false;

        Vector3 requiredDirection = Vector3.ProjectOnPlane(requiredEntryDirection, Vector3.up);

        // If not configured, use this checkout zone's forward direction.
        if (requiredDirection.sqrMagnitude < 0.0001f)
        {
            requiredDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        Vector3 incomingDirection = Vector3.ProjectOnPlane(leaderTransform.forward, Vector3.up);

        if (requiredDirection.sqrMagnitude < 0.0001f || incomingDirection.sqrMagnitude < 0.0001f) return false;

        requiredDirection.Normalize();
        incomingDirection.Normalize();

        return Vector3.Dot(incomingDirection, requiredDirection) >= directionThreshold;
    }

    private void EnterCheckout()
    {
        PlayerCameraManager cameraManager = GetPlayerCameraManager(occupyingPlayerIndex);
        cameraManager?.EnterCheckoutLane(myLaneNumber);

        SetPrompt(occupyingPlayerIndex, true);

        if (cashScoreManager != null)
        {
            cashScoreManager.StartCheckoutSession(occupyingPlayerIndex, myLaneNumber - 1);
            cashScoreManager.ShowCheckoutUI(occupyingPlayerIndex, myLaneNumber, true);
        }

        enteredCartController.SetInPit();
        enteredCartController.DisallowSpeedingUp();
        enteredCartController.DisallowActivatePowerUp();
        enteredCartController.SetActiveCheckoutHandler(checkOutManager);

        checkOutManager.SetSnakeCartManager(enteredSnakeCartManager);
        checkOutManager.SetIsCheckingOut();
        checkOutManager.EnableStation();

        FreezeAllWheelBehavior();

        CancelInvoke(nameof(FreezeAllWheelBehaviorDelayed));
        Invoke(nameof(FreezeAllWheelBehaviorDelayed), 0.5f);
    }

    #endregion

    #region Pit Exit

    public void ExitPitZone()
    {
        if (!stationOccupied || occupyingPlayerIndex <= 0) return;

        CancelInvoke(nameof(FreezeAllWheelBehaviorDelayed));

        int exitingPlayerIndex = occupyingPlayerIndex;

        PlayerCameraManager cameraManager = GetPlayerCameraManager(exitingPlayerIndex);
        cameraManager?.ExitCheckout();

        if (cashScoreManager != null)
        {
            cashScoreManager.EndCheckoutSession(exitingPlayerIndex);
            cashScoreManager.ShowCheckoutUI(exitingPlayerIndex, myLaneNumber, false);
        }

        if (enteredCartController != null)
        {
            enteredCartController.SetOutPit();
            enteredCartController.AllowSpeedingUp();
            enteredCartController.AllowActivatePowerUp();
            enteredCartController.SetActiveCheckoutHandler(null);
        }

        UnfreezeAllWheelBehavior();

        // New battle/ghost architecture replaces LeadingCartRaycaster ghost mode.
        if (enteredBattleController != null && ghostDurationAfterQuit > 0f)
        {
            enteredBattleController.SetGhostMode(ghostDurationAfterQuit);
        }

        SetPrompt(exitingPlayerIndex, false);

        ClearRuntimeCheckoutState();
    }

    private void ClearRuntimeCheckoutState()
    {
        stationOccupied = false;
        occupyingPlayerIndex = 0;

        enteredSnakeCartManager = null;
        enteredLeader = null;
        enteredLeaderBody = null;
        enteredCartController = null;
        enteredWheelBehaviours = null;
        enteredBattleController = null;
    }

    #endregion

    #region Wheel Control

    private void FreezeAllWheelBehavior()
    {
        if (enteredWheelBehaviours == null) return;

        for (int i = 0; i < enteredWheelBehaviours.Length; i++)
        {
            if (enteredWheelBehaviours[i] != null) enteredWheelBehaviours[i].SetSpeedToZero();
        }

        // Remove remaining planar momentum so the new Rigidbody-root leader
        // does not coast through the checkout lane after wheel drive is stopped.
        if (enteredLeaderBody != null)
        {
            Vector3 velocity = enteredLeaderBody.linearVelocity;
            enteredLeaderBody.linearVelocity = Vector3.up * velocity.y;
        }
    }

    private void FreezeAllWheelBehaviorDelayed()
    {
        if (!stationOccupied) return;
        FreezeAllWheelBehavior();
    }

    private void UnfreezeAllWheelBehavior()
    {
        if (enteredWheelBehaviours == null) return;

        for (int i = 0; i < enteredWheelBehaviours.Length; i++)
        {
            if (enteredWheelBehaviours[i] != null) enteredWheelBehaviours[i].ResetSpeed();
        }
    }

    #endregion

    #region UI / Camera Helpers

    private void SetPrompt(int playerIndex, bool visible)
    {
        int index = playerIndex - 1;

        if (playerPrompts == null || index < 0 || index >= playerPrompts.Length) return;

        if (playerPrompts[index] != null) playerPrompts[index].SetActive(visible);

        if (!visible) return;

        for (int i = 0; i < playerPrompts.Length; i++)
        {
            if (i == index) continue;
            if (playerPrompts[i] != null) playerPrompts[i].SetActive(false);
        }
    }

    private PlayerCameraManager GetPlayerCameraManager(int playerIndex)
    {
        int index = playerIndex - 1;

        if (playerCameraManagers == null || index < 0 || index >= playerCameraManagers.Length) return null;

        return playerCameraManagers[index];
    }

    #endregion

    #region Gizmos / Validation

    private void OnValidate()
    {
        myLaneNumber = Mathf.Max(1, myLaneNumber);
        ghostDurationAfterQuit = Mathf.Max(0f, ghostDurationAfterQuit);
    }

    private void OnDrawGizmos()
    {
        Vector3 direction = Vector3.ProjectOnPlane(requiredEntryDirection, Vector3.up);

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + direction * 20f);
        Gizmos.DrawSphere(transform.position, 0.1f);
    }

    #endregion
}
