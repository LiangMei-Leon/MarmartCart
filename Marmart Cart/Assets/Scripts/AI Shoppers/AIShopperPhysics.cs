using UnityEngine;

public class AIShopperPhysics : MonoBehaviour
{
    [Header("Knockout Settings")]
    [SerializeField] private float knockbackForce = 10f;  // Base force applied to the AI
    [SerializeField] private float upwardForce = 5f;      // Upward force to make the AI "fly"
    [SerializeField] private float spinTorque = 5f;       // Torque applied to spin the AI
    [SerializeField] private float destructionDelay = 2f; // Time before the AI is destroyed

    private Rigidbody rb;
    private bool isKnockedOut = false;

    private AIShopperBehaviour shopperBehaviour;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody is missing on this AI. Please attach a Rigidbody component.");
        }

        shopperBehaviour = GetComponent<AIShopperBehaviour>();
        if (shopperBehaviour == null)
        {
            Debug.LogError("AIShopperBehaviour is missing.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isKnockedOut) return; // Prevent multiple knockouts

        if (other.gameObject.CompareTag("Player"))
        {
            rb.isKinematic = false;
            KnockOut();
            shopperBehaviour.OnKnockOut();
        }
    }

    private void KnockOut()
    {
        if (rb == null) return;

        isKnockedOut = true;

        // Generate a random direction for knockback
        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            1f, // Ensure upward force
            Random.Range(-1f, 1f)
        ).normalized;

        // Apply knockback force
        Vector3 knockback = randomDirection * knockbackForce + Vector3.up * upwardForce;
        rb.AddForce(knockback, ForceMode.Impulse);

        // Apply random spin
        Vector3 randomTorque = new Vector3(
            Random.Range(-spinTorque, spinTorque),
            Random.Range(-spinTorque, spinTorque),
            Random.Range(-spinTorque, spinTorque)
        );
        rb.AddTorque(randomTorque, ForceMode.Impulse);

        // Disable AI functionality (e.g., NavMeshAgent)
        DisableAI();

        // Destroy the AI after a delay
        Destroy(gameObject, destructionDelay);
    }

    private void DisableAI()
    {
        // Disable NavMeshAgent or any other AI logic
        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // Optionally disable other components
        var AInavScript = GetComponent<AIShopperBehaviour>();
        if(AInavScript != null)
        {
            AInavScript.enabled = false;
        }
    }

    public bool IsKnockedOut()
    {
        return isKnockedOut;
    }
}
