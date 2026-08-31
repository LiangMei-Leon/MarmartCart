using System.Collections;
using UnityEngine;

/// <summary>
/// Per-wheel raycast suspension / lateral grip / forward drive for the leading cart.
///
/// This component is still intended to live on each raycast wheel object.
/// Assign the same CartMovementProfile to all leading-cart wheel scripts.
///
/// Movement design:
/// - Normal Drive uses chain-adjusted base speed.
/// - Drift uses chain-adjusted base speed with tight-drift dip and recoverable fatigue.
/// - Speedup uses chain-adjusted base speed + additive fuel speedup bonus.
/// - Turn assist only changes engine authority. It does not modify lateral counter-force.
/// </summary>
public class LeadingCartBehaviour : MonoBehaviour
{
    #region References

    [Header("References")]
    [Tooltip("Reads movement, speedup, pit, and control state.")]
    [SerializeField] private CartControlScript cartControlInput;

    [Tooltip("Main rigidbody of the leading cart.")]
    [SerializeField] private Rigidbody cartBody;
    public Rigidbody CartBody => cartBody;

    [Tooltip("Shared movement tuning asset for Drive / Drift / Speedup.")]
    [SerializeField] private CartMovementProfile movementProfile;

    [Tooltip("Used to detect drift mode and drift tightness.")]
    [SerializeField] private CartDriftController driftController;

    [Tooltip("Used by the chain-length speed penalty.")]
    [SerializeField] private SnakeCartManager snakeCartManager;

    #endregion

    #region Raycast Settings

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask layerMask;

    private bool isGrounded = false;

    #endregion

    #region Suspension Settings

    [Header("Suspension Settings")]
    [SerializeField] private float springRestLength = 1f;
    [SerializeField] private float springRaycastExtraLength = 0.1f;
    [SerializeField] private float springStrength = 100f;
    [SerializeField] private float springDamping = 10f;

    private Vector3 springDirection;
    private Vector3 wheelVelocity;

    #endregion

    #region Lateral Grip Settings

    [Header("Lateral Grip Settings")]
    [Tooltip("Curve that decides how strongly the wheel cancels lateral sliding.")]
    [SerializeField] private AnimationCurve wheelGripCurve;

    [Tooltip("Lateral speed used to normalize the wheel grip curve.")]
    [SerializeField] private float maxLateralVelocity = 6f;

    [Tooltip("Effective wheel mass used for lateral correction force.")]
    [SerializeField] private float wheelMass = 1.5f;

    private Vector3 steeringDirection;

    #endregion

    #region Forward Drive Settings

    [Header("Forward Drive Settings")]
    [Tooltip("Engine torque available at normalized speed 0..1.")]
    [SerializeField] private AnimationCurve engineTorqueCurve;

    [Tooltip("Base cap for forward drive force. Mode profile turn assist multiplies this value during turns.")]
    [SerializeField] private float maxEngineTorque = 100f;

    [Tooltip("Current speed target calculated from movement profile + chain length + mode.")]
    [SerializeField] private float targetSpeed = 20f;

    private CartDriveMode currentDriveMode = CartDriveMode.NormalDrive;

    #endregion

    #region Brake Settings

    [Header("Brake Settings")]
    [SerializeField] private float minSpeed = 5f;
    [SerializeField] private float brakeFactor = 1f;
    [SerializeField] private float maxBrakeForce = 1f;

    #endregion

    #region Chain-Length Speed Penalty

    [Header("Chain-Length Speed Penalty")]
    [Tooltip("No speed penalty while the snake body length is at or below this value.")]
    [SerializeField] private int fullSpeedUntilCarts = 5;

    [Tooltip("Base speed loss for each cart beyond Full Speed Until Carts.")]
    [SerializeField] private float speedLossPerCart = 0.3f;

    [Tooltip("If true, the leading cart is not counted when applying speed penalty.")]
    [SerializeField] private bool excludeLeadingCartFromCount = false;

    #endregion

    #region Powerup Boost Settings

    [Header("Powerup Boost Settings")]
    [Tooltip("Separate powerup boost target speed. This is not the fuel speedup mode.")]
    [SerializeField] private float boostedSpeed = 20f;

    [SerializeField] private float boostDuration = 2f;

    [Tooltip("Kept for existing tuning compatibility. The current boost coroutine uses duration sections.")]
    [SerializeField] private float speedLerpRate = 5f;

    public bool isBoosting = false;

    private Coroutine boostRoutine;

    #endregion

    #region Crash / Stop State

    private bool isStopping = false;

    #endregion

    #region Drift Runtime State

    private bool wasDriftingLastFrame = false;

    private float currentDriftDuration = 0f;
    private float currentDriftFatigue = 0f;

    private float tightDriftDipTimer = 0f;
    private bool tightDriftDipArmed = true;

    #endregion

    #region Events

    [Header("Events")]
    [SerializeField] private GameEvent disableDetachEvent;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartBody == null)
            Debug.LogError("Rigidbody is not assigned. Please assign the cartBody Rigidbody in the Inspector.", this);

        if (movementProfile == null)
            Debug.LogWarning("CartMovementProfile is not assigned. Using fallback movement values.", this);
    }

    private void Start()
    {
        if (!snakeCartManager)
            snakeCartManager = GetComponentInParent<SnakeCartManager>();

        if (!driftController)
            driftController = GetComponentInParent<CartDriftController>();
    }

    private void Update()
    {
        UpdateDriveModeAndTargetSpeed(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (cartBody == null)
            return;

        RaycastHit hit;

        bool didHitGround = Physics.Raycast(
            transform.position,
            -transform.up,
            out hit,
            springRestLength + springRaycastExtraLength,
            layerMask
        );

        if (!didHitGround)
        {
            isGrounded = false;
            return;
        }

        isGrounded = true;

        ApplySuspension(hit);
        ApplyLateralGrip();
        ApplyForwardDrive();
    }

    #endregion

    #region Mode / Target Speed

    private void UpdateDriveModeAndTargetSpeed(float deltaTime)
    {
        if (cartControlInput == null)
            return;

        if (cartControlInput.GetIsInPit() || isStopping)
            return;

        bool isDrifting = driftController != null && driftController.IsDrifting;

        UpdateDriftRuntimeState(isDrifting, deltaTime);

        // Powerup boost owns targetSpeed while its coroutine is active.
        if (isDrifting && isBoosting && ShouldCancelPowerupBoostWhenDrifting())
            CancelBoost();

        if (isBoosting)
            return;

        currentDriveMode = ResolveDriveMode(isDrifting);

        float chainAdjustedBaseSpeed = ComputeChainAdjustedBaseSpeed();
        targetSpeed = ComputeTargetSpeed(currentDriveMode, chainAdjustedBaseSpeed);
    }

    private void UpdateDriftRuntimeState(bool isDrifting, float deltaTime)
    {
        if (!isDrifting)
        {
            ResetDriftRuntimeState();
            return;
        }

        if (!wasDriftingLastFrame)
        {
            currentDriftDuration = 0f;
            currentDriftFatigue = 0f;
            tightDriftDipTimer = 0f;
            tightDriftDipArmed = true;
        }

        wasDriftingLastFrame = true;
        currentDriftDuration += deltaTime;

        float tightness = GetDriftTightness();

        UpdateTightDriftDip(tightness, deltaTime);
        UpdateDriftFatigue(tightness, deltaTime);
    }

    private void ResetDriftRuntimeState()
    {
        wasDriftingLastFrame = false;

        currentDriftDuration = 0f;
        currentDriftFatigue = 0f;

        tightDriftDipTimer = 0f;
        tightDriftDipArmed = true;
    }

    private void UpdateTightDriftDip(float tightness, float deltaTime)
    {
        CartDriftFeelSettings driftFeel = GetDriftFeelSettings();

        if (!driftFeel.enableTightDriftDip)
        {
            tightDriftDipTimer = 0f;
            tightDriftDipArmed = true;
            return;
        }

        // Re-arm only when the player returns to wide enough drift.
        if (tightness <= driftFeel.tightDriftDipRearmTightness)
            tightDriftDipArmed = true;

        // Trigger the dip immediately when entering tight-drift range.
        if (tightDriftDipArmed && tightness >= driftFeel.tightDriftDipTriggerTightness)
        {
            tightDriftDipTimer = driftFeel.tightDriftDipDuration;
            tightDriftDipArmed = false;
        }

        if (tightDriftDipTimer > 0f)
            tightDriftDipTimer = Mathf.Max(0f, tightDriftDipTimer - deltaTime);
    }

    private void UpdateDriftFatigue(float tightness, float deltaTime)
    {
        CartDriftFeelSettings driftFeel = GetDriftFeelSettings();

        if (!driftFeel.enableDriftFatigueSpeedLoss)
        {
            currentDriftFatigue = 0f;
            return;
        }

        float buildAmount = driftFeel.GetFatigueBuildAmount(tightness, deltaTime);
        float recoverAmount = driftFeel.GetFatigueRecoverAmount(tightness, deltaTime);

        currentDriftFatigue += buildAmount;
        currentDriftFatigue -= recoverAmount;

        currentDriftFatigue = Mathf.Clamp01(currentDriftFatigue);
    }

    private CartDriveMode ResolveDriveMode(bool isDrifting)
    {
        if (isDrifting)
            return CartDriveMode.Drift;

        bool isUsingFuelSpeedup =
            cartControlInput.IsSpeedingUp() &&
            cartControlInput.CanSpeedingUp();

        if (isUsingFuelSpeedup)
            return CartDriveMode.Speedup;

        return CartDriveMode.NormalDrive;
    }

    private float ComputeTargetSpeed(CartDriveMode mode, float chainAdjustedBaseSpeed)
    {
        switch (mode)
        {
            case CartDriveMode.Speedup:
                return Mathf.Max(
                    0f,
                    chainAdjustedBaseSpeed + GetSpeedupAdditiveBonus()
                );

            case CartDriveMode.Drift:
                return Mathf.Max(
                    0f,
                    ComputeDriftTargetSpeed(chainAdjustedBaseSpeed)
                );

            default:
                return Mathf.Max(0f, chainAdjustedBaseSpeed);
        }
    }

    private float ComputeDriftTargetSpeed(float chainAdjustedBaseSpeed)
    {
        CartDriftFeelSettings driftFeel = GetDriftFeelSettings();

        float finalMultiplier = 1f;

        // Tight drift dip.
        // This is not tied only to drift start. It triggers whenever the player
        // crosses into tight-drift range after being wide enough to re-arm.
        if (tightDriftDipTimer > 0f)
            finalMultiplier *= driftFeel.tightDriftDipMultiplier;

        // Recoverable fatigue.
        // Tight drifting builds fatigue. Wide drifting recovers fatigue.
        finalMultiplier *= driftFeel.GetFatigueSpeedMultiplier(currentDriftFatigue);

        return chainAdjustedBaseSpeed * finalMultiplier;
    }

    private CartMovementModeSettings GetModeSettings(CartDriveMode mode)
    {
        if (movementProfile != null)
            return movementProfile.GetSettings(mode);

        // Fallback keeps the cart usable if the profile is missing.
        switch (mode)
        {
            case CartDriveMode.Drift:
                return CartMovementModeSettings.CreateDrift();

            case CartDriveMode.Speedup:
                return CartMovementModeSettings.CreateSpeedup();

            default:
                return CartMovementModeSettings.CreateNormalDrive();
        }
    }

    private CartDriftFeelSettings GetDriftFeelSettings()
    {
        if (movementProfile != null && movementProfile.driftFeel != null)
            return movementProfile.driftFeel;

        return CartDriftFeelSettings.CreateDefault();
    }

    #endregion

    #region Suspension

    private void ApplySuspension(RaycastHit hit)
    {
        // The spring pushes along this wheel object's local up direction.
        springDirection = transform.up;

        // Velocity of the rigidbody at this wheel position.
        wheelVelocity = cartBody.GetPointVelocity(transform.position);

        // Velocity along the suspension direction.
        float wheelVelOnSpringDir = Vector3.Dot(springDirection, wheelVelocity);

        // Spring compression amount.
        float offset = springRestLength - hit.distance;

        // Hooke-style spring force with damping.
        float force = (offset * springStrength) - (wheelVelOnSpringDir * springDamping);

        Vector3 suspensionForce = springDirection * force;
        cartBody.AddForceAtPosition(suspensionForce, transform.position);
    }

    #endregion

    #region Lateral Grip / Steering Counter-Force

    private void ApplyLateralGrip()
    {
        // The wheel's local right axis is the lateral sliding axis.
        steeringDirection = transform.right;

        // Sideways velocity at this wheel.
        float lateralVel = Vector3.Dot(steeringDirection, wheelVelocity);

        // Normalize lateral velocity and evaluate grip curve.
        float normalizedLateralVelocity =
            Mathf.Clamp01(Mathf.Abs(lateralVel) / maxLateralVelocity);

        float gripFactor = wheelGripCurve.Evaluate(normalizedLateralVelocity);

        // Desired change to cancel lateral sliding.
        float desiredVelChange = -lateralVel * gripFactor;

        // Required acceleration over this fixed step.
        float desiredAcceleration = desiredVelChange / Time.fixedDeltaTime;

        // F = m * a
        Vector3 steeringForce = steeringDirection * wheelMass * desiredAcceleration;

        // Apply the original full lateral counter-force.
        // Do not modify this for speed scrub, because this force is also what gives
        // the cart its current sharp turning behavior.
        cartBody.AddForceAtPosition(steeringForce, transform.position);
    }

    #endregion

    #region Forward Drive

    private void ApplyForwardDrive()
    {
        if (!isGrounded)
            return;

        if (targetSpeed <= 0.01f)
            return;

        Vector3 accelDirection = transform.forward;
        float cartForwardSpeed = Vector3.Dot(cartBody.transform.forward, cartBody.linearVelocity);

        float speedError = targetSpeed - cartForwardSpeed;

        if (speedError <= 0.1f)
            return;

        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(cartForwardSpeed) / targetSpeed);
        float availableTorque = engineTorqueCurve.Evaluate(normalizedSpeed);

        float acceleration = speedError / Time.fixedDeltaTime;

        float forceCap =
            maxEngineTorque *
            availableTorque *
            GetTurnAssistTorqueMultiplier();

        float forceMagnitude = Mathf.Min(acceleration * cartBody.mass, forceCap);
        Vector3 forwardForce = accelDirection * forceMagnitude;

        cartBody.AddForceAtPosition(forwardForce, transform.position);
    }

    private float GetTurnAssistTorqueMultiplier()
    {
        CartMovementModeSettings settings = GetModeSettings(currentDriveMode);

        if (!settings.enableTurnSpeedAssist)
            return 1f;

        float requestedTurnAngle = GetRequestedTurnAngleForCurrentMode();
        float referenceAngle = Mathf.Max(1f, settings.turnAssistReferenceAngle);

        float normalizedTurnIntensity = Mathf.Clamp01(requestedTurnAngle / referenceAngle);

        return settings.GetTurnAssistTorqueMultiplier(normalizedTurnIntensity);
    }

    private float GetRequestedTurnAngleForCurrentMode()
    {
        if (currentDriveMode == CartDriveMode.Drift &&
            driftController != null &&
            driftController.IsDrifting)
        {
            // Drift steering output is already the requested drift wheel angle.
            return Mathf.Abs(driftController.DriftSteeringAngle);
        }

        if (cartControlInput == null)
            return 0f;

        Vector3 desiredDirection = cartControlInput.desiredDirection;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return 0f;

        Vector3 cartForward = Vector3.ProjectOnPlane(cartBody.transform.forward, Vector3.up);

        if (cartForward.sqrMagnitude < 0.001f)
            return 0f;

        cartForward.Normalize();
        desiredDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up).normalized;

        return Mathf.Abs(
            Vector3.SignedAngle(cartForward, desiredDirection, Vector3.up)
        );
    }

    #endregion

    #region Powerup Boost

    public void StartBoost()
    {
        if (ShouldBlockPowerupBoostWhileDrifting())
            return;

        cartControlInput.DisallowMoveBackward();

        if (!isBoosting)
            boostRoutine = StartCoroutine(BoostCoroutine());
    }

    private IEnumerator BoostCoroutine()
    {
        isBoosting = true;
        cartControlInput.DisableControl();

        float originalSpeed = targetSpeed;
        float timeElapsed = 0f;

        // Step 1: Ramp up to boostedSpeed.
        while (timeElapsed < boostDuration * 0.25f)
        {
            targetSpeed = Mathf.Lerp(
                originalSpeed,
                boostedSpeed,
                timeElapsed / (boostDuration * 0.25f)
            );

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        targetSpeed = boostedSpeed;

        // Step 2: Hold boosted speed.
        yield return new WaitForSeconds(boostDuration * 0.5f);

        // Step 3: Ramp back down.
        timeElapsed = 0f;

        while (timeElapsed < boostDuration * 0.25f)
        {
            targetSpeed = Mathf.Lerp(
                boostedSpeed,
                originalSpeed,
                timeElapsed / (boostDuration * 0.25f)
            );

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        targetSpeed = originalSpeed;
        cartControlInput.EnableControl();
        cartControlInput.DisallowMoveBackward();

        isBoosting = false;
        boostRoutine = null;
    }

    private void CancelBoost()
    {
        if (!isBoosting)
            return;

        if (boostRoutine != null)
            StopCoroutine(boostRoutine);

        boostRoutine = null;
        isBoosting = false;

        cartControlInput.EnableControl();
        cartControlInput.DisallowMoveBackward();
    }

    private bool ShouldBlockPowerupBoostWhileDrifting()
    {
        if (movementProfile == null)
            return false;

        return
            movementProfile.blockPowerupBoostWhileDrifting &&
            driftController != null &&
            driftController.IsDrifting;
    }

    private bool ShouldCancelPowerupBoostWhenDrifting()
    {
        if (movementProfile == null)
            return true;

        return movementProfile.cancelPowerupBoostWhenDriftStarts;
    }

    #endregion

    #region Brake / Reset / Stop

    public void Brake()
    {
        if (!isGrounded)
            return;

        Vector3 accelDirection = transform.forward;
        float cartSpeed = Vector3.Dot(cartBody.transform.forward, cartBody.linearVelocity);

        if (cartSpeed <= minSpeed)
            return;

        float desiredBrakeVelChange = -cartSpeed * brakeFactor;
        float desiredBrakeAcceleration = desiredBrakeVelChange / Time.fixedDeltaTime;
        float brakeForceMagnitude =
            Mathf.Min(Mathf.Abs(desiredBrakeAcceleration * cartBody.mass), maxBrakeForce);

        Vector3 brakeForce = -accelDirection * brakeForceMagnitude;
        cartBody.AddForceAtPosition(brakeForce, transform.position);
    }

    public void Reset()
    {
        disableDetachEvent.Raise();

        cartControlInput.DisallowMoveBackward();
        cartControlInput.AllowActivatePowerUp();

        Vector3 desiredFacingDirection = -cartBody.transform.forward;
        cartBody.transform.rotation = Quaternion.LookRotation(desiredFacingDirection);
    }

    public void SetSpeedToZero(float duration)
    {
        InterruptDriftFromCrash("SetSpeedToZero duration");

        isStopping = true;
        targetSpeed = 0f;

        Vector3 knockbackDir = GetSafeKnockbackDirection();
        float knockbackForce = 200f;

        // Instant stop.
        cartBody.linearVelocity = Vector3.zero;

        // Apply a small punch back from the collision/stopping direction.
        cartBody.AddForceAtPosition(
            knockbackDir * knockbackForce,
            transform.position,
            ForceMode.Impulse
        );

        Invoke(nameof(ResetSpeed), duration);
    }

    public void SetSpeedToZero()
    {
        InterruptDriftFromCrash("SetSpeedToZero");

        isStopping = true;
        targetSpeed = 0f;
        cartBody.linearVelocity = Vector3.zero;
    }

    public void ResetSpeed()
    {
        isStopping = false;
        isBoosting = false;
        boostRoutine = null;
    }

    private Vector3 GetSafeKnockbackDirection()
    {
        Vector3 velocity = cartBody.linearVelocity;

        if (velocity.sqrMagnitude > 0.001f)
            return -velocity.normalized;

        return -cartBody.transform.forward;
    }

    private void InterruptDriftFromCrash(string reason)
    {
        if (!ShouldInterruptDriftWhenSetSpeedToZero())
            return;

        if (driftController == null)
            return;

        if (!driftController.IsDrifting && !driftController.IsDriftArmed)
            return;

        driftController.InterruptDrift(reason);
    }

    private bool ShouldInterruptDriftWhenSetSpeedToZero()
    {
        if (movementProfile == null)
            return true;

        return movementProfile.interruptDriftWhenSetSpeedToZero;
    }

    #endregion

    #region Chain-Length Speed Penalty

    private float ComputeChainAdjustedBaseSpeed()
    {
        int carts = GetEffectiveCartCount();

        float profileBaseSpeed = GetProfileBaseSpeed();
        float profileMinimumBaseSpeed = GetProfileMinimumBaseSpeed();

        if (carts <= fullSpeedUntilCarts)
            return profileBaseSpeed;

        float rawSpeed =
            profileBaseSpeed -
            speedLossPerCart * (carts - fullSpeedUntilCarts);

        return Mathf.Max(profileMinimumBaseSpeed, rawSpeed);
    }

    private int GetEffectiveCartCount()
    {
        if (!snakeCartManager)
            return 0;

        int count = snakeCartManager.GetSnakeBodyLength();

        if (excludeLeadingCartFromCount)
            count = Mathf.Max(0, count - 1);

        return count;
    }

    #endregion

    #region Profile Fallbacks

    private float GetProfileBaseSpeed()
    {
        return movementProfile != null ? movementProfile.baseSpeed : 20f;
    }

    private float GetProfileMinimumBaseSpeed()
    {
        return movementProfile != null ? movementProfile.minimumBaseSpeed : 10f;
    }

    private float GetSpeedupAdditiveBonus()
    {
        return movementProfile != null ? movementProfile.speedupAdditiveBonus : 5f;
    }

    private float GetDriftTightness()
    {
        if (driftController == null || !driftController.IsDrifting)
            return 0f;

        return Mathf.Clamp01(driftController.CurrentTightness);
    }

    #endregion
}