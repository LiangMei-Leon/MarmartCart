using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeCartManager : MonoBehaviour, IAssistPlayerDataSource
{
    [Header("Distance Path System")]
    [SerializeField] private SnakePathHistory pathHistory;

    [SerializeField] float distanceBetween = 0.2f; // The spawn rate time difference that creates an illusion of distance in between snake bodies
    //[SerializeField] float cartSpacing = 1f; // world space units

    [SerializeField] List<GameObject> bodyParts = new List<GameObject>();
    [SerializeField] List<GameObject> snakeBody = new List<GameObject>();
    [SerializeField] List<GameObject> cartsWithOutItem = new List<GameObject>();

    LeadingCartRaycaster LeadingCartRaycaster;
    [Header("Player")]
    [UnityEngine.Range(1, 4)]
    [SerializeField] private int playerIndex = 1; // 1..4

    [Header("Related Events")]
    [SerializeField] GameEvent setupCamera;

    float countUp = 0;

    [Header("PlayerInputManager")]
    [SerializeField] private PlayerInputManager playerInputManager;

    [SerializeField] private CashScoreManager cashScoreManager;
    //[SerializeField] private ComboDealsManager comboDealsManager;

    public bool needScaleup = false;

    [SerializeField] private int numOfCartsWithGroceryItem = 0;
    [SerializeField] private SfxManager sfxManager;

    private void Awake()
    {
        if (pathHistory == null)
            pathHistory = GetComponent<SnakePathHistory>();

        if (pathHistory == null)
            pathHistory = gameObject.AddComponent<SnakePathHistory>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CreateBodyParts();
    }
    private void Update()
    {
        
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        ManageSnakeBody();
        SnakeMovement();
    }

    void SnakeMovement()
    {
        if (snakeBody.Count > 1)
        {
            for (int i = 1; i < snakeBody.Count; i++)
            {
                if (needScaleup) // this bool get written by PowerupManager class
                {
                    // Apply scale multiplier to current carts (the leading cart is handles in PowerupManager class
                    snakeBody[i].transform.transform.localScale = new Vector3(10f, 10f, 10f); 
                }
                else
                {
                    // Revert scale multiplier to normal
                    snakeBody[i].transform.transform.localScale = new Vector3(5f, 5f, 5f);
                }

                MarkerManager markM = snakeBody[i - 1].GetComponent<MarkerManager>();
                snakeBody[i].transform.position = markM.markerList[0].position;
                snakeBody[i].transform.rotation = markM.markerList[0].rotation;
                markM.markerList.RemoveAt(0);
            }
        }
    }

    void CreateBodyParts()
    {
        if (snakeBody.Count == 0)
        {
            GameObject tempCartInstance = Instantiate(bodyParts[0], transform.position, transform.rotation, transform);
            tempCartInstance.tag = GetPlayerTag(); // Set tag to the attached carts based on player index
            // Ensure MarkerManager is added
            if (!tempCartInstance.GetComponent<MarkerManager>())
            {
                tempCartInstance.AddComponent<MarkerManager>();
            }

            // Set as collected by the player
            var cartManager = tempCartInstance.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                cartManager.CollectByPlayer();
            }

            snakeBody.Add(tempCartInstance);
            LeadingCartRaycaster = tempCartInstance.GetComponent<LeadingCartRaycaster>();
            // ------------------------------------------------------
            // NEW DISTANCE PATH SYSTEM
            // ------------------------------------------------------

            LeadingCartBehaviour movementBehaviour =
                tempCartInstance.GetComponentInChildren<LeadingCartBehaviour>();

            if (movementBehaviour != null &&
                movementBehaviour.CartBody != null)
            {
                pathHistory.Initialize(
                    movementBehaviour.CartBody
                );
            }
            else
            {
                Debug.LogError(
                    "[SnakeCartManager] Could not find the " +
                    "authoritative leading-cart Rigidbody for " +
                    "SnakePathHistory.",
                    tempCartInstance
                );
            }

            // ------------------------------------------------------
            setupCamera.Raise();
            bodyParts.RemoveAt(0);

            //if(playerInputManager != null)
            //{
            //    if (isPlayer1)
            //        playerInputManager.SetupPlayers();
            //}
            return;
        }

        MarkerManager markM = snakeBody[snakeBody.Count - 1].GetComponent<MarkerManager>();
        if (countUp == 0)
        {
            markM.ClearMarkerList();
        }
        countUp += Time.deltaTime;
        if (countUp >= distanceBetween)
        {
            GameObject tempCartInstance = Instantiate(bodyParts[0], markM.markerList[0].position, markM.markerList[0].rotation, transform);
            tempCartInstance.tag = GetPlayerTag();
            // Ensure MarkerManager is added
            if (!tempCartInstance.GetComponent<MarkerManager>())
            {
                tempCartInstance.AddComponent<MarkerManager>();
            }

            // Set as collected by the player
            var cartManager = tempCartInstance.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                cartManager.CollectByPlayer();
                cartManager.SetCartTeamColor();
            }

            snakeBody.Add(tempCartInstance);
            if(!tempCartInstance.GetComponent<ChainedCartManager>().HasGroceryItem())
            {
                cartsWithOutItem.Add(tempCartInstance);
            }
            bodyParts.RemoveAt(0);
            tempCartInstance.GetComponent<MarkerManager>().ClearMarkerList();
            countUp = 0;
        }
    }

    void ManageSnakeBody()
    {
        if (bodyParts.Count > 0)
        {
            CreateBodyParts();
        }
        for (int i = 1; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null)
            {
                snakeBody.RemoveAt(i);
                i = i - 1;
                break;
            }

            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager == null)
            {
                Debug.LogError("No Chained Cart Manager Component on " + snakeBody[i].name);
                continue;
            }

            // If this cart is no longer collected by the player
            if (!cartManager.isCollectedByPlayer)
            {
                // Detach this cart and all subsequent carts
                for (int j = i; j < snakeBody.Count; j++)
                {
                    if(snakeBody[j].GetComponent<ChainedCartManager>().HasGroceryItem())
                    {
                        numOfCartsWithGroceryItem--;
                   
                    }
                    snakeBody[j].transform.localScale = new Vector3(5f, 5f, 5f);
                    snakeBody[j].transform.SetParent(null); // Detach from parent
                    snakeBody[j].GetComponent<ChainedCartManager>().OnDetach();
                    cartsWithOutItem.Remove(snakeBody[j]);
                }

                // Remove all subsequent carts from the list
                snakeBody.RemoveRange(i, snakeBody.Count - i);

                break; // Exit the loop as we've detached all necessary carts
            }
        }

        // If no carts are left, destroy this script
        if (snakeBody.Count == 0)
        {
            Destroy(this);
        }
    }

    public void AddBodyParts(GameObject addedObj)
    {
        bodyParts.Add(addedObj);
        StartCoroutine(DelayedPlayVFX());
    }

    private IEnumerator DelayedPlayVFX()
    {
        // Wait for 0.1 seconds
        yield return new WaitForSeconds(0.12f);

        // Ensure the snakeBody list has elements
        if (snakeBody.Count > 0)
        {
            // Reference the last object in the snakeBody list
            var lastCart = snakeBody[snakeBody.Count - 1];
            var cartManager = lastCart.GetComponent<ChainedCartManager>();

            if (cartManager != null)
            {
                Debug.Log("Playing VFX on: " + lastCart.name);
                cartManager.PlayVFX();
            }
            else
            {
                Debug.LogError("ChainedCartManager missing on: " + lastCart.name);
            }
        }
        else
        {
            Debug.LogError("SnakeBody list is empty. No VFX to play.");
        }
    }

    public void TemporarilyDisableDetaching()
    {
        LeadingCartRaycaster.TemporarilyDisableDetaching();
    }

    public int GetSnakeBodyLength()
    {
        return snakeBody.Count;
    }
    public List<GameObject> GetSnakeBody()
    {
        return snakeBody;
    }
    public void TriggerAllPowerupsDelayed()
    {
        StartCoroutine(DelayedTrigger());
    }
    private IEnumerator DelayedTrigger()
    {
        yield return new WaitForSeconds(0.2f); // Delay

        foreach (var cart in this.GetSnakeBody())
        {
            var cartManager = cart.GetComponent<ChainedCartManager>();
            if (cartManager != null && cartManager.isBonusCart)
            {
                GameObject powerupObject = cart.transform.GetChild(4).gameObject;
                //Debug.Log(powerupObject.name);
                var powerup = powerupObject.GetComponent<IPowerup>();
                if (powerup != null)
                {
                    //Debug.Log("Fire");
                    powerup.ActivatePowerup();
                }
            }
        }
    }
    public int CheckOutNextCartWithItem()
    {
        // No chained carts, nothing to do
        if (snakeBody.Count <= 1)
            return snakeBody.Count;

        int pIndex = playerIndex;

        // Start from 1 to skip the leading cart
        for (int i = 1; i < snakeBody.Count; i++)
        {
            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager == null || !cartManager.HasGroceryItem())
                continue;

            // Get rarity from this cart
            bool isExpensiveItem = cartManager.isCarryingExpensiveGroceryItem();

            // Update internal counters
            numOfCartsWithGroceryItem = Mathf.Max(0, numOfCartsWithGroceryItem - 1);

            // Submit to cash score manager and handle combo streak there
            if (cashScoreManager != null)
            {
                cashScoreManager.RegisterItemCheckout(pIndex, isExpensiveItem);
            }

            // Play checkout SFX
            if (sfxManager != null)
            {
                sfxManager.PlaySFX("CheckoutSingle"); // sfx for each item checkout
            }

            // Remove this cart from the snake and destroy it
            GameObject removed = snakeBody[i];
            snakeBody.RemoveAt(i);
            Destroy(removed);

            // Return remaining number of carts in snake
            return numOfCartsWithGroceryItem;
        }

        // If we reach here, there was no cart with a grocery item
        return numOfCartsWithGroceryItem;
    }
    public void CollectNormalGroceryItem()
    {
        if (cartsWithOutItem.Count >= 1)
        {
            numOfCartsWithGroceryItem++;
            // Give a random cart (excluding leading cart) a grocery item
            int cartIndex = Random.Range(0, cartsWithOutItem.Count);
            ChainedCartManager cartManager = cartsWithOutItem[cartIndex].GetComponent<ChainedCartManager>();
            cartManager.EnableNormalGroveryItem();
            cartsWithOutItem.RemoveAt(cartIndex); // Remove from list to avoid duplicate assignment
        }
        else
        {
            // All carts already have grocery items.
            // Debug.Log("All carts already have grocery items.");
        }
    }
    public void CollectExpensiveGroceryItem()
    {
        if (cartsWithOutItem.Count >= 1)
        {
            numOfCartsWithGroceryItem++;
            // Give a random cart (excluding leading cart) a grocery item
            int cartIndex = Random.Range(0, cartsWithOutItem.Count);
            ChainedCartManager cartManager = cartsWithOutItem[cartIndex].GetComponent<ChainedCartManager>();
            cartManager.EnableExpensiveGroveryItem();
            cartsWithOutItem.RemoveAt(cartIndex); // Remove from list to avoid duplicate assignment
        }
        else
        {
            // All carts already have grocery items.
            // Debug.Log("All carts already have grocery items.");
        }
    }
    public void IncreaseNumOfCartsWithItem()
    {
        numOfCartsWithGroceryItem++;
    }
    public int GetCurrentNumOfCartsWithItem()
    {
        return numOfCartsWithGroceryItem;
    }
    public bool HasEmptyCartForGroceryItem()
    {
        return cartsWithOutItem.Count > 0;
    }
    public void RemoveAllCartsWithItem()
    {
        sfxManager.PlaySFX("CheckoutCarts");
        for (int i = snakeBody.Count - 1; i >= 1; i--) // iterate backward, skip leading cart
        {
            var cartManager = snakeBody[i].GetComponent<ChainedCartManager>();
            if (cartManager != null && cartManager.HasGroceryItem())
            {
                numOfCartsWithGroceryItem--;
                GameObject removed = snakeBody[i];
                snakeBody.RemoveAt(i); // use RemoveAt for clarity
                Destroy(removed);
            }
        }
    }
    private string GetPlayerTag()
    {
        // Make sure these tags exist in Unity Tag Manager:
        // Player1, Player2, Player3, Player4
        return $"Player{playerIndex}";
    }

    /// IAssistPlayerDataSource implementation for MatchBalanceManager
    public int GetPlayerId()
    {
        return playerIndex;
    }

    public int GetCurrentScore()
    {
        return cashScoreManager.GetPlayerScore(playerIndex);
    }

    public int GetCurrentCartCount()
    {
        return snakeBody.Count;
    }
}