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

    [SerializeField] private bool controllable = true; // variable that controls if the system gonna read input
    [SerializeField] private bool isInPit = false;
    [SerializeField] private GameEvent boostEvent; // raise this event when the player accelerates (boost)
    [SerializeField] private GameEvent resetCartEvent; // raise this event when the player wants to reset the cart (to solve stuck issues)
    [SerializeField] private float speedUpMeter = 100f;
    [SerializeField] private float speedUpConsumeRate = 10f;
    private bool isSpeedingUp = false;
    [SerializeField] private bool canSpeedup = true;
    [SerializeField] private bool canBoost = true;
    [SerializeField] private bool canFlip = false;

    // fro cart pit check out actions
    private CheckOutManager activeCheckoutManager;

    public void InitializeWithDevice(InputDevice device)
    {
        assignedDevice = device;
        _inputActions = new InputSystem_Actions();

        // Pair an InputUser with this device and bind actions
        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(device, user);

        _inputActions.Enable();

        // Hook up filtered actions
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
        _inputActions.Player.Boost.performed += ctx =>
        {
            if (ctx.control.device == device && canBoost && speedUpMeter >= 100f)
            {
                speedUpMeter = 0f;
                boostEvent.Raise();
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
        _inputActions.Player.Boost.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && canBoost && speedUpMeter >= 100f)
            {
                speedUpMeter = 0f;
                boostEvent.Raise();
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
    }
    void Start()
    {
        speedUpMeter = 100f;
    }

    // Update is called once per frame
    void Update()
    {
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
    public void AllowBoost()
    {
        canBoost = true;
    }
    public void DisallowBoost()
    {
        canBoost = false;
    }
    public void DisableControl()
    {
        controllable = false;
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
    public float GetSpeedUpMeter()
    {
        return speedUpMeter;
    }
    public void RefillSpeedUpMeter(float amount)
    {
        speedUpMeter = Mathf.Clamp(speedUpMeter + amount, 0f, 100f);
    }
}
