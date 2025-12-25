using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.Windows;

public class CartControlScript : MonoBehaviour
{
    private InputSystem_Actions _inputActions; // reference to the new input system class
    private Vector2 _inputVector; // 2D Vector variable that stores the raw input read from the input system
    private Vector3 _input; // 3D Vector variable that stores the raw input from 2D to 3D coordinate system (xy -> xz)

    private InputUser user;
    private InputDevice assignedDevice;

    public Vector3 desiredDirection { get; private set; } // Public property to provide desired direction
    // Aiming input
    private Vector2 _aimInputVector;
    private Vector3 _aimDirection;
    private bool canAim = false;
    public Vector3 AimDirection => _aimDirection;

    [SerializeField] private bool controllable = true; // variable that controls if the system gonna read input
    [SerializeField] private bool isInPit = false;
    [SerializeField] private GameEvent boostEvent; // raise this event when the player accelerates (boost)
    [SerializeField] private GameEvent resetCartEvent; // raise this event when the player wants to reset the cart (to solve stuck issues)
    [SerializeField] private float speedUpMeter = 100f;
    [SerializeField] private float speedUpConsumeRate = 10f;
    private bool isSpeedingUp = false;
    [SerializeField] private bool canSpeedup = true;
    [SerializeField] private bool canActivatePowerUp = false;
    [SerializeField] private bool canFlip = false;

    [SerializeField] private PowerupsManager powerupsManager; // Reference to the PowerupsManager script

    // fro cart pit check out actions
    private CheckOutManager activeCheckoutManager;

    public System.Action OnTutorialPrev;
    public System.Action OnTutorialNext;

    public void InitializeWithDevice(InputDevice device)
    {
        assignedDevice = device;
        _inputActions = new InputSystem_Actions();

        // Pair an InputUser with this device and bind actions
        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(device, user);

        _inputActions.Enable();

        // Hook up filtered actions for controller only, left stick for movement
        _inputActions.Player.Move.performed += ctx =>
        {
            if (ctx.control.device == device)
                _inputVector = ctx.ReadValue<Vector2>();
        };
        _inputActions.Player.Move.canceled += ctx =>
        {
            if (ctx.control.device == device)
                _inputVector = Vector2.zero;
        };
        // Aim input for controller (right stick)
        _inputActions.Player.Aim.performed += ctx =>
        {
            if (ctx.control.device == device && canAim)
            {
                _aimInputVector = ctx.ReadValue<Vector2>();
                _aimDirection = new Vector3(_aimInputVector.x, 0, _aimInputVector.y).ToIso();
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
        // for speed up
        _inputActions.Player.Speedup.performed += ctx =>
        {
            if (ctx.control.device == device && speedUpMeter > speedUpConsumeRate && canSpeedup)
                isSpeedingUp = true;
        };
        _inputActions.Player.Speedup.canceled += ctx =>
        {
            if (ctx.control.device == device)
                isSpeedingUp = false;
        };
        // for powerful charged boost
        _inputActions.Player.ActivatePowerUp.performed += ctx =>
        {
            if (ctx.control.device == device && canActivatePowerUp)
            {
                ActivatePowerUp();
            }
        };
        _inputActions.Player.FlipDirection.performed += ctx =>
        {
            if (ctx.control.device == device && canFlip)
                resetCartEvent.Raise();
        };
        // inputs for check out pit
        _inputActions.Player.CheckOut.performed += ctx =>
        {
            if (ctx.control.device == device && activeCheckoutManager != null)
                activeCheckoutManager.TryCheckoutCart();
        };
        _inputActions.Player.QuitCheckOut.performed += ctx =>
        {
            if (ctx.control.device == device && activeCheckoutManager != null)
                activeCheckoutManager.QuitCheckout();
        };
        // Tutorial page inputs (D-pad left/right)
        _inputActions.Player.TutorialPrev.performed += ctx =>
        {
            if (ctx.control.device == device)
                OnTutorialPrev?.Invoke();
        };

        _inputActions.Player.TutorialNext.performed += ctx =>
        {
            if (ctx.control.device == device)
                OnTutorialNext?.Invoke();
        };
        _inputActions.Enable(); // Only enable after setup is complete
    }
    public void InitializeWithKeyboard()
    {
        assignedDevice = Keyboard.current;

        _inputActions = new InputSystem_Actions();
        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(Keyboard.current, user);

        _inputActions.Enable();

        // Hook up filtered actions for keyboard, WASD for movement, mouse for aim
        _inputActions.Player.Move.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
                _inputVector = ctx.ReadValue<Vector2>();
        };
        _inputActions.Player.Move.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
                _inputVector = Vector2.zero;
        };
        // Aim input for controller (mouse position)
        _inputActions.Player.Aim.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canAim)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                Vector2 offset = mouseScreenPos - screenCenter;
                _aimInputVector = offset.normalized;
                _aimDirection = new Vector3(_aimInputVector.x, 0, _aimInputVector.y).ToIso();
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
        // for speed up
        _inputActions.Player.Speedup.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && speedUpMeter > speedUpConsumeRate && canSpeedup)
                isSpeedingUp = true;
        };
        _inputActions.Player.Speedup.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
                isSpeedingUp = false;
        };
        // for powerful charged boost
        _inputActions.Player.ActivatePowerUp.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canActivatePowerUp)
            {
                ActivatePowerUp();
            }
        };
        _inputActions.Player.FlipDirection.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canFlip)
                resetCartEvent.Raise();
        };
        // inputs for check out pit
        _inputActions.Player.CheckOut.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && activeCheckoutManager != null)
                activeCheckoutManager.TryCheckoutCart();
        };
        _inputActions.Player.QuitCheckOut.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && activeCheckoutManager != null)
                activeCheckoutManager.QuitCheckout();
        };
        _inputActions.Player.TutorialPrev.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
                OnTutorialPrev?.Invoke();
        };

        _inputActions.Player.TutorialNext.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
                OnTutorialNext?.Invoke();
        };
    }
    void Start()
    {
        speedUpMeter = 50f;
    }

    // Update is called once per frame
    void Update()
    {
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, AimDirection * 30f, Color.red);
        if (controllable && !isInPit)
        {
            GatherInput();
        }

        if (isSpeedingUp && speedUpMeter > 0f)
        {
            speedUpMeter -= speedUpConsumeRate * Time.deltaTime * 10f;

            if (speedUpMeter <= 0f)
            {
                speedUpMeter = 0f;
                isSpeedingUp = false;
            }
        }

        if(UnityEngine.Input.GetKeyDown(KeyCode.T))
        {
            RefillSpeedUpMeter(100f);
        }
    }

    void GatherInput()
    {
        // Transfer 2D input to 3D input (from xy to xz)
        _input = new Vector3(_inputVector.x, 0, _inputVector.y);

        desiredDirection = controllable ? _input.ToIso() : Vector3.zero;

        // Draw a ray to visualize the direction of the input in the scene view
        // Debug.DrawRay(transform.position, desiredDirection, Color.red);
    }
    public void CleanupInput()
    {
        _inputActions?.Disable();
        InputUser.PerformPairingWithDevice(null, user); // unpair
    }
    public void SetActiveCheckoutHandler(CheckOutManager currenetCheckoutManager)
    {
        activeCheckoutManager = currenetCheckoutManager;
    }
    public void SetPowerupsManager(PowerupsManager manager)
    {
        powerupsManager = manager;
    }
    public void AllowFlip()
    {
        canFlip = true;
    }
    public void DisallowFlip()
    {
        canFlip = false;
    }
    public bool GetCanFlip()
    {
        return canFlip;
    }
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
    public void DisableControl()
    {
        controllable = false;
    }
    public bool GetCanAim()
    {
        return canAim;
    }
    public void EnableControl()
    {
        controllable = true;
    }
    public void SetInPit()
    {
        isInPit = true;
    }
    public void SetOutPit()
    {
        isInPit = false;
    }
    public bool GetIsInPit()
    {
        return isInPit;
    }
    public bool IsCharing()
    {
        return !controllable;
    }
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
    }
    public float GetSpeedUpMeter()
    {
        return speedUpMeter;
    }
    public void RefillSpeedUpMeter(float amount)
    {
        speedUpMeter = Mathf.Clamp(speedUpMeter + amount, 0f, 100f);
    }
    public void ActivatePowerUp()
    {
        if (!canActivatePowerUp)
            return;

        powerupsManager.ActivateStoredPowerup(); // Call the method in PowerupsManager to handle the power-up logic
    }
    public void AllowAim() => canAim = true;
    public void DisallowAim() => canAim = false;
}
