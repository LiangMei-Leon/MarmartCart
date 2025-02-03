using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [SerializeField] GameObjectPool targetPool;
    [SerializeField] GameTimeManager gameManager;
    [SerializeField] SfxManager sfxManager;
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

        gameManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameTimeManager>();
        if (gameManager == null)
        {
            Debug.LogError("gameManager is missing.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isKnockedOut) return; // Prevent multiple knockouts

        if (other.gameObject.CompareTag("Player"))
        {
            // Play one of the two sound effects randomly
            if (Random.value < 0.5f) // Random.value gives a float between 0 and 1
            {
                sfxManager.PlaySFX("HitCharacter1");
            }
            else
            {
                sfxManager.PlaySFX("HitCharacter2");
            }
            rb.isKinematic = false;
            KnockOut();
            shopperBehaviour.OnKnockOut();
        }
    }

    private void KnockOut()
    {
        if (rb == null) return;

        isKnockedOut = true;
        gameManager.IncreaseHitCount();
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

        // Return the AI to the pool after a delay
        StartCoroutine(ReturnToPool());
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

    private IEnumerator ReturnToPool()
    {

        yield return new WaitForSeconds(destructionDelay);

        // Reset Rigidbody state
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        // Reset AI state
        var aiBehaviour = GetComponent<AIShopperBehaviour>();
        if (aiBehaviour != null)
        {
            aiBehaviour.ResetState();
        }
        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = true;
        }
        var AInavScript = GetComponent<AIShopperBehaviour>();
        if (AInavScript != null)
        {
            AInavScript.enabled = true;
        }
        // Return to pool
        isKnockedOut = false;
        targetPool.ReturnObject(gameObject);
    }

    public bool IsKnockedOut()
    {
        return isKnockedOut;
    }
}
