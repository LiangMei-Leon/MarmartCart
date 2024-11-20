using UnityEngine;

public class ChainedCartManager : MonoBehaviour
{
    [Header("Cart Info")]
    [SerializeField] bool isBonusCart = false;

    private ParticleSystem collectVFX;
    private SnakeCartManager snakeCartManager;

    public bool isCollectedByPlayer = false;

    public bool isCollectedByAI = false;

    private Rigidbody rb;
    private LeadingCartRaycaster hitInfo;

    [Header("Related Events")]
    [SerializeField] GameEvent collectNormalCartEvent;
    [SerializeField] GameEvent collectBonusCartEvent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Cache the Rigidbody reference
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody not found on the GameObject. Please attach one.");
        }


        collectVFX = this.transform.GetChild(0).GetComponent<ParticleSystem>();
        snakeCartManager = GameObject.FindWithTag("SnakeCartManager").GetComponent<SnakeCartManager>();
        if(snakeCartManager != null)
        {
            hitInfo = snakeCartManager.gameObject.transform.GetChild(0).GetComponent<LeadingCartRaycaster>();
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnDetach()
    {
        if (rb == null) return;

        Vector3 forceDirection = hitInfo.hitDirection;

        isCollectedByPlayer = false;

        // Normalize the input direction to ensure it's a unit vector
        forceDirection.y = 0; // Ensure it's constrained to the XZ plane
        forceDirection.Normalize();

        // Generate a random angle within the 30-degree cone
        float randomAngle = Random.Range(-30f, 30f);

        // Rotate the forceDirection by the random angle in the XZ plane
        Quaternion rotation = Quaternion.Euler(0, randomAngle, 0);
        Vector3 randomizedDirection = rotation * forceDirection;

        // Scale the randomized direction by a random force magnitude
        float forceMagnitude = Random.Range(50f, 70f); // Adjust range as needed
        Vector3 randomForce = randomizedDirection * forceMagnitude;

        // Apply the force to the Rigidbody
        rb.AddForce(randomForce, ForceMode.Impulse);

        // Optionally, add some torque for rotational randomness
        Vector3 randomTorque = Random.insideUnitSphere * Random.Range(20f, 30f); // Adjust range as needed
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isCollectedByPlayer)
        {
            isCollectedByPlayer = true;
            if(isBonusCart)
            {
                collectBonusCartEvent.Raise();
            }
            else
            {
                collectNormalCartEvent.Raise();
            }
            // Destroy(this.gameObject);
        }
    }

    public void OnCollectedByPlayer()
    {
        if(snakeCartManager != null)
        {
            snakeCartManager.AddBodyParts(this.gameObject);
        }
        collectVFX.Play();
    }
}