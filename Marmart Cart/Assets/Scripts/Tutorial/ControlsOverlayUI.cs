using UnityEngine;

public class ControlsOverlayUI : MonoBehaviour
{
    [Header("Rows")]
    [SerializeField] private ControlHints moveRow;
    [SerializeField] private ControlHints moveBackwardRow;

    [SerializeField] private ControlHints checkoutRow;
    [SerializeField] private ControlHints exitRow;

    [SerializeField] private ControlHints aimRow;
    [SerializeField] private ControlHints shootRow;
    [SerializeField] private ControlHints chargeRow;

    [SerializeField] private CartControlScript _cart;

    private void Awake()
    {
        // Start minimal (show only movement / steer if you want)
        moveRow?.SetIntroduced(true);
        moveBackwardRow?.SetIntroduced(true);

        checkoutRow?.SetIntroduced(false);
        exitRow?.SetIntroduced(false);

        aimRow?.SetIntroduced(false);
        shootRow?.SetIntroduced(false);
    }

    public void BindToCart(CartControlScript cart)
    {
        if (_cart == cart) return;

        Unbind();
        _cart = cart;
        if (_cart == null) return;

        // These events need to exist in CartControlScript
        _cart.OnMoveBackwardPressed += HandleMoveBackward;
        _cart.OnCheckoutReleased += HandleCheckout;
        _cart.OnExitReleased += HandleExit;
        _cart.OnShootPressed += HandleShoot;
        _cart.OnMoveHeld += HandleMoveHeld;
        _cart.OnAimHeld += HandleAimHeld;
        _cart.OnSpeedupHeld += HandleSpeedupHeld;
    }

    public void Unbind()
    {
        if (_cart == null) return;
        _cart.OnMoveBackwardPressed -= HandleMoveBackward;
        _cart.OnCheckoutReleased = HandleCheckout;
        _cart.OnExitReleased -= HandleExit;
        _cart.OnShootPressed -= HandleShoot;
        _cart.OnMoveHeld -= HandleMoveHeld;
        _cart.OnAimHeld -= HandleAimHeld;
        _cart.OnSpeedupHeld -= HandleSpeedupHeld;

        _cart = null;
    }
    private void HandleMoveHeld(bool isHeld)
    {
        moveRow?.SetHeld(isHeld);
    }
    private void HandleAimHeld(bool isHeld)
    {
        aimRow?.SetHeld(isHeld);
    }
    private void HandleSpeedupHeld(bool isHeld)
    {
        chargeRow?.SetHeld(isHeld);
    }
    // Called by tutorial “sections” when you want to reveal a new control
    public void IntroduceMove() => moveRow?.SetIntroduced(true);
    public void IntroduceMoveBackward() => moveBackwardRow?.SetIntroduced(true);
    public void IntroduceCheckout() => checkoutRow?.SetIntroduced(true);
    public void IntroduceExit() => exitRow?.SetIntroduced(true);
    public void IntroduceAim() => aimRow?.SetIntroduced(true);
    public void IntroduceShoot() => shootRow?.SetIntroduced(true);
    public void IntroduceCharge() => chargeRow?.SetIntroduced(true);

    // Input callbacks -> pulse the row
    private void HandleMoveBackward() => moveBackwardRow?.Pulse();
    private void HandleCheckout() => checkoutRow?.Pulse();
    private void HandleExit() => exitRow?.Pulse();
    private void HandleShoot() => shootRow?.Pulse();
}
