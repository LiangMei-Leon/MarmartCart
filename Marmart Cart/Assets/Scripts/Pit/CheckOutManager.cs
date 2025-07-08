using UnityEngine;

public class CheckOutManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private SnakeCartManager enteredSnakeCartManager;
    [SerializeField] private LeadingCartRaycaster enteredCartRaycaster;
    [SerializeField] private CartPitZone myPitZone;
    [Header("State")]
    [SerializeField] private bool isCheckingOut = false;
    [SerializeField] private bool isStationAvailable = true;

    [SerializeField] private GameObject pitBlockers;
    private void Start()
    {
        if (pitBlockers == null)
        {
            Debug.LogError("pitBlocks not assigned");
        }
    }
    public void SetSnakeCartManager(SnakeCartManager script)
    {
        enteredSnakeCartManager = script;
    }
    public void SetMyPitZone(CartPitZone script)
    {
        myPitZone = script;
    }
    public void SetCartRaycaster(LeadingCartRaycaster script)
    {
        enteredCartRaycaster = script;
    }
    public void TryCheckoutCart()
    {
        if (!isStationAvailable || enteredSnakeCartManager.GetSnakeBodyLength() <= 1)
        {
            QuitCheckout();
            return;
        }

        int newLength = enteredSnakeCartManager.CheckOutFirstChainedCart();
        if (newLength <= 1)
        {
            QuitCheckout();
        }
    }
    public void QuitCheckout()
    {
        myPitZone.ExitPitZone(enteredCartRaycaster);
        isCheckingOut = false;
        Invoke(nameof(DisableStation), 1f);
    }
    public void EnableStation()
    {
        isStationAvailable = true;
        pitBlockers.SetActive(false);
    }
    public void DisableStation()
    {
        isStationAvailable = false;
        if(isCheckingOut)
        {
            QuitCheckout();
            Invoke(nameof(DisableStation), 1f);
        }
        else
        {
            pitBlockers.SetActive(true);
        }
    }
    public bool IsStationAvailable()
    {
        return isStationAvailable;
    }
    public void SetIsCheckingOut()
    {
        isCheckingOut = true;
    }
}
