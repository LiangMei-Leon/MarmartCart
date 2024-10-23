using UnityEditor.Timeline.Actions;
using UnityEngine;

public class WheelSuspensionScript : MonoBehaviour
{
    private Ray ray;
    [SerializeField] private float xAxisOffset;
    [SerializeField] private float yAxisOffset;
    [SerializeField] private float zAxisOffset;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Rigidbody cartBody;
    private Vector3 rayStartPosition;
    [SerializeField] float springRestLength = 1f;
    [SerializeField] float springRaycastExtraLength = 0.1f;
    [SerializeField] float springStrength = 100f;
    [SerializeField] float springDamping = 10f;
    private Vector3 springDirection;
    private Vector3 appliedForce;

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
        rayStartPosition = transform.TransformPoint(new Vector3(xAxisOffset, yAxisOffset, zAxisOffset));
    }
    void FixedUpdate()
    {
        RaycastHit hit;
        if(Physics.Raycast(transform.position, -1 * transform.up, out hit, springRestLength + springRaycastExtraLength, layerMask))
        {
            // Initial Debug on if the raycast is correctly hitting something
            Debug.Log(gameObject.name + "hit" + hit.collider.gameObject.name);

            // Calculate the upward (positive) direction of the spring
            springDirection = transform.up;

            Vector3 wheelVelocity = cartBody.GetPointVelocity(transform.position);

            float wheelVelOnSpringDir = Vector3.Dot(springDirection, wheelVelocity);

            float offset = springRestLength - hit.distance;

            float force = (offset * springStrength) - (wheelVelOnSpringDir * springDamping);

            appliedForce = springDirection * force;
            
            // Apply the force to the rigidbody at the wheel's position
            cartBody.AddForceAtPosition(appliedForce, transform.position);
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

        // Visualize the force direction and magnitude
        if (Application.isPlaying) // Only draw the force vector during play mode
        {
            Gizmos.color = Color.green;
            // Scale the force vector for visualization
            float forceVisualizationScale = 0.001f; // Adjust this value to make the force more or less visible
            Gizmos.DrawRay(transform.position, appliedForce * forceVisualizationScale);
        }
    }
}
