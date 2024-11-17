using UnityEngine;

public class ChainedCartManager : MonoBehaviour
{
    [field: SerializeField]
    public bool isCollectedByPlayer { get; private set; }

    [field: SerializeField]
    public bool isCollectedByAI { get; private set; }

    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Cache the Rigidbody reference
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody not found on the GameObject. Please attach one.");
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnDetach()
    {
        if (rb == null) return;

        isCollectedByPlayer = false;

        // Generate a random force direction
        Vector3 randomDirection = Random.insideUnitSphere.normalized;

        // Scale the random direction by a force magnitude
        float forceMagnitude = Random.Range(50f, 70f); // Adjust range as needed
        Vector3 randomForce = randomDirection * forceMagnitude;

        // Apply the force to the Rigidbody
        rb.AddForce(randomForce, ForceMode.Impulse);

        // Optionally, add some torque for rotational randomness
        Vector3 randomTorque = Random.insideUnitSphere * Random.Range(20f, 30f); // Adjust range as needed
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }
}