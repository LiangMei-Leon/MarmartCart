using UnityEngine;

public class ChainedCartBehaviour : MonoBehaviour
{
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
    private Vector3 wheelVelocity;
    private Vector3 finalSuspensionForce;
    void Start()
    {

    }

    // Update is called once per frame
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

        }
    }
}