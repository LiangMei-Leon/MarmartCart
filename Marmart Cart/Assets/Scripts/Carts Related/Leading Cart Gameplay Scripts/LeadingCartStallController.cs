using UnityEngine;

/// <summary>
/// Converts the front stall sensor into a simple gameplay stall state.
///
/// Stall rule:
/// Front sensor blocked + cart speed below threshold = Stalled.
///
/// No delay and no input-direction requirement.
/// </summary>
public class LeadingCartStallController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private LeadingCartStallSensor stallSensor;
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private Rigidbody cartBody;

    #endregion

    #region Stall Settings

    [Header("Stall Settings")]
    [Tooltip("Maximum planar speed at which a blocked cart is considered stalled.")]
    [Min(0f)]
    [SerializeField] private float maxStallSpeed = 1.5f;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isStalled;
    [SerializeField] private float currentPlanarSpeed;

    public bool IsStalled => isStalled;
    public bool IsFrontBlocked => stallSensor != null && stallSensor.IsBlocked;

    #endregion

    #region Events

    public System.Action OnStallStarted;
    public System.Action OnStallEnded;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (stallSensor == null) stallSensor = GetComponent<LeadingCartStallSensor>();

        if (stallSensor == null) Debug.LogError("[LeadingCartStallController] Stall Sensor is not assigned.", this);
        if (cartControlInput == null) Debug.LogError("[LeadingCartStallController] CartControlScript is not assigned.", this);
        if (cartBody == null) Debug.LogError("[LeadingCartStallController] Cart Rigidbody is not assigned.", this);
    }

    private void FixedUpdate()
    {
        UpdateStallState();
    }

    private void OnDisable()
    {
        if (isStalled && cartControlInput != null) cartControlInput.DisallowMoveBackward();

        isStalled = false;
        currentPlanarSpeed = 0f;
    }

    #endregion

    #region Stall State

    private void UpdateStallState()
    {
        if (stallSensor == null || cartControlInput == null || cartBody == null)
        {
            SetStalled(false);
            return;
        }

        currentPlanarSpeed = GetPlanarSpeed();

        bool shouldBeStalled =
            stallSensor.IsBlocked &&
            currentPlanarSpeed <= maxStallSpeed &&
            !cartControlInput.GetIsInPit();

        SetStalled(shouldBeStalled);
    }

    private void SetStalled(bool stalled)
    {
        if (isStalled == stalled) return;

        isStalled = stalled;

        if (isStalled)
        {
            cartControlInput.AllowMoveBackward();
            OnStallStarted?.Invoke();
        }
        else
        {
            cartControlInput.DisallowMoveBackward();
            OnStallEnded?.Invoke();
        }
    }

    #endregion

    #region Helpers

    private float GetPlanarSpeed()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);
        return planarVelocity.magnitude;
    }

    #endregion
}