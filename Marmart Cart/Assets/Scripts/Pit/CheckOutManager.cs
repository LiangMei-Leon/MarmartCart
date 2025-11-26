using System.Collections;
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
    [Header("Checkout Timer")]
    [SerializeField] private float checkoutTimeLimit = 20f; // seconds allowed in lane
    private Coroutine checkoutTimerRoutine;

    //[SerializeField] private GameObject pitInavailableIndictor;
    private void Start()
    {
        //if (pitInavailableIndictor == null)
        //{
        //    Debug.LogError("pitBlocks not assigned");
        //}
        EnableStation();
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

        int cartWithItemLeft = enteredSnakeCartManager.CheckOutNextCartWithItem();
        if (cartWithItemLeft <= 0)
        {
            QuitCheckout();
        }
    }
    public void QuitCheckout()
    {
        isCheckingOut = false;

        if (checkoutTimerRoutine != null)
        {
            StopCoroutine(checkoutTimerRoutine);
            checkoutTimerRoutine = null;
        }

        myPitZone.ExitPitZone(enteredCartRaycaster);
    }
    public void EnableStation()
    {
        isStationAvailable = true;
        //pitInavailableIndictor.SetActive(false);
    }
    //public void DisableStation()
    //{
    //    isStationAvailable = false;
    //    if(isCheckingOut)
    //    {
    //        QuitCheckout();
    //        Invoke(nameof(DisableStation), 1f);
    //    }
    //    else
    //    {
    //        pitInavailableIndictor.SetActive(true);
    //    }
    //}
    public bool IsStationAvailable()
    {
        return isStationAvailable;
    }
    public void SetIsCheckingOut()
    {
        isCheckingOut = true;
        // Start / restart checkout timer
        if (checkoutTimerRoutine != null)
            StopCoroutine(checkoutTimerRoutine);

        checkoutTimerRoutine = StartCoroutine(CheckoutTimerRoutine());
    }
    private IEnumerator CheckoutTimerRoutine()
    {
        float timer = checkoutTimeLimit;

        while (timer > 0f && isCheckingOut)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        // Time ran out while still checking out → kick player
        if (isCheckingOut)
        {
            QuitCheckout();
        }
    }
}
