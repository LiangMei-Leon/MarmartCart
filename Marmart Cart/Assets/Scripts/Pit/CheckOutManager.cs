using System.Collections;
using UnityEngine;

/// <summary>
/// Controls one checkout station session.
///
/// LeadingCartRaycaster has been completely removed.
/// CartPitZone owns the entered leading-cart references and this manager only
/// needs the player's SnakeCartManager to process checkout carts.
/// </summary>
[DisallowMultipleComponent]
public class CheckOutManager : MonoBehaviour
{
    #region Dependencies

    [Header("Dependencies")]
    [SerializeField] private SnakeCartManager enteredSnakeCartManager;
    [SerializeField] private CartPitZone myPitZone;

    #endregion

    #region State

    [Header("State")]
    [SerializeField] private bool isCheckingOut;
    [SerializeField] private bool isStationAvailable = true;

    #endregion

    #region Checkout Timer

    [Header("Checkout Timer")]
    [Min(0f)]
    [SerializeField] private float checkoutTimeLimit = 20f;

    private Coroutine checkoutTimerRoutine;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        EnableStation();
    }

    private void OnDisable()
    {
        StopCheckoutTimer();
    }

    #endregion

    #region Setup

    public void SetSnakeCartManager(SnakeCartManager snakeCartManager)
    {
        enteredSnakeCartManager = snakeCartManager;
    }

    public void SetMyPitZone(CartPitZone pitZone)
    {
        myPitZone = pitZone;
    }

    #endregion

    #region Checkout

    public void TryCheckoutCart()
    {
        if (!isCheckingOut || !isStationAvailable || enteredSnakeCartManager == null)
        {
            QuitCheckout();
            return;
        }

        // No follower carts remain.
        if (enteredSnakeCartManager.GetSnakeBodyLength() <= 1)
        {
            QuitCheckout();
            return;
        }

        // Remove/check out the next cart that actually contains a grocery item.
        int cartWithItemLeft = enteredSnakeCartManager.CheckOutNextCartWithItem();

        if (cartWithItemLeft <= 0) QuitCheckout();
    }

    public void SetIsCheckingOut()
    {
        isCheckingOut = true;

        StopCheckoutTimer();

        if (checkoutTimeLimit > 0f)
        {
            checkoutTimerRoutine = StartCoroutine(CheckoutTimerRoutine());
        }
    }

    public void QuitCheckout()
    {
        if (!isCheckingOut && enteredSnakeCartManager == null) return;

        isCheckingOut = false;
        StopCheckoutTimer();

        if (myPitZone != null)
        {
            myPitZone.ExitPitZone();
        }
        else
        {
            Debug.LogError("[CheckOutManager] CartPitZone reference is missing.", this);
        }

        enteredSnakeCartManager = null;
    }

    #endregion

    #region Station State

    public void EnableStation()
    {
        isStationAvailable = true;
    }

    public bool IsStationAvailable()
    {
        return isStationAvailable;
    }

    #endregion

    #region Timer

    private IEnumerator CheckoutTimerRoutine()
    {
        float timer = checkoutTimeLimit;

        while (timer > 0f && isCheckingOut)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        checkoutTimerRoutine = null;

        if (isCheckingOut) QuitCheckout();
    }

    private void StopCheckoutTimer()
    {
        if (checkoutTimerRoutine == null) return;

        StopCoroutine(checkoutTimerRoutine);
        checkoutTimerRoutine = null;
    }

    #endregion
}
