using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using TMPro;
using UnityEditor.Timeline.Actions;
using UnityEngine;

public class LeadingCartBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Rigidbody cartBody;
    private Vector3 rayStartPosition;
    [SerializeField] float springRestLength = 1f;
    [SerializeField] float springRaycastExtraLength = 0.1f;
    [SerializeField] float springStrength = 100f;
    [SerializeField] float springDamping = 10f;
    private Vector3 springDirection;

    private Vector3 steeringDirection;
    private Vector3 wheelVelocity;
    [SerializeField] AnimationCurve wheelGripCurve;
    [SerializeField] float maxLateralVelocity = 6f;
    [SerializeField] float wheelMass = 1.5f;

    [SerializeField] AnimationCurve engineTorqueCurve;
    [SerializeField] float maxEngineTorque = 100f;
    [SerializeField] float thresholdSpeed = 0.1f;  // Small threshold to treat near-zero speeds as zero
    [SerializeField] float baseSpeed = 10f;         // Minimum forward speed when no input is applied
    [SerializeField] float maxSpeed = 20f;         // Maximum speed cap
    [SerializeField] float minSpeed = 5f;         // minimum speed cap
    private float torque;
    [SerializeField] float brakeFactor = 1f;
    [SerializeField] float maxBrakeForce = 1f;
    [SerializeField] float reverseSpeedCap = 5f;
    private Vector3 finalSuspensionForce;
    private Vector3 finalSteeringForce;
    private Vector3 finalBrakeForce;
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
            Debug.Log(lateralVel);
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
            float input = Input.GetAxis("Vertical");
            // Ensure cart always has base speed
            float cartSpeed = Vector3.Dot(cartBody.gameObject.transform.forward, cartBody.linearVelocity);
            if (cartSpeed < baseSpeed && input == 0.0f)
            {
                float normalizedCartSpeed = Mathf.Clamp01(Mathf.Abs(cartSpeed) / baseSpeed);
                float availableTorque = engineTorqueCurve.Evaluate(normalizedCartSpeed);
                cartBody.AddForceAtPosition(accelDirection * availableTorque * maxEngineTorque, transform.position);
            }

            // Acceleration Logic: pressing W to go faster
            if (input > 0.0f)
            {
                float normalizedCartSpeed = Mathf.Clamp01(Mathf.Abs(cartSpeed) / maxSpeed);
                float availableTorque = engineTorqueCurve.Evaluate(normalizedCartSpeed) * input;

                // Only apply force if current speed is below maxSpeed
                if (Mathf.Abs(cartSpeed) < maxSpeed)
                {
                    cartBody.AddForceAtPosition(accelDirection * availableTorque * maxEngineTorque, transform.position);
                }
            }
            // Deceleration Logic: pressing S to slow down but not below base speed
            else if (input < 0.0f && cartSpeed > minSpeed)
            {
                float desiredBrakeVelChange = -cartSpeed * brakeFactor;
                float desiredBrakeAcceleration = desiredBrakeVelChange / Time.fixedDeltaTime;
                float brakeForceMagnitude = Mathf.Min(Mathf.Abs(desiredBrakeAcceleration * wheelMass), maxBrakeForce);

                Vector3 finalBrakeForce = -accelDirection * brakeForceMagnitude;
                cartBody.AddForceAtPosition(finalBrakeForce, transform.position);
            }

            #endregion
        }
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