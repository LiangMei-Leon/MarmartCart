using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.Serialization;

/// <summary>
/// Central input/state gateway for the leading cart.
/// Reads the assigned player's Input System actions and exposes movement state/events.
/// It does not directly move the Rigidbody.
/// </summary>
public class CartControlScript : MonoBehaviour
{
    #region Input Runtime

    private InputSystem_Actions _inputActions;
    private InputUser user;

    private Vector2 _inputVector;
    private Vector3 _input;

    public Vector3 desiredDirection { get; private set; }
    public Vector2 MoveInput => _inputVector;

    #endregion

    #region General Control State

    [Header("Control State")]
    [SerializeField] private bool controllable = true;
    [SerializeField] private bool isInPit = false;

    #endregion

    #region Drift

    [Header("Drift")]
    [FormerlySerializedAs("canDrift")]
    [SerializeField] private bool allowDrift = true;

    [FormerlySerializedAs("prototypeSteerDeadzone")]
    [SerializeField] private float steerDeadzone = 0.15f;

    [Header("Drift / Speedup Mutual Override")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private bool enableDriftSpeedupOverride = true;

    private bool isDriftHeld;

    public float GetSteerInput()
    {
        if (Mathf.Abs(_inputVector.x) < steerDeadzone) return 0f;
        return Mathf.Clamp(_inputVector.x, -1f, 1f);
    }
    #endregion

    #region Aiming

    private Vector2 _aimInputVector;
    private Vector3 _aimDirection;

    [SerializeField] private bool canAim = false;

    public Vector3 AimDirection => _aimDirection;

    #endregion

    #region Speedup

    [Header("Speedup")]
    [SerializeField] private float speedUpMeter = 100f;
    [SerializeField] private float speedUpConsumeRate = 10f;
    [SerializeField] private bool canSpeedup = true;

    [FormerlySerializedAs("boostEvent")]
    [SerializeField] private GameEvent speedupEvent;

    private bool isSpeedingUp;

    #endregion

    #region Move Backward

    [Header("Move Backward")]
    [SerializeField] private bool canMoveBackward = false;

    #endregion

    #region Powerup / Checkout

    [Header("Powerup")]
    [SerializeField] private bool canActivatePowerUp = false;
    [SerializeField] private PowerupsManager powerupsManager;

    private CheckOutManager activeCheckoutManager;

    #endregion

    #region Input Events

    public System.Action OnTutorialPrev;
    public System.Action OnTutorialNext;

    public System.Action OnMoveBackwardPressed;
    public System.Action OnCheckoutReleased;
    public System.Action OnExitReleased;
    public System.Action OnShootPressed;

    public System.Action<bool> OnMoveHeld;
    public System.Action<bool> OnAimHeld;
    public System.Action<bool> OnSpeedupHeld;

    #endregion

    #region Initialization

    public void InitializeWithDevice(InputDevice device)
    {
        _inputActions = new InputSystem_Actions();

        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(device, user);

        BindControllerActions(device);
        _inputActions.Enable();
    }

    public void InitializeWithKeyboard()
    {
        _inputActions = new InputSystem_Actions();

        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(Keyboard.current, user);

        BindKeyboardActions();
        _inputActions.Enable();
    }

    private void BindControllerActions(InputDevice device)
    {
        _inputActions.Player.Move.performed += ctx =>
        {
            if (ctx.control.device == device) _inputVector = ctx.ReadValue<Vector2>();
        };

        _inputActions.Player.Move.canceled += ctx =>
        {
            if (ctx.control.device == device) _inputVector = Vector2.zero;
        };

        _inputActions.Player.Drift.performed += ctx =>
        {
            if (ctx.control.device == device && allowDrift) HandleDriftPressed();
        };

        _inputActions.Player.Drift.canceled += ctx =>
        {
            if (ctx.control.device == device) HandleDriftReleased();
        };

        _inputActions.Player.Aim.performed += ctx =>
        {
            if (ctx.control.device == device && canAim)
            {
                _aimInputVector = ctx.ReadValue<Vector2>();
                _aimDirection = new Vector3(_aimInputVector.x, 0f, _aimInputVector.y).ToIso();
            }
        };

        _inputActions.Player.Aim.canceled += ctx =>
        {
            if (ctx.control.device == device)
            {
                _aimInputVector = Vector2.zero;
                _aimDirection = Vector3.zero;
            }
        };

        _inputActions.Player.Speedup.performed += ctx =>
        {
            if (ctx.control.device == device && speedUpMeter > speedUpConsumeRate && canSpeedup) HandleSpeedupPressed();
        };

        _inputActions.Player.Speedup.canceled += ctx =>
        {
            if (ctx.control.device == device) HandleSpeedupReleased();
        };

        _inputActions.Player.ActivatePowerUp.performed += ctx =>
        {
            if (ctx.control.device == device && canActivatePowerUp)
            {
                ActivatePowerUp();
                OnShootPressed?.Invoke();
            }
        };

        _inputActions.Player.MoveBackward.performed += ctx =>
        {
            if (ctx.control.device == device && canMoveBackward)
            {
                canMoveBackward = false;
                OnMoveBackwardPressed?.Invoke();
            }
        };

        _inputActions.Player.CheckOut.performed += ctx =>
        {
            if (ctx.control.device == device && activeCheckoutManager != null)
            {
                activeCheckoutManager.TryCheckoutCart();
                OnCheckoutReleased?.Invoke();
            }
        };

        _inputActions.Player.QuitCheckOut.performed += ctx =>
        {
            if (ctx.control.device == device && activeCheckoutManager != null)
            {
                activeCheckoutManager.QuitCheckout();
                OnExitReleased?.Invoke();
            }
        };

        _inputActions.Player.TutorialPrev.performed += ctx =>
        {
            if (ctx.control.device == device) OnTutorialPrev?.Invoke();
        };

        _inputActions.Player.TutorialNext.performed += ctx =>
        {
            if (ctx.control.device == device) OnTutorialNext?.Invoke();
        };
    }

    private void BindKeyboardActions()
    {
        _inputActions.Player.Move.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) _inputVector = ctx.ReadValue<Vector2>();
        };

        _inputActions.Player.Move.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current) _inputVector = Vector2.zero;
        };

        _inputActions.Player.Drift.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && allowDrift) HandleDriftPressed();
        };

        _inputActions.Player.Drift.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current) HandleDriftReleased();
        };

        _inputActions.Player.Aim.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canAim)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                Vector2 offset = mouseScreenPos - screenCenter;

                _aimInputVector = offset.normalized;
                _aimDirection = new Vector3(_aimInputVector.x, 0f, _aimInputVector.y).ToIso();
            }
        };

        _inputActions.Player.Aim.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
            {
                _aimInputVector = Vector2.zero;
                _aimDirection = Vector3.zero;
            }
        };

        _inputActions.Player.Speedup.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && speedUpMeter > speedUpConsumeRate && canSpeedup) HandleSpeedupPressed();
        };

        _inputActions.Player.Speedup.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current) HandleSpeedupReleased();
        };

        _inputActions.Player.ActivatePowerUp.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canActivatePowerUp)
            {
                ActivatePowerUp();
                OnShootPressed?.Invoke();
            }
        };

        _inputActions.Player.MoveBackward.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canMoveBackward)
            {
                canMoveBackward = false;
                OnMoveBackwardPressed?.Invoke();
            }
        };

        _inputActions.Player.CheckOut.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && activeCheckoutManager != null)
            {
                activeCheckoutManager.TryCheckoutCart();
                OnCheckoutReleased?.Invoke();
            }
        };

        _inputActions.Player.QuitCheckOut.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && activeCheckoutManager != null)
            {
                activeCheckoutManager.QuitCheckout();
                OnExitReleased?.Invoke();
            }
        };

        _inputActions.Player.TutorialPrev.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) OnTutorialPrev?.Invoke();
        };

        _inputActions.Player.TutorialNext.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) OnTutorialNext?.Invoke();
        };
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        speedUpMeter = 50f;
    }

    private void Update()
    {
        if (controllable && !isInPit) GatherInput();

        if (!controllable || isInPit || !allowDrift) isDriftHeld = false;
        if (!controllable || isInPit || !canSpeedup) StopSpeedupInput();

        UpdateSpeedup();
        UpdateHeldEvents();
    }

    #endregion

    #region Movement Input

    private void GatherInput()
    {
        _input = new Vector3(_inputVector.x, 0f, _inputVector.y);
        desiredDirection = controllable ? _input.ToIso() : Vector3.zero;
    }

    public void CleanupInput()
    {
        _inputActions?.Disable();
        InputUser.PerformPairingWithDevice(null, user);
    }

    #endregion

    #region Drift / Speedup Interaction

    private void HandleDriftPressed()
    {
        if (!allowDrift) return;

        if (enableDriftSpeedupOverride) StopSpeedupInput();
        isDriftHeld = true;
    }

    private void HandleDriftReleased()
    {
        isDriftHeld = false;
    }

    private void HandleSpeedupPressed()
    {
        if (speedUpMeter <= speedUpConsumeRate || !canSpeedup) return;

        if (enableDriftSpeedupOverride) StopDriftInputForSpeedup("Speedup pressed");
        isSpeedingUp = true;
    }

    private void HandleSpeedupReleased()
    {
        isSpeedingUp = false;
    }

    private void StopSpeedupInput()
    {
        if (!isSpeedingUp) return;

        isSpeedingUp = false;
        OnSpeedupHeld?.Invoke(false);
    }

    private void StopDriftInputForSpeedup(string reason)
    {
        if (!isDriftHeld && (driftController == null || !driftController.IsDrifting)) return;

        isDriftHeld = false;

        if (driftController != null) driftController.CancelDriftForSpeedup(reason);
    }

    #endregion

    #region Speedup Runtime

    private void UpdateSpeedup()
    {
        if (isSpeedingUp && speedUpMeter > 0f)
        {
            speedUpMeter -= speedUpConsumeRate * Time.deltaTime * 10f;
            OnSpeedupHeld?.Invoke(true);

            if (speedUpMeter <= 0f)
            {
                speedUpMeter = 0f;
                isSpeedingUp = false;
                OnSpeedupHeld?.Invoke(false);
            }

            return;
        }

        OnSpeedupHeld?.Invoke(false);
    }

    private void UpdateHeldEvents()
    {
        OnMoveHeld?.Invoke(_inputVector.sqrMagnitude > 0.05f);
        OnAimHeld?.Invoke(_aimInputVector.sqrMagnitude > 0.05f);
    }

    #endregion

    #region Checkout / Powerup References

    public void SetActiveCheckoutHandler(CheckOutManager currentCheckoutManager)
    {
        activeCheckoutManager = currentCheckoutManager;
    }

    public void SetPowerupsManager(PowerupsManager manager)
    {
        powerupsManager = manager;
    }

    #endregion

    #region Move Backward State

    public void AllowMoveBackward()
    {
        canMoveBackward = true;
    }

    public void DisallowMoveBackward()
    {
        canMoveBackward = false;
    }

    public bool GetCanMoveBackward()
    {
        return canMoveBackward;
    }

    #endregion

    #region Drift State

    public bool IsDriftHeld()
    {
        return isDriftHeld;
    }

    public bool CanDrift()
    {
        return allowDrift;
    }

    public void AllowDrift()
    {
        allowDrift = true;
    }

    public void DisallowDrift()
    {
        allowDrift = false;
        isDriftHeld = false;
    }

    #endregion

    #region Powerup State

    public bool GetCanActivatePowerUp()
    {
        return canActivatePowerUp;
    }

    public void AllowActivatePowerUp()
    {
        canActivatePowerUp = true;
    }

    public void DisallowActivatePowerUp()
    {
        canActivatePowerUp = false;
    }

    public void ActivatePowerUp()
    {
        if (!canActivatePowerUp || powerupsManager == null) return;
        powerupsManager.ActivateStoredPowerup();
    }

    #endregion

    #region General Control / Pit State

    public void DisableControl()
    {
        controllable = false;
        isDriftHeld = false;
    }

    public void EnableControl()
    {
        controllable = true;
    }

    public void SetInPit()
    {
        isInPit = true;
        isDriftHeld = false;
    }

    public void SetOutPit()
    {
        isInPit = false;
    }

    public bool GetIsInPit()
    {
        return isInPit;
    }

    public bool GetCanAim()
    {
        return canAim;
    }

    public void AllowAim()
    {
        canAim = true;
    }

    public void DisallowAim()
    {
        canAim = false;
    }

    // Legacy charging state used by the old powerup/combat code.
    public bool IsCharing()
    {
        return !controllable;
    }

    #endregion

    #region Speedup State

    public bool IsSpeedingUp()
    {
        return isSpeedingUp;
    }

    public bool CanSpeedingUp()
    {
        return canSpeedup;
    }

    public void AllowSpeedingUp()
    {
        canSpeedup = true;
    }

    public void DisallowSpeedingUp()
    {
        canSpeedup = false;
        StopSpeedupInput();
    }

    public float GetSpeedUpMeter()
    {
        return speedUpMeter;
    }

    public void RefillSpeedUpMeter(float amount)
    {
        speedUpMeter = Mathf.Clamp(speedUpMeter + amount, 0f, 100f);
    }

    #endregion
}