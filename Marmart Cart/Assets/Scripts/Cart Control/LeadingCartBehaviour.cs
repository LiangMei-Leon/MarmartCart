using System.Collections;
using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using TMPro;
using UnityEditor.Timeline.Actions;
using UnityEngine;

public class LeadingCartBehaviour : MonoBehaviour
{
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

    [Header("Acceleration and Brake Settings")]
    [SerializeField] AnimationCurve engineTorqueCurve;
    [SerializeField] float maxEngineTorque = 100f;
    [SerializeField] float thresholdSpeed = 0.1f;  // Small threshold to treat near-zero speeds as zero
    [SerializeField] float regularMaxSpeed = 10f;        // Regular forward speed
    [SerializeField] float minSpeed = 5f;          // minimum speed cap
    [SerializeField] float brakeFactor = 1f;
    [SerializeField] float maxBrakeForce = 1f;

    private Vector3 finalSuspensionForce;
    private Vector3 finalSteeringForce;
    private Vector3 finalBrakeForce;

    [Header("Boost Settings")]
    [SerializeField] private float boostSpeed = 30f;       // Target speed during boost
    [SerializeField] private float boostDuration = 2f;     // Duration to hold the boosted speed
    [SerializeField] private float decelerationRate = 10f; // Rate at which the cart returns to normal speed
    private bool isBoosting = false;                       // Flag to track if boost is active
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

    }
    void Update()
    {

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

            // Calculate the desired velocity change to stop sliding.
            float desiredVelChange = -1 * lateralVel * gripFactor;

            // Calculate the acceleration needed to stop sliding within the fixed time step.
            float desiredAccelration = desiredVelChange / Time.fixedDeltaTime;

            // Apply the force to cancel sliding (F = m * a), in the direction opposite to sliding.
            finalSteeringForce = steeringDirection * wheelMass * desiredAccelration;

            // Apply the force at the wheel's position to counteract the lateral sliding.
            cartBody.AddForceAtPosition(finalSteeringForce, transform.position);

            #endregion

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
        }
        else
        {
            isGrounded = false;
        }
    }
    public void StartBoost()
    {
        // Ensure that the boost is only triggered if not already active
        if (!isBoosting)
        {
            StartCoroutine(BoostCoroutine());
        }
    }

    private IEnumerator BoostCoroutine()
    {
        isBoosting = true;

        // Initial setup for boost direction and force
        Vector3 accelDirection = transform.forward;
        float boostForce = maxEngineTorque; // Set this to a fixed value to provide a constant acceleration
        float boostTime = 0.5f;               // Duration to apply the boost force
        float holdTime = boostDuration;     // Duration to maintain the boosted speed

        // Step 1: Apply consistent boost force for boostTime duration
        float timeElapsed = 0f;
        while (timeElapsed < boostTime)
        {
            // Apply a constant boost force to achieve a consistent acceleration
            cartBody.AddForceAtPosition(accelDirection * boostForce, transform.position);

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Step 2: Hold the boosted speed for holdTime duration
        yield return new WaitForSeconds(holdTime);

        // Step 3: Decelerate gradually back to normal speed
        while (cartBody.linearVelocity.magnitude > regularMaxSpeed)
        {
            Vector3 decelerationForce = -cartBody.linearVelocity.normalized * decelerationRate * Time.deltaTime * cartBody.mass;
            cartBody.AddForce(decelerationForce, ForceMode.Acceleration);

            yield return null;
        }

        isBoosting = false; // Reset the boosting flag
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
        Vector3 desiredFacingDirection = -1 * cartBody.gameObject.transform.forward;
        cartBody.gameObject.transform.rotation = Quaternion.LookRotation(desiredFacingDirection);
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