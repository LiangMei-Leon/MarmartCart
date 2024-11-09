using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class CartControlScript : MonoBehaviour
{
    private InputSystem_Actions _inputActions; // reference to the new input system class
    private Vector2 _inputVector; // 2D Vector variable that stores the raw input read from the input system
    public Vector3 desiredDirection { get; private set; } // Public property to provide desired direction

    [SerializeField] private bool controllable = true; // variable that controls if the system gonna read input
    [SerializeField] private GameEvent accelerationEvent; // raise this event as the player accelerates
    [SerializeField] private GameEvent brakeEvent; // raise this event as the player brakes
    void Awake()
    {
        _inputActions = new InputSystem_Actions(); // reference to the new input system class

        // Bind the movement action
        _inputActions.Player.Move.performed += ctx => _inputVector = ctx.ReadValue<Vector2>();
        _inputActions.Player.Move.canceled += ctx => _inputVector = Vector2.zero;

        _inputActions.Player.Accelerate.performed += ctx => accelerationEvent.Raise();
        _inputActions.Player.Brake.performed += ctx => brakeEvent.Raise();
    }
    private void OnEnable()
    {
        _inputActions.Enable();
    }
    private void OnDisable()
    {
        _inputActions.Disable();
    }
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
        desiredDirection = new Vector3(_inputVector.x, 0, _inputVector.y);

        // Draw a ray to visualize the direction of the input in the scene view
        Debug.DrawRay(transform.position, desiredDirection, Color.red);
    }
}
