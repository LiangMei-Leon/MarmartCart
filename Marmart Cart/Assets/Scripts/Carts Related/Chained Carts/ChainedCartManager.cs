using System;
using Unity.Properties;
using UnityEngine;

public class ChainedCartManager : MonoBehaviour, ISpawnerHoldable
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

    private const int MaxSupportedPlayers = 4;
    private int maxPlayers = 4;
    [SerializeField] private LeadingCartRaycaster[] playerRaycasters = new LeadingCartRaycaster[MaxSupportedPlayers];
    private bool[] allowCollect;

    [Header("Self-destory timer")]
    [Tooltip("A timer that only ticks when it is not being collected by player, if the time is up, remove this cart from the scene")]
    [SerializeField] private float disappearTime = 30f;
    [SerializeField] private float countTimer = 0f;

    [Header("Visual Settings")]
    [SerializeField] private Renderer cartRenderer; // Reference to mesh renderer that uses the material

    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField]
    private Color[] playerTeamColors = new Color[MaxSupportedPlayers]
    {
        Color.blue, Color.red, Color.green, Color.yellow
    };

    [Header("Related Events")]
    [SerializeField] private GameEvent[] collectEmptyCartEvent = new GameEvent[MaxSupportedPlayers];
    [SerializeField] private GameEvent[] collectNormalGroceryItemCartEvent = new GameEvent[MaxSupportedPlayers];
    [SerializeField] private GameEvent[] collectExpensiveGroceryItemCartEvent = new GameEvent[MaxSupportedPlayers];

    [Header("Grocery Item Setting")]
    [SerializeField] private bool hasGroceryItem = false;
    [SerializeField] private bool hasNormalGroceryItem = false;
    [SerializeField] private bool hasExpensiveGroceryItem = false;
    [SerializeField] private GameObject normalGroceryItemVisual;
    [SerializeField] private GameObject expensiveGroceryItemVisual;
    private bool _heldBySpawner = false;

    private CartMaterialManager cartMaterialManagerScript;
    void Awake()
    {
        cartMaterialManagerScript = GetComponentInChildren<CartMaterialManager>();

        maxPlayers = Mathf.Clamp(GMode.Instance.PlayerCount(), 1, MaxSupportedPlayers);
        allowCollect = new bool[maxPlayers];

        collectVFX = this.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>();
        if (collectVFX == null)
        {
            Debug.Log("Fail to find the particle system");
        }
        SetCartTeamColor();
    }
    public void OnSpawnerHoldStart()
    {
        _heldBySpawner = true;
        ResetDisappearCountDown(); // you already have this method
    }

    public void OnSpawnerHoldEnd()
    {
        _heldBySpawner = false;
        ResetDisappearCountDown();
    }

    void Start()
    {
        // Cache the Rigidbody reference
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody not found on the GameObject. Please attach one.");
        }
        for (int i = 0; i < maxPlayers; i++)
        {
            playerRaycasters[i] = GameObject.FindWithTag("SnakeCartManagerP" + (i+1)).transform.GetChild(0).GetComponent<LeadingCartRaycaster>();
            if (playerRaycasters[i] != null)
                allowCollect[i] = !playerRaycasters[i].getIfInGhostMode();
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < maxPlayers; i++)
        {
            if (playerRaycasters[i] != null)
                allowCollect[i] = !playerRaycasters[i].getIfInGhostMode();
        }

        if (hasGroceryItem)
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

        if (_heldBySpawner)
        {
            countTimer = 0f;
        }
        else
        {
            if (isAvailable) countTimer += Time.deltaTime;
            else countTimer = 0f;

            if (countTimer - (disappearTime - 3f) <= 0.1f && countTimer - (disappearTime - 3f) > 0f)
            {
                cartMaterialManagerScript.SetCooldown(3f);
                //Debug.Log("enter ghost mode");
            }
            else if (countTimer >= disappearTime)
            {
                Destroy(this.gameObject);
            }
        }

    }

    public void OnDetach()
    {
        if (rb == null) return;
        this.gameObject.tag = "Item";
        Vector3 forceDirection = UnityEngine.Random.insideUnitSphere;

        isCollectedByPlayer = false;
        // Reset cart color to default
        SetCartTeamColor();
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
        // Reset cart color to default
        SetCartTeamColor();
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
        if (isCollectedByPlayer) return;

        int playerIdx = TagToPlayerIndex(other.tag); // 0..3, -1 if not a player
        if (playerIdx < 0) return;

        if (!allowCollect[playerIdx]) return;

        // Raise correct event
        if (hasGroceryItem && hasNormalGroceryItem)
            collectNormalGroceryItemCartEvent[playerIdx]?.Raise();
        else if (hasGroceryItem && hasExpensiveGroceryItem)
            collectExpensiveGroceryItemCartEvent[playerIdx]?.Raise();
        else
            collectEmptyCartEvent[playerIdx]?.Raise();

        Destroy(gameObject);
    }

    private int TagToPlayerIndex(string tag)
    {
        if(tag == "Player1") return 0;
        if(tag == "Player2") return 1;
        if(tag == "Player3") return 2;
        if(tag == "Player4") return 3;
        return -1;
    }
    public void SetCartTeamColor()
    {
        if (cartRenderer == null) return;
        var materials = cartRenderer.materials;
        if (materials.Length < 2 || materials[1] == null) return;

        Color targetColor = defaultColor;

        if (isCollectedByPlayer)
        {
            int idx = TagToPlayerIndex(gameObject.tag);
            if (idx >= 0 && idx < playerTeamColors.Length)
                targetColor = playerTeamColors[idx];
        }

        materials[1].color = targetColor;
        cartRenderer.materials = materials;
    }
    //private void ApplyRarityColor()
    //{
    //    if (cartRenderer == null) return;

    //    Material[] materials = cartRenderer.materials;
    //    if (materials.Length < 2 || materials[1] == null) return;

    //    Color targetColor = commonColor;

    //    switch (CartType)
    //    {
    //        case CartRarity.Common:
    //            targetColor = commonColor;
    //            break;
    //        case CartRarity.Rare:
    //            targetColor = rareColor;
    //            break;
    //        case CartRarity.Epic:
    //            targetColor = epicColor;
    //            break;
    //        case CartRarity.Legendary:
    //            targetColor = legendaryColor;
    //            break;
    //    }

    //    materials[1].color = targetColor;
    //    cartRenderer.materials = materials; // Apply the modified array back
    //}
    public void PlayVFX()
    {
        //Debug.Log("Attempt to play vfx on: " + gameObject.name);
       // Debug.Log($"ParticleSystem state: IsPlaying = {collectVFX.isPlaying}, IsEmitting = {collectVFX.isEmitting}");
        collectVFX.Stop();
        collectVFX.Play();
        //Debug.Log($"After Play: IsPlaying = {collectVFX.isPlaying}, IsEmitting = {collectVFX.isEmitting}");
    }
    //public void SetRarity(CartRarity rarity)
    //{
    //    CartType = rarity;
    //    // Apply the rarity color to the cart renderer
    //    ApplyRarityColor();
    //}
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
    public bool isCarryingNormalGroceryItem()
    {
        return hasNormalGroceryItem;
    }
    public bool isCarryingExpensiveGroceryItem()
    {
        return hasExpensiveGroceryItem;
    }
}