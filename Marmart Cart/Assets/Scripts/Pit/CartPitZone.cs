using UnityEngine;

public class CartPitZone : MonoBehaviour
{
    [Header("Entry Direction")]
    [SerializeField] private Vector3 requiredEntryDirection;
    [SerializeField, Range(0f, 1f)] private float directionThreshold = 0.7f;

    [Header("Checkout")]
    [SerializeField] private float ghostDurationAfterQuit = 3f;
    [SerializeField] private int myLaneNumber = 1; // 1 or 2 (UI/camera lane)

    [Header("Per-Player UI (index 0..3 = P1..P4)")]
    [SerializeField] private GameObject[] playerPrompts = new GameObject[4];

    [Header("Per-Player Camera Managers (index 0..3 = P1..P4)")]
    [SerializeField] private PlayerCameraManager[] playerCameraManagers = new PlayerCameraManager[4];

    [Header("Refs")]
    [SerializeField] private CashScoreManager cashScoreManager;

    private CheckOutManager checkOutManager;

    // Runtime state
    private CartControlScript enteredCartController;
    private LeadingCartRaycaster enteredCartRaycaster;

    private bool stationOccupied = false;
    private int occupyingPlayerIndex = 0; // 1..4, 0 = none

    private void Start()
    {
        checkOutManager = GetComponent<CheckOutManager>();
        checkOutManager.SetMyPitZone(this);

        //requiredEntryDirection = transform.forward;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stationOccupied) return;
        if (!checkOutManager.IsStationAvailable()) return;

        enteredCartRaycaster = other.GetComponent<LeadingCartRaycaster>();
        if (enteredCartRaycaster == null) return;

        // Must have items to checkout
        if (enteredCartRaycaster.GetmySnakeCartManager().GetCurrentNumOfCartsWithItem() < 1)
            return;

        int playerIndex = TagToPlayerIndex(other.gameObject.tag);
        if (playerIndex <= 0) return;

        // Only allow players that exist in this mode (2P vs 4P)
        int activePlayers = (GMode.Instance != null) ? GMode.Instance.PlayerCount() : 2;
        if (playerIndex > activePlayers) return;

        // Direction gate
        Vector3 incomingDirection = other.transform.forward;
        Vector3 requiredDirNormalized = requiredEntryDirection.normalized;
        float dot = Vector3.Dot(incomingDirection, requiredDirNormalized);

        if (dot < directionThreshold) return;

        // ---- ENTER PIT SUCCESS ----
        stationOccupied = true;
        occupyingPlayerIndex = playerIndex;

        // camera + prompt
        var camMgr = GetPlayerCameraManager(playerIndex);
        camMgr?.EnterCheckoutLane(myLaneNumber);

        SetPrompt(playerIndex, true);

        // start checkout session
        if (cashScoreManager != null)
        {
            cashScoreManager.StartCheckoutSession(playerIndex, myLaneNumber - 1);
            cashScoreManager.ShowCheckoutUI(playerIndex, myLaneNumber, true);
        }

        enteredCartController = other.GetComponentInChildren<CartControlScript>();
        if (enteredCartController == null)
        {
            Debug.LogError("CartPitZone: can't find CartControlScript");
            return;
        }

        // Disable player abilities while in pit
        enteredCartController.SetInPit();
        enteredCartController.DisallowSpeedingUp();
        enteredCartController.DisallowActivatePowerUp();

        // Setup checkout manager links
        enteredCartController.SetActiveCheckoutHandler(checkOutManager);
        checkOutManager.SetSnakeCartManager(enteredCartRaycaster.GetmySnakeCartManager());
        checkOutManager.SetCartRaycaster(enteredCartRaycaster);
        checkOutManager.SetIsCheckingOut();
        checkOutManager.EnableStation();

        // Stop the cart
        FreezeAllWheelBehavior(enteredCartRaycaster);
        Invoke(nameof(FreezeAllWheelBehaviorDelayed), 0.5f);
    }

    public void ExitPitZone(LeadingCartRaycaster raycaster)
    {
        if (!stationOccupied) return;
        if (occupyingPlayerIndex <= 0) return;

        var camMgr = GetPlayerCameraManager(occupyingPlayerIndex);
        camMgr?.ExitCheckout();

        if (cashScoreManager != null)
        {
            cashScoreManager.EndCheckoutSession(occupyingPlayerIndex);
            cashScoreManager.ShowCheckoutUI(occupyingPlayerIndex, myLaneNumber, false);
        }

        // Restore control
        stationOccupied = false;

        if (enteredCartController != null)
        {
            enteredCartController.SetOutPit();
            enteredCartController.AllowSpeedingUp();
            enteredCartController.AllowActivatePowerUp();
            enteredCartController.SetActiveCheckoutHandler(null);
        }

        // Unfreeze wheels
        UnfreezeAllWheelBehavior(raycaster);

        // Ghost mode after leaving
        raycaster.SetInGhostModeWithTime(ghostDurationAfterQuit);

        // Prompts off
        SetPrompt(occupyingPlayerIndex, false);

        occupyingPlayerIndex = 0;
    }

    // ----------------- Helpers -----------------

    private void SetPrompt(int playerIndex, bool on)
    {
        int idx = playerIndex - 1;
        if (idx < 0 || idx >= playerPrompts.Length) return;

        if (playerPrompts[idx] != null)
            playerPrompts[idx].SetActive(on);

        // turn off others if you want only one visible
        if (on)
        {
            for (int i = 0; i < playerPrompts.Length; i++)
            {
                if (i == idx) continue;
                if (playerPrompts[i] != null) playerPrompts[i].SetActive(false);
            }
        }
    }

    private PlayerCameraManager GetPlayerCameraManager(int playerIndex)
    {
        int idx = playerIndex - 1;
        if (idx < 0 || idx >= playerCameraManagers.Length) return null;
        return playerCameraManagers[idx];
    }

    private int TagToPlayerIndex(string tag)
    {
        // expects Player1..Player4
        if (!tag.StartsWith("Player")) return 0;
        if (int.TryParse(tag.Substring(6), out int num))
            return num;
        return 0;
    }

    private void FreezeAllWheelBehavior(LeadingCartRaycaster raycaster)
    {
        if (raycaster == null) return;

        var behaviours = raycaster.GetComponentsInChildren<LeadingCartBehaviour>(true);
        foreach (var b in behaviours)
            b.SetSpeedToZero();
    }

    private void FreezeAllWheelBehaviorDelayed()
    {
        FreezeAllWheelBehavior(enteredCartRaycaster);
    }

    private void UnfreezeAllWheelBehavior(LeadingCartRaycaster raycaster)
    {
        if (raycaster == null) return;

        var behaviours = raycaster.GetComponentsInChildren<LeadingCartBehaviour>(true);
        foreach (var b in behaviours)
            b.ResetSpeed();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 start = transform.position;
        Vector3 end = start + requiredEntryDirection.normalized * 20f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(start, 0.1f);
    }

    //private void FreezeAllWheelBehavior(LeadingCartRaycaster enteredCartRaycaster)
    //{
    //    LeadingCartBehaviour leadingCartBehaviour0 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour1 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour2 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour3 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

    //    leadingCartBehaviour0.SetSpeedToZero();
    //    leadingCartBehaviour1.SetSpeedToZero();
    //    leadingCartBehaviour2.SetSpeedToZero();
    //    leadingCartBehaviour3.SetSpeedToZero();
    //}
    //private void FreezeAllWheelBehavior()
    //{
    //    if(enteredCartRaycaster == null)
    //    {
    //        return;
    //    }
    //    LeadingCartBehaviour leadingCartBehaviour0 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour1 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour2 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour3 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

    //    leadingCartBehaviour0.SetSpeedToZero();
    //    leadingCartBehaviour1.SetSpeedToZero();
    //    leadingCartBehaviour2.SetSpeedToZero();
    //    leadingCartBehaviour3.SetSpeedToZero();
    //}
    //private void UnfreezeAllWheelBehavior(LeadingCartRaycaster enteredCartRaycaster)
    //{
    //    LeadingCartBehaviour leadingCartBehaviour0 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour1 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour2 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
    //    LeadingCartBehaviour leadingCartBehaviour3 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

    //    leadingCartBehaviour0.ResetSpeed();
    //    leadingCartBehaviour1.ResetSpeed();
    //    leadingCartBehaviour2.ResetSpeed();
    //    leadingCartBehaviour3.ResetSpeed();
    //}
}
