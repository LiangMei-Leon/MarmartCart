using System;
using Unity.Properties;
using UnityEngine;

public class ChainedCartManager : MonoBehaviour
{
    [Header("Cart Info")]
    [field: SerializeField]
    public bool isBonusCart { get; private set; } = false;
    [field: SerializeField]
    public CartRarity CartType { get; private set; } = CartRarity.Common;

    [SerializeField] private ParticleSystem collectVFX;
    private SnakeCartManager snakeCartManager;

    [field: SerializeField]
    public bool isCollectedByPlayer { get; private set; } = false;

    [field: SerializeField]
    public bool isCollectedByAI { get; private set; } = false;
    public bool isAvailable => !isCollectedByPlayer && !isCollectedByAI;

    private Rigidbody rb;
    [SerializeField] private GameObject p1Snake;
    private LeadingCartRaycaster p1Raycaster;
    private bool p1AllowCollect = true;
    [SerializeField] private GameObject p2Snake;
    private LeadingCartRaycaster p2Raycaster;
    private bool p2AllowCollect = true;

    [Header("Self-destory timer")]
    [Tooltip("A timer that only ticks when it is not being collected by player, if the time is up, remove this cart from the scene")]
    [SerializeField] private float disappearTime = 30f;
    [SerializeField] private float countTimer = 0f;

    [Header("Visual Settings")]
    [SerializeField] private Renderer cartRenderer; // Reference to mesh renderer that uses the material

    [SerializeField] private Color commonColor = Color.white;
    [SerializeField] private Color rareColor = Color.blue;
    [SerializeField] private Color epicColor = Color.magenta;
    [SerializeField] private Color legendaryColor = Color.yellow;

    [Header("Related Events")]
    [SerializeField] GameEvent p1CollectEmptyCartEvent;
    [SerializeField] GameEvent p1CollectNormalGroceryItemCartEvent;
    [SerializeField] GameEvent p1CollectExpensiveGroceryItemCartEvent;
    [SerializeField] GameEvent p2CollectEmptyCartEvent;
    [SerializeField] GameEvent p2CollectNormalGroceryItemCartEvent;
    [SerializeField] GameEvent p2CollectExpensiveGroceryItemCartEvent;

    [Header("Grocery Item Setting")]
    [SerializeField] private bool hasGroceryItem = false;
    [SerializeField] private bool hasNormalGroceryItem = false;
    [SerializeField] private bool hasExpensiveGroceryItem = false;
    [SerializeField] private GameObject normalGroceryItemVisual;
    [SerializeField] private GameObject expensiveGroceryItemVisual;

    void Awake()
    {
        collectVFX = this.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>();
        if (collectVFX == null)
        {
            Debug.Log("Fail to find the particle system");
        }
        ApplyRarityColor();
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
        p1Snake = GameObject.FindWithTag("SnakeCartManagerP1");
        p1Raycaster = p1Snake.transform.GetChild(0).GetComponent<LeadingCartRaycaster>();
        p1AllowCollect = !p1Raycaster.getIfInGhostMode();
        p2Snake = GameObject.FindWithTag("SnakeCartManagerP2");
        p2Raycaster = p2Snake.transform.GetChild(0).GetComponent<LeadingCartRaycaster>();
        p2AllowCollect = !p2Raycaster.getIfInGhostMode();
    }

    // Update is called once per frame
    void Update()
    {
        p1AllowCollect = !p1Raycaster.getIfInGhostMode();
        p2AllowCollect = !p2Raycaster.getIfInGhostMode();

        if(hasGroceryItem)
        {
            if(hasNormalGroceryItem)
            {
                normalGroceryItemVisual.SetActive(true);
            }
            else
            {
                normalGroceryItemVisual.SetActive(false);
            }
            if (hasExpensiveGroceryItem)
            {
                expensiveGroceryItemVisual.SetActive(true);
            }
            else
            {
                expensiveGroceryItemVisual.SetActive(false);
            }
        }

        if (isAvailable)
        {
            countTimer += Time.deltaTime;
        }
        else
        {
            countTimer = 0;
        }
        if (countTimer - (disappearTime - 3f) <= 0.1f && countTimer - (disappearTime - 3f) > 0f)
        {
            this.gameObject.GetComponent<CartMaterialManager>()?.SetCooldown(3f);
            Debug.Log("enter ghost mode");
        }else if (countTimer >= disappearTime)
        {
            Destroy(this.gameObject);
        }
        
    }

    public void OnDetach()
    {
        if (rb == null) return;
        this.gameObject.tag = "Item";
        Vector3 forceDirection = UnityEngine.Random.insideUnitSphere;

        isCollectedByPlayer = false;

        // Normalize the input direction to ensure it's a unit vector
        forceDirection.y = 0; // Ensure it's constrained to the XZ plane
        forceDirection.Normalize();

        // Scale the randomized direction by a random force magnitude
        float forceMagnitude = UnityEngine.Random.Range(10f, 30f); // Adjust range as needed
        Vector3 randomForce = forceDirection * forceMagnitude;

        // Apply the force to the Rigidbody
        rb.AddForce(randomForce, ForceMode.Impulse);

        // Optionally, add some torque for rotational randomness
        Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(20f, 30f); // Adjust range as needed
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    public void OnDetach(Vector3 hitDirection)
    {
        if (rb == null) return;
        this.gameObject.tag = "Item";
        Vector3 forceDirection = hitDirection;

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
        float forceMagnitude = UnityEngine.Random.Range(30f, 50f); // Adjust range as needed
        Vector3 randomForce = randomizedDirection * forceMagnitude;

        // Apply the force to the Rigidbody
        rb.AddForce(randomForce, ForceMode.Impulse);

        // Optionally, add some torque for rotational randomness
        Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(20f, 30f); // Adjust range as needed
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player1") && !isCollectedByPlayer && p1AllowCollect)
        {
            if(hasGroceryItem && hasNormalGroceryItem)
            {
                p1CollectNormalGroceryItemCartEvent.Raise();
            }
            else if(hasGroceryItem && hasExpensiveGroceryItem)
            {
                p1CollectExpensiveGroceryItemCartEvent.Raise();
            }
            else
            {
                p1CollectEmptyCartEvent.Raise();
            }

            Destroy(this.gameObject);
        }
        if (other.CompareTag("Player2") && !isCollectedByPlayer && p2AllowCollect)
        {
            if (hasGroceryItem && hasNormalGroceryItem)
            {
                p2CollectNormalGroceryItemCartEvent.Raise();
            }
            else if (hasGroceryItem && hasExpensiveGroceryItem)
            {
                p2CollectExpensiveGroceryItemCartEvent.Raise();
            }
            else
            {
                p2CollectEmptyCartEvent.Raise();
            }

            Destroy(this.gameObject);
        }
    }
    private void ApplyRarityColor()
    {
        if (cartRenderer == null) return;

        Material[] materials = cartRenderer.materials;
        if (materials.Length < 2 || materials[1] == null) return;

        Color targetColor = commonColor;

        switch (CartType)
        {
            case CartRarity.Common:
                targetColor = commonColor;
                break;
            case CartRarity.Rare:
                targetColor = rareColor;
                break;
            case CartRarity.Epic:
                targetColor = epicColor;
                break;
            case CartRarity.Legendary:
                targetColor = legendaryColor;
                break;
        }

        materials[1].color = targetColor;
        cartRenderer.materials = materials; // Apply the modified array back
    }
    public void PlayVFX()
    {
        //Debug.Log("Attempt to play vfx on: " + gameObject.name);
       // Debug.Log($"ParticleSystem state: IsPlaying = {collectVFX.isPlaying}, IsEmitting = {collectVFX.isEmitting}");
        collectVFX.Stop();
        collectVFX.Play();
        //Debug.Log($"After Play: IsPlaying = {collectVFX.isPlaying}, IsEmitting = {collectVFX.isEmitting}");
    }
    public void SetRarity(CartRarity rarity)
    {
        CartType = rarity;
        // Apply the rarity color to the cart renderer
        ApplyRarityColor();
    }
    public void CollectByPlayer()
    {
        isCollectedByPlayer = true;
    }

    public void CollectByAI()
    {
        isCollectedByAI = true;
    }

    public void ResetDisappearCountDown()
    {
        countTimer = 0f;
    }

    public void EnableNormalGroveryItem()
    {
        hasGroceryItem = true;
        hasNormalGroceryItem = true;
        if (normalGroceryItemVisual != null)
        {
            normalGroceryItemVisual.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Grocery item visual is not assigned.");
        }
    }
    public void EnableExpensiveGroveryItem()
    {
        hasGroceryItem = true;
        hasExpensiveGroceryItem = true;
        if (expensiveGroceryItemVisual != null)
        {
            expensiveGroceryItemVisual.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Grocery item visual is not assigned.");
        }
    }
    public bool HasGroceryItem()
    {
        return hasGroceryItem;
    }
}