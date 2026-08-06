using UnityEngine;

public class CartDriftController : MonoBehaviour
{
    public enum DriftState
    {
        None,
        DriftArmed,
        Drifting,
        DriftBlockedUntilRelease
    }

    public enum DriftInputMode
    {
        None,
        Holding,
        LiveMapped
    }

    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private Rigidbody cartBody;

    [Header("Prototype Toggle")]
    [SerializeField] private bool enableCameraMappedDriftPrototype = true;

    [Header("Drift Entry Rules")]
    [SerializeField] private float minSpeedToStartDrift = 5f;

    [Tooltip("Only used to choose a stable left/right drift side. Not meant to block committed drift input.")]
    [SerializeField] private float minInputMagnitudeToChooseSide = 0.15f;

    [Tooltip("Only used to avoid random left/right side choice from tiny angle noise.")]
    [SerializeField] private float minInputAngleToChooseSide = 5f;

    [Tooltip("If true, drift side is chosen from cart facing direction. If false, side is chosen from current velocity/path direction.")]
    [SerializeField] private bool useCartForwardForEntrySide = true;
    public bool IsDriftBlockedUntilRelease => driftState == DriftState.DriftBlockedUntilRelease;

    [Header("Dynamic Side Switch Experiment")]
    [Tooltip("True = current locked drift behavior. False = strong opposite input can switch drift side while still holding drift.")]
    [SerializeField] private bool lockDriftSide = true;

    [Tooltip("Input magnitude required to switch drift side while drifting. Keep high to avoid snapback switching.")]
    [SerializeField] private float sideSwitchMinInputMagnitude = 0.9f;

    [Tooltip("Input must be this many degrees to the opposite side before drift side switches.")]
    [SerializeField] private float sideSwitchMinAngleFromReference = 25f;

    [Tooltip("If true, entry debug lines update every time side switches.")]
    [SerializeField] private bool updateEntryDebugOnSideSwitch = true;

    [SerializeField] private bool debugSideSwitch = true;

    [Header("Direct Tightness Mapping")]
    [Tooltip("If stick magnitude is below this while drifting, preserve the current tightness. For this prototype, high values like 0.85-0.9 work well.")]
    [SerializeField] private float driftInputHoldDeadzone = 0.9f;

    [Tooltip("Same-side angle at or below this maps to widest drift.")]
    [SerializeField] private float wideSameSideAngle = 10f;

    [Tooltip("Same-side angle at or above this maps to tightest drift.")]
    [SerializeField] private float tightSameSideAngle = 70f;

    [Tooltip("If true, tightness is measured against velocity/path direction. If false, measured against cart forward.")]
    [SerializeField] private bool usePathDirectionForTightness = true;

    [Header("Drift Steering Output")]
    [SerializeField] private bool enableDriftSteeringOutput = true;

    [Tooltip("Wheel angle for very wide / preserved drift.")]
    [SerializeField] private float wideDriftSteerAngle = 5f;

    [Tooltip("Wheel angle for medium drift.")]
    [SerializeField] private float baseDriftSteerAngle = 15f;

    [Tooltip("Wheel angle for max useful U-turn drift.")]
    [SerializeField] private float tightDriftSteerAngle = 30f;

    [Tooltip("At this tightness value, steering reaches baseDriftSteerAngle.")]
    [SerializeField] private float baseTightnessPoint = 0.5f;

    [SerializeField] private bool invertDriftSteeringSign = false;

    [Header("Debug")]
    [SerializeField] private bool debugDriftState = true;
    [SerializeField] private float debugLogInterval = 0.35f;

    public bool IsDrifting => enableCameraMappedDriftPrototype && driftState == DriftState.Drifting;
    public bool IsDriftArmed => enableCameraMappedDriftPrototype && driftState == DriftState.DriftArmed;

    public bool EnableDriftSteeringOutput => enableDriftSteeringOutput;

    public bool LockDriftSide => lockDriftSide;
    public int SideSwitchCount => sideSwitchCount;

    /// <summary>
    /// -1 / +1 drift side.
    /// If left/right steering feels inverted, use invertDriftSteeringSign first.
    /// </summary>
    public float DriftSign => driftSign;

    public string DriftSideName
    {
        get
        {
            if (!IsDrifting) return "None";
            return driftSign > 0f ? "Right" : "Left";
        }
    }

    public string CurrentStateName => driftState.ToString();
    public DriftInputMode CurrentInputMode => currentInputMode;

    public Vector3 EntryForward => entryForward;
    public Vector3 EntryInputDirection => entryInputDirection;
    public Vector3 EntryPathDirection => entryPathDirection;

    public float CurrentInputAngle => currentInputAngle;
    public float CurrentSameSideAngle => currentSameSideAngle;
    public float CurrentSpeed => currentSpeed;
    public float CurrentTightness => currentTightness;

    public float DriftSteeringAngle
    {
        get
        {
            if (!IsDrifting || !enableDriftSteeringOutput)
                return 0f;

            float steerMagnitude;

            if (currentTightness <= baseTightnessPoint)
            {
                float t = Mathf.InverseLerp(0f, baseTightnessPoint, currentTightness);
                steerMagnitude = Mathf.Lerp(wideDriftSteerAngle, baseDriftSteerAngle, t);
            }
            else
            {
                float t = Mathf.InverseLerp(baseTightnessPoint, 1f, currentTightness);
                steerMagnitude = Mathf.Lerp(baseDriftSteerAngle, tightDriftSteerAngle, t);
            }

            float sign = invertDriftSteeringSign ? -driftSign : driftSign;
            return sign * steerMagnitude;
        }
    }

    private DriftState driftState = DriftState.None;
    private DriftInputMode currentInputMode = DriftInputMode.None;

    private float driftSign = 0f;
    private float currentInputAngle = 0f;
    private float currentSameSideAngle = 0f;
    private float currentSpeed = 0f;
    private float currentTightness = 0f;
    private float debugTimer = 0f;

    private int sideSwitchCount = 0;

    private Vector3 entryForward = Vector3.zero;
    private Vector3 entryInputDirection = Vector3.zero;
    private Vector3 entryPathDirection = Vector3.zero;
    public System.Action OnDriftStarted;
    public System.Action<string> OnDriftEndedClean;
    public System.Action<string> OnDriftInterrupted;
    private void Update()
    {
        UpdateDriftState();
    }

    private void UpdateDriftState()
    {
        if (!enableCameraMappedDriftPrototype || cartControlInput == null || cartBody == null)
        {
            ForceClearDrift();
            return;
        }

        bool driftHeld = cartControlInput.IsDriftHeld();
        Vector3 desiredDirection = cartControlInput.desiredDirection;
        Vector2 moveInput = cartControlInput.MoveInput;

        currentSpeed = GetPlanarSpeed();

        if (driftState == DriftState.DriftBlockedUntilRelease)
        {
            if (!driftHeld)
            {
                ForceClearDrift();

                if (debugDriftState)
                    Debug.Log("[Drift Prototype] UNBLOCKED after drift button release.");
            }

            return;
        }

        if (!driftHeld)
        {
            EndDrift(true);
            return;
        }

        if (driftState == DriftState.None)
        {
            driftState = DriftState.DriftArmed;
            currentInputMode = DriftInputMode.Holding;

            if (debugDriftState)
                Debug.Log("[Drift Prototype] ARMED");
        }

        if (driftState == DriftState.DriftArmed)
        {
            TryStartDrift(desiredDirection, moveInput);
            return;
        }

        if (driftState == DriftState.Drifting)
        {
            UpdateTightnessDirectMapping(desiredDirection, moveInput);
            DebugActiveDrift();
        }
    }

    private void TryStartDrift(Vector3 desiredDirection, Vector2 moveInput)
    {
        if (currentSpeed < minSpeedToStartDrift)
            return;

        if (moveInput.magnitude < minInputMagnitudeToChooseSide)
            return;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return;

        Vector3 referenceDirection = useCartForwardForEntrySide
            ? GetCartForward()
            : GetPathDirection();

        float entryAngle = Vector3.SignedAngle(
            referenceDirection,
            desiredDirection.normalized,
            Vector3.up
        );

        if (Mathf.Abs(entryAngle) < minInputAngleToChooseSide)
            return;

        driftSign = Mathf.Sign(entryAngle);

        if (Mathf.Approximately(driftSign, 0f))
            return;

        entryForward = GetCartForward();
        entryInputDirection = desiredDirection.normalized;
        entryPathDirection = GetPathDirection();

        driftState = DriftState.Drifting;
        currentInputMode = DriftInputMode.Holding;
        OnDriftStarted?.Invoke();
        debugTimer = 0f;
        sideSwitchCount = 0;

        // Entry input uses the same direct mapping rule.
        // forceLiveMap = true lets drift entry set tightness immediately,
        // even if the stick magnitude is below the high hold deadzone.
        UpdateTightnessDirectMapping(desiredDirection, moveInput, true);

        if (debugDriftState)
        {
            Debug.Log(
                $"[Drift Prototype] START {DriftSideName} Drift | " +
                $"entryAngle: {entryAngle:F1}, " +
                $"mappedTightness: {currentTightness:F2}, " +
                $"steer: {DriftSteeringAngle:F1}, " +
                $"speed: {currentSpeed:F1}"
            );
        }
    }

    private void UpdateTightnessDirectMapping(
        Vector3 desiredDirection,
        Vector2 moveInput,
        bool forceLiveMap = false
    )
    {
        float moveMagnitude = moveInput.magnitude;

        bool shouldHold =
            !forceLiveMap &&
            (moveMagnitude < driftInputHoldDeadzone || desiredDirection.sqrMagnitude < 0.001f);

        if (shouldHold)
        {
            // No input / soft input preserves the previous tightness and current drift side.
            currentInputMode = DriftInputMode.Holding;
            return;
        }

        Vector3 referenceDirection = usePathDirectionForTightness
            ? GetPathDirection()
            : GetCartForward();

        currentInputAngle = Vector3.SignedAngle(
            referenceDirection,
            desiredDirection.normalized,
            Vector3.up
        );

        TrySwitchDriftSideFromInput(desiredDirection, moveInput);

        currentSameSideAngle = currentInputAngle * driftSign;

        currentTightness = Mathf.InverseLerp(
            wideSameSideAngle,
            tightSameSideAngle,
            currentSameSideAngle
        );

        currentTightness = Mathf.Clamp01(currentTightness);
        currentInputMode = DriftInputMode.LiveMapped;
    }

    private void TrySwitchDriftSideFromInput(Vector3 desiredDirection, Vector2 moveInput)
    {
        if (lockDriftSide)
            return;

        if (!IsDrifting)
            return;

        if (moveInput.magnitude < sideSwitchMinInputMagnitude)
            return;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return;

        if (Mathf.Abs(currentInputAngle) < sideSwitchMinAngleFromReference)
            return;

        float inputSideSign = Mathf.Sign(currentInputAngle);

        if (Mathf.Approximately(inputSideSign, 0f))
            return;

        if (Mathf.Approximately(inputSideSign, driftSign))
            return;

        float oldSign = driftSign;
        driftSign = inputSideSign;
        sideSwitchCount++;

        if (updateEntryDebugOnSideSwitch)
        {
            entryForward = GetCartForward();
            entryInputDirection = desiredDirection.normalized;
            entryPathDirection = GetPathDirection();
        }

        if (debugSideSwitch)
        {
            string oldSide = oldSign > 0f ? "Right" : "Left";
            string newSide = driftSign > 0f ? "Right" : "Left";

            Debug.Log(
                $"[Drift Prototype] SIDE SWITCH {oldSide} -> {newSide} | " +
                $"inputAngle:{currentInputAngle:F1}, " +
                $"switchCount:{sideSwitchCount}"
            );
        }
    }
    public void InterruptDrift(string reason = "Interrupted")
    {
        if (driftState == DriftState.None || driftState == DriftState.DriftBlockedUntilRelease)
            return;

        if (debugDriftState)
            Debug.Log($"[Drift Prototype] INTERRUPTED: {reason}");

        OnDriftInterrupted?.Invoke(reason);

        ForceClearDrift();

        // Important:
        // If the player is still holding RB after crash/interruption,
        // do not allow drift to instantly restart.
        driftState = DriftState.DriftBlockedUntilRelease;
        currentInputMode = DriftInputMode.Holding;
    }
    private void DebugActiveDrift()
    {
        if (!debugDriftState)
            return;

        debugTimer += Time.deltaTime;

        if (debugTimer < debugLogInterval)
            return;

        debugTimer = 0f;

        Debug.Log(
            $"[Drift Prototype] HOLD {DriftSideName} Drift | " +
            $"lockSide:{lockDriftSide}, " +
            $"switches:{sideSwitchCount}, " +
            $"mode:{currentInputMode}, " +
            $"inputAngle:{currentInputAngle:F1}, " +
            $"sameSideAngle:{currentSameSideAngle:F1}, " +
            $"tightness:{currentTightness:F2}, " +
            $"steer:{DriftSteeringAngle:F1}, " +
            $"speed:{currentSpeed:F1}"
        );
    }

    private void EndDrift(bool printDebug)
    {
        if (driftState == DriftState.None)
            return;

        if (printDebug && debugDriftState)
        {
            Debug.Log($"[Drift Prototype] END {DriftSideName} Drift");
        }
        OnDriftEndedClean?.Invoke("Released drift button");
        ForceClearDrift();
    }
    public void CancelDriftForSpeedup(string reason = "Speedup started")
    {
        if (driftState == DriftState.None)
            return;

        if (debugDriftState)
            Debug.Log($"[Drift Prototype] CANCELLED CLEANLY: {reason}");

        OnDriftEndedClean?.Invoke(reason);

        ForceClearDrift();
    }
    private void ForceClearDrift()
    {
        driftState = DriftState.None;
        currentInputMode = DriftInputMode.None;

        driftSign = 0f;
        currentInputAngle = 0f;
        currentSameSideAngle = 0f;
        currentSpeed = 0f;
        currentTightness = 0f;
        debugTimer = 0f;
        sideSwitchCount = 0;

        entryForward = Vector3.zero;
        entryInputDirection = Vector3.zero;
        entryPathDirection = Vector3.zero;
    }

    private float GetPlanarSpeed()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);
        return planarVelocity.magnitude;
    }

    private Vector3 GetPathDirection()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);

        if (planarVelocity.sqrMagnitude > 0.1f)
            return planarVelocity.normalized;

        return GetCartForward();
    }

    private Vector3 GetCartForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(cartBody.transform.forward, Vector3.up);

        if (forward.sqrMagnitude > 0.001f)
            return forward.normalized;

        return Vector3.forward;
    }
}