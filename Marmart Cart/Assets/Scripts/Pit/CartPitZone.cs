using UnityEngine;

public class CartPitZone : MonoBehaviour
{
    [Tooltip("Direction from which the cart must enter")]
    [SerializeField] private Vector3 requiredEntryDirection;

    [Tooltip("Dot threshold to accept the direction. 1 = exact, 0 = 90 degrees.")]
    [SerializeField, Range(0f, 1f)] private float directionThreshold = 0.7f;

    [Tooltip("Allow player to be in ghost mode after quit checking out")]
    [SerializeField] private float ghostDurationAfterQuit = 3f;
    private CartControlScript enteredCartController;
    private LeadingCartRaycaster enteredCartRaycaster;
    private bool playerInPit = false;
    private bool isPlayer1 = false;
    [SerializeField] private GameObject p1Prompt;
    [SerializeField] private GameObject p2Prompt;

    [SerializeField] private CashScoreManager cashScoreManager;

    private CheckOutManager checkOutManager;

    [Header("Camera")]
    [SerializeField] private int myLaneNumber = 1; // 1 or 2
    [SerializeField] private PlayerCameraManager p1CameraManager;
    [SerializeField] private PlayerCameraManager p2CameraManager;

    void Start()
    {
        checkOutManager = this.GetComponent<CheckOutManager>();
        checkOutManager.SetMyPitZone(this);

        requiredEntryDirection = -1 * transform.forward;
    }
    private void OnTriggerEnter(Collider other)
    {
        enteredCartRaycaster = other.GetComponent<LeadingCartRaycaster>();
        // if no player in pit and collider is a leading cart, check incoming direction
        if (checkOutManager.IsStationAvailable() && enteredCartRaycaster != null && !playerInPit && enteredCartRaycaster.GetmySnakeCartManager().GetCurrentNumOfCartsWithItem() >= 1)
        {
            Vector3 incomingDirection = other.transform.forward;
            Vector3 requiredDirNormalized = requiredEntryDirection.normalized;

            float dot = Vector3.Dot(incomingDirection, requiredDirNormalized);
            // Comparing how closely aligned the player's entry direction is with the required direction:
            /*
             * Dot = 1 → perfectly aligned(angle = 0°)
             * Dot = 0 → perpendicular(angle = 90°)
             * Dot = -1 → opposite direction(angle = 180°)
            */
            if (dot >= directionThreshold)
            {
                // Automatically checkout all carts with items (Obsolete)
                //enteredCartRaycaster.GetmySnakeCartManager().RemoveAllCartsWithItem();
                //checkOutManager.QuitCheckout();
                // logic when player X successfully enters the pit
                playerInPit = true;
                if (other.gameObject.CompareTag("Player1"))
                {
                    isPlayer1 = true;
                    p1CameraManager.EnterCheckoutLane(myLaneNumber);
                    p1Prompt.SetActive(true);
                    cashScoreManager.StartCheckoutSession(1);
                }
                else
                {
                    isPlayer1 = false;
                    p2CameraManager.EnterCheckoutLane(myLaneNumber);
                    p2Prompt.SetActive(true);
                    cashScoreManager.StartCheckoutSession(2);
                }
                enteredCartController = other.GetComponentInChildren<CartControlScript>();
                if (enteredCartController != null)
                {
                    // Disable player input for turn and boost
                    enteredCartController.SetInPit();
                    enteredCartController.DisallowActivatePowerUp();
                    // Setup events
                    enteredCartController.SetActiveCheckoutHandler(checkOutManager);
                    checkOutManager.SetSnakeCartManager(enteredCartRaycaster.GetmySnakeCartManager());
                    checkOutManager.SetCartRaycaster(enteredCartRaycaster);
                    checkOutManager.SetIsCheckingOut();
                    checkOutManager.EnableStation();
                    // Stop the cart, set speed to zero
                    FreezeAllWheelBehavior(enteredCartRaycaster);
                    Invoke("FreezeAllWheelBehavior", 0.5f); // ensure wheels are frozen after physics update
                }
                else
                {
                    Debug.LogError("can't find cartcontrolscript");
                }
            }
            else
            {
                //Debug.Log("Cart entered pit from wrong direction.");
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        
    }

    public void ExitPitZone(LeadingCartRaycaster script)
    {
        if (playerInPit)
        {
            if(isPlayer1)
            {
                p1CameraManager.ExitCheckout();
                cashScoreManager.EndCheckoutSession(1);
            }
            else
            {
                p2CameraManager.ExitCheckout();
                cashScoreManager.EndCheckoutSession(2);
            }
            // might move to other place where handles player quit checking out carts
            playerInPit = false;
            // give players control
            enteredCartController.SetOutPit();
            //enteredCartController.AllowBoost();
            // reset speed
            UnfreezeAllWheelBehavior(script);
            // temporarily enter ghost mode
            script.SetInGhostModeWithTime(ghostDurationAfterQuit);
            // reset input listener
            enteredCartController.SetActiveCheckoutHandler(null);
            // reset prompot
            p1Prompt.SetActive(false);
            p2Prompt.SetActive(false);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw entry direction from pit center
        Gizmos.color = Color.green;
        Vector3 start = transform.position;
        Vector3 end = start + requiredEntryDirection.normalized * 20f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(start, 0.1f);
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    private void FreezeAllWheelBehavior(LeadingCartRaycaster enteredCartRaycaster)
    {
        LeadingCartBehaviour leadingCartBehaviour0 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour1 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour2 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour3 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

        leadingCartBehaviour0.SetSpeedToZero();
        leadingCartBehaviour1.SetSpeedToZero();
        leadingCartBehaviour2.SetSpeedToZero();
        leadingCartBehaviour3.SetSpeedToZero();
    }
    private void FreezeAllWheelBehavior()
    {
        if(enteredCartRaycaster == null)
        {
            return;
        }
        LeadingCartBehaviour leadingCartBehaviour0 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour1 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour2 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour3 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

        leadingCartBehaviour0.SetSpeedToZero();
        leadingCartBehaviour1.SetSpeedToZero();
        leadingCartBehaviour2.SetSpeedToZero();
        leadingCartBehaviour3.SetSpeedToZero();
    }
    private void UnfreezeAllWheelBehavior(LeadingCartRaycaster enteredCartRaycaster)
    {
        LeadingCartBehaviour leadingCartBehaviour0 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour1 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour2 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour3 = enteredCartRaycaster.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

        leadingCartBehaviour0.ResetSpeed();
        leadingCartBehaviour1.ResetSpeed();
        leadingCartBehaviour2.ResetSpeed();
        leadingCartBehaviour3.ResetSpeed();
    }
}
