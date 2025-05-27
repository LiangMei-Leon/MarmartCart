using System;
using Unity.Properties;
using UnityEngine;

public class ChainedCartManager : MonoBehaviour
{
    [Header("Cart Info")]
    [field: SerializeField]
    public bool isBonusCart { get; private set; } = false;

    [SerializeField] private ParticleSystem collectVFX;
    private SnakeCartManager snakeCartManager;

    [field: SerializeField]
    public bool isCollectedByPlayer { get; private set; } = false;

    [field: SerializeField]
    public bool isCollectedByAI { get; private set; } = false;
    public bool isAvailable => !isCollectedByPlayer && !isCollectedByAI;

    private Rigidbody rb;
    private LeadingCartRaycaster hitInfo;

    [Header("Related Events")]
    [SerializeField] GameEvent p1collectNormalCartEvent;
    [SerializeField] GameEvent p1collectBonusCartEvent;
    [SerializeField] GameEvent p2collectNormalCartEvent;
    [SerializeField] GameEvent p2collectBonusCartEvent;

    void Awake()
    {
        collectVFX = this.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>();
        if (collectVFX == null)
        {
            Debug.Log("Fail to find the particle system");
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Cache the Rigidbody reference
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody not found on the GameObject. Please attach one.");
        }

        snakeCartManager = GameObject.FindWithTag("SnakeCartManager").GetComponent<SnakeCartManager>();
        if (snakeCartManager != null)
        {
            hitInfo = snakeCartManager.gameObject.transform.GetChild(0).GetComponent<LeadingCartRaycaster>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            PlayVFX();
        }
    }

    public void OnDetach()
    {
        if (rb == null) return;
        this.gameObject.tag = "Item";
        Vector3 forceDirection = hitInfo.hitDirection;

        isCollectedByPlayer = false;

        // Normalize the input direction to ensure it's a unit vector
        forceDirection.y = 0; // Ensure it's constrained to the XZ plane
        forceDirection.Normalize();

        // Generate a random angle within the 30-degree cone
        float randomAngle = UnityEngine.Random.Range(-30f, 30f);

        // Rotate the forceDirection by the random angle in the XZ plane
        Quaternion rotation = Quaternion.Euler(0, randomAngle, 0);
        Vector3 randomizedDirection = rotation * forceDirection;

        // Scale the randomized direction by a random force magnitude
        float forceMagnitude = UnityEngine.Random.Range(50f, 70f); // Adjust range as needed
        Vector3 randomForce = randomizedDirection * forceMagnitude;

        // Apply the force to the Rigidbody
        rb.AddForce(randomForce, ForceMode.Impulse);

        // Optionally, add some torque for rotational randomness
        Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(20f, 30f); // Adjust range as needed
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player1") && !isCollectedByPlayer)
        {
            // isCollectedByPlayer = true;
            if(isBonusCart)
            {
                p1collectBonusCartEvent.Raise();
            }
            else
            {
                p1collectNormalCartEvent.Raise();
            }
            Destroy(this.gameObject);
        }
        if (other.CompareTag("Player2") && !isCollectedByPlayer)
        {
            // isCollectedByPlayer = true;
            if (isBonusCart)
            {
                p2collectBonusCartEvent.Raise();
            }
            else
            {
                p2collectNormalCartEvent.Raise();
            }
            Destroy(this.gameObject);
        }
    }

    public void PlayVFX()
    {
        Debug.Log("Attempt to play vfx on: " + gameObject.name);
        Debug.Log($"ParticleSystem state: IsPlaying = {collectVFX.isPlaying}, IsEmitting = {collectVFX.isEmitting}");
        collectVFX.Stop();
        collectVFX.Play();
        Debug.Log($"After Play: IsPlaying = {collectVFX.isPlaying}, IsEmitting = {collectVFX.isEmitting}");
    }

    public void CollectByPlayer()
    {
        isCollectedByPlayer = true;
    }

    public void CollectByAI()
    {
        isCollectedByAI = true;
    }
}