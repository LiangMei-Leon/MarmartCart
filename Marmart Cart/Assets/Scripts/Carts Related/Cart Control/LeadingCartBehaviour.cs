using System.Collections;
using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

public class LeadingCartBehaviour : MonoBehaviour
{
    public enum WheelRole
    {
        Front,
        Rear
    }

    [Tooltip("Refer to the script that read/gather the new input system")]
    [SerializeField] CartControlScript cartControlInput;
    [SerializeField] private Rigidbody cartBody;

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask layerMask;
    private bool isGrounded = false;

    [Header("Suspension Settings")]
    private Vector3 rayStartPosition;
    [SerializeField] float springRestLength = 1f;
    [SerializeField] float springRaycastExtraLength = 0.1f;
    [SerializeField] float springStrength = 100f;
    [SerializeField] float springDamping = 10f;
    private Vector3 springDirection;

    [Header("Steering Settings")]
    private Vector3 steeringDirection;
    private Vector3 wheelVelocity;
    [SerializeField] AnimationCurve wheelGripCurve;
    [SerializeField] float maxLateralVelocity = 6f;
    [SerializeField] float wheelMass = 1.5f;

    [Header("Drift Grip Experiment")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private WheelRole wheelRole = WheelRole.Front;
    [Tooltip("Master toggle for this physics experiment.")]
    [SerializeField] private bool enableDriftGripExperiment = false;
    [Tooltip("0 = keep original path. 1 = full drift grip effect.")]
    [SerializeField, Range(0f, 1f)] private float driftGripInfluence = 0f;
    [Tooltip("Front wheels usually stay planted so the cart remains controllable.")]
    [SerializeField, Range(0f, 1.5f)] private float frontDriftGripMultiplier = 1f;
    [Tooltip("Rear grip when CurrentTightness is 0. Lower = looser wide drift.")]
    [SerializeField, Range(0f, 1.5f)] private float rearGripAtWideDrift = 0.55f;
    [Tooltip("Rear grip when CurrentTightness is 1. Higher = more controlled tight drift.")]
    [SerializeField, Range(0f, 1.5f)] private float rearGripAtTightDrift = 0.75f;
    [Header("Drift Grip Debug")]
    [SerializeField] private bool debugDriftGrip = false;
    [SerializeField] private float debugDriftGripLogInterval = 0.35f;
    private float currentDriftGripMultiplier = 1f;
    private float driftGripDebugTimer = 0f;

    [Header("Acceleration and Brake Settings")]
    [SerializeField] AnimationCurve engineTorqueCurve;
    [SerializeField] float maxEngineTorque = 100f;
    [SerializeField] float thresholdSpeed = 0.1f;  // Small threshold to treat near-zero speeds as zero
    public float regularMaxSpeed = 10f;        // Regular forward speed
    [SerializeField] float minSpeed = 5f;          // minimum speed cap
    [SerializeField] float brakeFactor = 1f;
    [SerializeField] float maxBrakeForce = 1f;
    [SerializeField] private float targetSpeed = 20f;
    [SerializeField] private float upSpeed = 25f;
    private float cacheSpeed = 20f;
    private bool isStopping = false;
    private Vector3 finalSuspensionForce;
    private Vector3 finalSteeringForce;
    private Vector3 finalBrakeForce;

    [Header("Dynamic Speed by Chain Length")]
    [SerializeField] private SnakeCartManager snakeCartManager;

    [SerializeField] private int fullSpeedUntilCarts = 5;
    [SerializeField] private float fullSpeed = 25f;

    [SerializeField] private float speedLossPerCart = 0.3f;
    [SerializeField] private float minBaseSpeed = 10f;

    [SerializeField] private float upSpeedBonus = 5f; // upSpeed = base + 5
    [SerializeField] private bool excludeLeadingCartFromCount = false;

    [Header("Boost Settings")]
    [SerializeField] private float boostedSpeed = 20f;
    [SerializeField] private float boostDuration = 2f;
    [SerializeField] private float speedLerpRate = 5f;
    //[SerializeField] private float boostForce = 80f;       // Force applied to the cart to boost
    //[SerializeField] private float boostTime = 1f;     // Duration to hold the boosted speed
    //[SerializeField] private float decelerationRate = 10f; // Rate at which the cart returns to normal speed
    public bool isBoosting = false;                       // Flag to track if boost is active

    [Header("Drift Speed Test")]
    [SerializeField] private bool enableDriftSpeedOverride = true;

    [Tooltip("If true, drift uses a fixed target speed. If false, drift uses base speed * multiplier.")]
    [SerializeField] private bool useFixedDriftSpeed = false;

    [Tooltip("Recommended first test: normal 20, drift 16-18, boost 25.")]
    [SerializeField] private float fixedDriftSpeed = 17f;

    [Tooltip("Used when Use Fixed Drift Speed is false. Example: 0.85 means drift is 85% of normal speed.")]
    [SerializeField, Range(0.1f, 1.5f)] private float driftSpeedMultiplier = 0.85f;

    [Tooltip("If true, SpeedUp / boost-speed input cannot raise target speed during drift.")]
    [SerializeField] private bool blockSpeedUpInputWhileDrifting = true;

    [Tooltip("If true, StartBoost() is ignored while drifting.")]
    [SerializeField] private bool blockBoostAbilityWhileDrifting = true;

    [Tooltip("If true, entering drift while boosting cancels the boost coroutine.")]
    [SerializeField] private bool cancelBoostWhenDriftStarts = true;

    [Header("Drift Fail Conditions")]
    [SerializeField] private bool interruptDriftWhenSetSpeedToZero = true;

    [SerializeField] private bool debugDriftFail = true;
    [Header("Events")]
    [SerializeField] GameEvent disableDetachEvent;

    [Header("Runtime Grip Debug")]
    [SerializeField] private string debugWheelName = "Wheel";

    public string DebugWheelName => debugWheelName;
    public WheelRole DebugWheelRole => wheelRole;

    public float DebugLateralVelocity => debugLateralVelocity;
    public float DebugNormalizedLateralVelocity => debugNormalizedLateralVelocity;
    public float DebugBaseGripFactor => debugBaseGripFactor;
    public float DebugDriftGripMultiplier => currentDriftGripMultiplier;
    public float DebugFinalGripFactor => debugFinalGripFactor;
    public float DebugSteeringForceMagnitude => debugSteeringForceMagnitude;

    private float debugLateralVelocity = 0f;
    private float debugNormalizedLateralVelocity = 0f;
    private float debugBaseGripFactor = 0f;
    private float debugFinalGripFactor = 0f;
    private float debugSteeringForceMagnitude = 0f;
    void Awake()
    {
        // Warn the user if the Rigidbody is not assigned
        if (cartBody == null)
        {
            Debug.LogError("Rigidbody is not assigned! Please assign the Rigidbody for the cartBody in the Inspector.");
        }
    }

    void Start()
    {
        if (!snakeCartManager)
            snakeCartManager = GetComponentInParent<SnakeCartManager>();
    }
    void Update()
    {
        if (cartControlInput.GetIsInPit() || isStopping)
            return;

        bool isDrifting = driftController != null && driftController.IsDrifting;

        if (isDrifting && isBoosting && cancelBoostWhenDriftStarts)
        {
            CancelBoost("Drift started");
        }

        // If boost coroutine is controlling speed, do not overwrite targetSpeed here.
        if (isBoosting)
            return;

        int carts = GetEffectiveCartCount();
        float baseSpeed = ComputeBaseSpeed(carts);
        float dynamicUpSpeed = baseSpeed + upSpeedBonus;

        if (isDrifting && enableDriftSpeedOverride)
        {
            targetSpeed = GetDriftTargetSpeed(baseSpeed);
            return;
        }

        bool canUseSpeedUp =
            cartControlInput.IsSpeedingUp() &&
            cartControlInput.CanSpeedingUp();

        if (isDrifting && blockSpeedUpInputWhileDrifting)
            canUseSpeedUp = false;

        if (canUseSpeedUp)
            targetSpeed = dynamicUpSpeed;
        else
            targetSpeed = baseSpeed;
    }
    void FixedUpdate()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -1 * transform.up, out hit, springRestLength + springRaycastExtraLength, layerMask))
        {
            // Initial Debug on if the raycast is correctly hitting something
            // Debug.Log(gameObject.name + "hit" + hit.collider.gameObject.name);
            isGrounded = true;

            #region Suspension System Code

            // Calculate the spring's upward direction (local up of the wheel).
            springDirection = transform.up;

            // Get the velocity of the wheel at its position.
            wheelVelocity = cartBody.GetPointVelocity(transform.position);

            // Project the wheel's velocity onto the spring direction.
            float wheelVelOnSpringDir = Vector3.Dot(springDirection, wheelVelocity);

            // Calculate the compression/extension offset of the spring.
            float offset = springRestLength - hit.distance;

            // Calculate the suspension force based on the spring compression and damping.
            float force = (offset * springStrength) - (wheelVelOnSpringDir * springDamping);

            // Apply the final suspension force upward at the wheel's position.
            finalSuspensionForce = springDirection * force;
            cartBody.AddForceAtPosition(finalSuspensionForce, transform.position);

            #endregion

            #region Steering System Code
            
                // Set right as the steering direction (lateral sliding axis).
                steeringDirection = transform.right;

                // Calculate the velocity of the wheel along the steering direction (sideways).
                float lateralVel = Vector3.Dot(steeringDirection, wheelVelocity);
                // Debug.Log(lateralVel);
                // Normalize lateral velocity by max steering velocity
                float normalizedLateralVelocity = Mathf.Clamp01(Mathf.Abs(lateralVel) / maxLateralVelocity);

                // Evaluate grip factor from curve (0 = no grip, 1 = full grip)
                float gripFactor = wheelGripCurve.Evaluate(normalizedLateralVelocity);
                debugBaseGripFactor = gripFactor;

                // Drift experiment:
                // This multiplier only affects lateral correction force.
                // It does not change wheel angle, suspension, or forward drive.
                currentDriftGripMultiplier = GetDriftGripMultiplier();
                gripFactor *= currentDriftGripMultiplier;

                // Runtime debug values.
                debugLateralVelocity = lateralVel;
                debugNormalizedLateralVelocity = normalizedLateralVelocity;
                debugFinalGripFactor = gripFactor;

                // Calculate the desired velocity change to stop sliding.
                float desiredVelChange = -1 * lateralVel * gripFactor;

                // Calculate the acceleration needed to stop sliding within the fixed time step.
                float desiredAccelration = desiredVelChange / Time.fixedDeltaTime;

                // Apply the force to cancel sliding (F = m * a), in the direction opposite to sliding.
                finalSteeringForce = steeringDirection * wheelMass * desiredAccelration;

                debugSteeringForceMagnitude = finalSteeringForce.magnitude;

                // Apply the force at the wheel's position to counteract the lateral sliding.
                cartBody.AddForceAtPosition(finalSteeringForce, transform.position);

                DebugDriftGrip(lateralVel, gripFactor);

            #endregion

            /*
            #region Acceleration and Brake System

            Vector3 accelDirection = transform.forward;
            float cartSpeed = Vector3.Dot(cartBody.gameObject.transform.forward, cartBody.linearVelocity);
            Vector3 desiredDirection = cartControlInput.desiredDirection;
            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                if (cartSpeed < regularMaxSpeed)
                {
                    float normalizedCartSpeed = Mathf.Clamp01(Mathf.Abs(cartSpeed) / regularMaxSpeed);
                    float availableTorque = engineTorqueCurve.Evaluate(normalizedCartSpeed);
                    cartBody.AddForceAtPosition(accelDirection * availableTorque * maxEngineTorque, transform.position);
                }
            }
            else
            {
                Brake();
            }
            #endregion
            */
            #region Constant Forward Drive System

            Vector3 accelDirection = transform.forward;
            float cartSpeed = Vector3.Dot(cartBody.transform.forward, cartBody.linearVelocity);

            // Always attempt to maintain targetSpeed if grounded
            if (isGrounded)
            {
                float speedError = targetSpeed - cartSpeed;

                if (speedError > 0.1f)
                {
                    float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(cartSpeed) / targetSpeed);
                    float availableTorque = engineTorqueCurve.Evaluate(normalizedSpeed);
                    float acceleration = speedError / Time.fixedDeltaTime;

                    float forceMag = Mathf.Min(acceleration * cartBody.mass, maxEngineTorque * availableTorque);
                    Vector3 forwardForce = accelDirection * forceMag;

                    cartBody.AddForceAtPosition(forwardForce, transform.position);
                }
            }
            #endregion
            // Brake the cart, slow to zero now
            //if (isBraking)
            //    Brake();
        }
        else
        {
            isGrounded = false;
        }
    }
    public void StartBoost()
    {
        if (blockBoostAbilityWhileDrifting && driftController != null && driftController.IsDrifting)
        {
            if (debugDriftFail)
                Debug.Log("[LeadingCartBehaviour] Boost ignored because cart is drifting.");

            return;
        }

        cartControlInput.DisallowFlip();

        if (!isBoosting)
            StartCoroutine(BoostCoroutine());
    }
    private void CancelBoost(string reason)
    {
        if (!isBoosting)
            return;

        StopAllCoroutines();

        isBoosting = false;
        cartControlInput.EnableControl();
        cartControlInput.DisallowFlip();

        if (debugDriftFail)
            Debug.Log($"[LeadingCartBehaviour] Boost cancelled: {reason}");
    }
    private IEnumerator BoostCoroutine()
    {
        isBoosting = true;
        cartControlInput.DisableControl();

        float originalSpeed = targetSpeed;
        float timeElapsed = 0f;

        // Step 1: Ramp up to boostedSpeed
        while (timeElapsed < boostDuration * 0.25f)
        {
            targetSpeed = Mathf.Lerp(originalSpeed, boostedSpeed, timeElapsed / (boostDuration * 0.25f));
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        targetSpeed = boostedSpeed;

        // Step 2: Hold boosted speed
        yield return new WaitForSeconds(boostDuration * 0.5f);

        // Step 3: Ramp down
        timeElapsed = 0f;
        while (timeElapsed < boostDuration * 0.25f)
        {
            targetSpeed = Mathf.Lerp(boostedSpeed, originalSpeed, timeElapsed / (boostDuration * 0.25f));
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        targetSpeed = originalSpeed;
        cartControlInput.EnableControl();
        cartControlInput.DisallowFlip();
        isBoosting = false;
    }

    public void Brake()
    {
        if (isGrounded)
        {
            Vector3 accelDirection = transform.forward;
            float cartSpeed = Vector3.Dot(cartBody.gameObject.transform.forward, cartBody.linearVelocity);

            if (cartSpeed > minSpeed)
            {
                float desiredBrakeVelChange = -cartSpeed * brakeFactor;
                float desiredBrakeAcceleration = desiredBrakeVelChange / Time.fixedDeltaTime;
                float brakeForceMagnitude = Mathf.Min(Mathf.Abs(desiredBrakeAcceleration * cartBody.mass), maxBrakeForce);

                Vector3 finalBrakeForce = -accelDirection * brakeForceMagnitude;
                cartBody.AddForceAtPosition(finalBrakeForce, transform.position);
            }
        }
    }

    public void Reset()
    {
        // Debug.Log("attempt to flip the cart");
        disableDetachEvent.Raise();
        cartControlInput.DisallowFlip();
        cartControlInput.AllowActivatePowerUp();
        Vector3 desiredFacingDirection = -1 * cartBody.gameObject.transform.forward;
        cartBody.gameObject.transform.rotation = Quaternion.LookRotation(desiredFacingDirection);

    }
    private float ComputeBaseSpeed(int carts)
    {
        if (carts <= fullSpeedUntilCarts)
            return fullSpeed;

        float raw = fullSpeed - speedLossPerCart * (carts - fullSpeedUntilCarts);
        return Mathf.Max(minBaseSpeed, raw);
    }
    public void SetSpeedToZero(float duration)
    {
        InterruptDriftFromCrash("SetSpeedToZero duration");

        isStopping = true;
        cacheSpeed = targetSpeed;
        targetSpeed = 0f;
        Vector3 knockbackDir = -cartBody.linearVelocity.normalized;
        float knockbackForce = 200f; // Make this punchy

        // Instant stop
        cartBody.linearVelocity = Vector3.zero;

        // Apply force at the offset point
        cartBody.AddForceAtPosition(knockbackDir * knockbackForce, transform.position, ForceMode.Impulse);
        Invoke(nameof(ResetSpeed), duration);
    }
    
    public void SetSpeedToZero()
    {
        InterruptDriftFromCrash("SetSpeedToZero duration");

        isStopping = true;
        cacheSpeed = targetSpeed;
        targetSpeed = 0f;
        cartBody.linearVelocity = Vector3.zero;
    }
    public void ResetSpeed()
    {
        isStopping = false;
        isBoosting = false;
        //Debug.Log("ResetSpeed executed");
        //targetSpeed = 20f;
        //cartControlInput.AllowBoost();
    }
    private void InterruptDriftFromCrash(string reason)
    {
        if (!interruptDriftWhenSetSpeedToZero)
            return;

        if (driftController == null)
            return;

        if (!driftController.IsDrifting && !driftController.IsDriftArmed)
            return;

        driftController.InterruptDrift(reason);

        if (debugDriftFail)
            Debug.Log($"[LeadingCartBehaviour] Drift failed/interrupted: {reason}");
    }
    private int GetEffectiveCartCount()
    {
        if (!snakeCartManager) return 0;

        int count = snakeCartManager.GetSnakeBodyLength(); // includes leading cart
        if (excludeLeadingCartFromCount)
            count = Mathf.Max(0, count - 1);

        return count;
    }
    private float GetDriftGripMultiplier()
    {
        if (!enableDriftGripExperiment)
            return 1f;

        if (driftController == null || !driftController.IsDrifting)
            return 1f;

        float targetMultiplier;

        if (wheelRole == WheelRole.Front)
        {
            targetMultiplier = frontDriftGripMultiplier;
        }
        else
        {
            float tightness = Mathf.Clamp01(driftController.CurrentTightness);

            targetMultiplier = Mathf.Lerp(
                rearGripAtWideDrift,
                rearGripAtTightDrift,
                tightness
            );
        }

        // Safety blend:
        // 0 influence = original grip
        // 1 influence = full drift multiplier
        return Mathf.Lerp(1f, targetMultiplier, driftGripInfluence);
    }
    private float GetDriftTargetSpeed(float baseSpeed)
    {
        if (useFixedDriftSpeed)
            return fixedDriftSpeed;

        return baseSpeed * driftSpeedMultiplier;
    }
    private void DebugDriftGrip(float lateralVel, float finalGripFactor)
    {
        if (!debugDriftGrip)
            return;

        if (driftController == null || !driftController.IsDrifting)
            return;

        driftGripDebugTimer += Time.fixedDeltaTime;

        if (driftGripDebugTimer < debugDriftGripLogInterval)
            return;

        driftGripDebugTimer = 0f;

        Debug.Log(
            $"[Drift Grip Experiment] {gameObject.name} | " +
            $"role: {wheelRole}, " +
            $"tightness: {driftController.CurrentTightness:F2}, " +
            $"multiplier: {currentDriftGripMultiplier:F2}, " +
            $"lateralVel: {lateralVel:F2}, " +
            $"finalGripFactor: {finalGripFactor:F2}"
        );
    }
    void OnDrawGizmos()
    {
        // Calculate the RayStartPosition in OnDrawGizmos so it updates in the editor
        // rayStartPosition = transform.TransformPoint(new Vector3(xAxisOffset, yAxisOffset, zAxisOffset));

        //  Draw the length of the spring
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + new Vector3(0.1f, 0, 0), -1 * transform.up * springRestLength);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(rayStartPosition, 0.015f);
        Gizmos.DrawRay(transform.position, -1 * transform.up * (springRestLength + springRaycastExtraLength));
        // Gizmos.color = Color.gray;
        // Gizmos.DrawRay(transform.position, transform.right * 0.35f);
        // Visualize the force direction and magnitude
        if (Application.isPlaying) // Only draw the force vector during play mode
        {
            Gizmos.color = Color.green;
            // Scale the force vector for visualization
            float forceVisualizationScale = 0.001f; // Adjust this value to make the force more or less visible
            Gizmos.DrawRay(transform.position, finalSuspensionForce * forceVisualizationScale);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, finalSteeringForce);
            Gizmos.color = Color.black;
            Gizmos.DrawRay(transform.position, finalBrakeForce);
        }
    }
}