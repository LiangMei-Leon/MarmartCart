using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.Windows;

public class CartControlScript : MonoBehaviour
{
    private InputSystem_Actions _inputActions; // reference to the new input system class
    private Vector2 _inputVector; // 2D Vector variable that stores the raw input read from the input system
    private Vector3 _input; // 3D Vector variable that stores the raw input from 2D to 3D coordinate system (xy -> xz)

    [Header("Device Binding")]
    [SerializeField] private bool useKeyboard;
    [SerializeField] private bool useGamepad;
    private InputUser user;
    private InputDevice assignedDevice;

    public Vector3 desiredDirection { get; private set; } // Public property to provide desired direction

    [SerializeField] private bool controllable = true; // variable that controls if the system gonna read input
    [SerializeField] private GameEvent boostEvent; // raise this event when the player accelerates (boost)
    [SerializeField] private GameEvent resetCartEvent; // raise this event when the player wants to reset the cart (to solve stuck issues)

    [SerializeField] private bool canFlip = false;  
    //void Awake()
    //{
    //    _inputActions = new InputSystem_Actions(); // reference to the new input system class

    //    // Bind the movement action
    //    _inputActions.Player.Move.performed += ctx => _inputVector = ctx.ReadValue<Vector2>();
    //    _inputActions.Player.Move.canceled += ctx => _inputVector = Vector2.zero;

    //    _inputActions.Player.Boost.performed += ctx => boostEvent.Raise();
    //    _inputActions.Player.FlipDirection.performed += ctx => resetCartEvent.Raise();
    //}
    //void Awake()
    //{
    //    // Assign input device based on inspector
    //    if (useKeyboard)
    //        assignedDevice = Keyboard.current;
    //    else if (useGamepad)
    //        assignedDevice = Gamepad.current;

    //    if (assignedDevice == null)
    //    {
    //        Debug.LogError($"Assigned device is missing for {gameObject.name}");
    //        return;
    //    }

    //    _inputActions = new InputSystem_Actions();

    //    _inputActions.Player.Move.performed += ctx =>
    //    {
    //        if (ctx.control.device == assignedDevice)
    //            _inputVector = ctx.ReadValue<Vector2>();
    //    };
    //    _inputActions.Player.Move.canceled += ctx =>
    //    {
    //        if (ctx.control.device == assignedDevice)
    //            _inputVector = Vector2.zero;
    //    };

    //    _inputActions.Player.Boost.performed += ctx =>
    //    {
    //        if (ctx.control.device == assignedDevice)
    //            boostEvent.Raise();
    //    };
    //    _inputActions.Player.FlipDirection.performed += ctx =>
    //    {
    //        if (ctx.control.device == assignedDevice)
    //            resetCartEvent.Raise();
    //    };
    //}
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
        _inputActions.Player.Boost.performed += ctx =>
        {
            if (ctx.control.device == device)
                boostEvent.Raise();
        };
        _inputActions.Player.FlipDirection.performed += ctx =>
        {
            if (ctx.control.device == device && canFlip)
                resetCartEvent.Raise();
        };

        _inputActions.Enable(); // Only enable after setup is complete
    }
    //private void OnEnable()
    //{
    //    _inputActions.Enable();
    //}
    //private void OnDisable()
    //{
    //    _inputActions.Disable();
    //}
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (controllable)
        {
            GatherInput();
        }
    }

    void GatherInput()
    {
        // Transfer 2D input to 3D input (from xy to xz)
        _input = new Vector3(_inputVector.x, 0, _inputVector.y);

        desiredDirection = _input.ToIso();

        // Draw a ray to visualize the direction of the input in the scene view
        Debug.DrawRay(transform.position, desiredDirection, Color.red);
    }
    public void CleanupInput()
    {
        _inputActions?.Disable();
        InputUser.PerformPairingWithDevice(null, user); // unpair
    }

    public void AllowFlip()
    {
        canFlip = true;
    }
    public void DisallowFlip()
    {
        canFlip = false;
    }
}
