using UnityEngine;

/// <summary>
/// Runtime state and collection behavior for a loose / chained cart.
///
/// Collection still uses the existing ScriptableObject GameEvent arrays:
/// - Empty cart collection event
/// - Normal grocery cart collection event
/// - Expensive grocery cart collection event
///
/// A loose cart resolves the player that touched it from that player's
/// SnakeCartManager hierarchy, checks the player's current battle ghost state,
/// raises the correct GameEvent, then destroys the loose world cart.
///
/// This component no longer depends on LeadingCartRaycaster.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class ChainedCartManager : MonoBehaviour, ISpawnerHoldable
{
    private const int MaxSupportedPlayers = 4;

    #region Cart Info

    [Header("Cart Info")]
    [field: SerializeField]
    public bool isBonusCart { get; private set; } = false;

    [field: SerializeField]
    public CartRarity CartType { get; private set; } = CartRarity.Common;

    [field: SerializeField]
    public bool isCollectedByPlayer { get; private set; } = false;

    [field: SerializeField]
    public bool isCollectedByAI { get; private set; } = false;

    public bool isAvailable =>
        !isCollectedByPlayer &&
        !isCollectedByAI &&
        !collectionCommitted;

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private ParticleSystem collectVFX;
    [SerializeField] private Renderer cartRenderer;
    [SerializeField] private CartMaterialManager cartMaterialManager;

    private Rigidbody rb;

    #endregion

    #region Self-Destruct

    [Header("Self-Destruct")]
    [Tooltip("Loose carts disappear after remaining uncollected for this duration.")]
    [Min(0f)]
    [SerializeField] private float disappearTime = 15f;

    [Tooltip("How long before disappearing the cart begins its ghost/blink warning.")]
    [Min(0f)]
    [SerializeField] private float disappearWarningDuration = 3f;

    [Header("Runtime - Read Only")]
    [SerializeField] private float countTimer;

    private bool disappearWarningStarted;
    private bool heldBySpawner;

    #endregion

    #region Visual Settings

    [Header("Team Color")]
    [SerializeField] private Color defaultColor = Color.white;

    [SerializeField]
    private Color[] playerTeamColors = new Color[MaxSupportedPlayers]
    {
        Color.blue,
        Color.red,
        Color.green,
        Color.yellow
    };

    [Tooltip("Material slot whose color represents the owning player's team.")]
    [Min(0)]
    [SerializeField] private int teamColorMaterialIndex = 1;

    #endregion

    #region Collection Events

    [Header("Collection Events")]
    [Tooltip("P1, P2, P3, P4")]
    [SerializeField] private GameEvent[] collectEmptyCartEvent = new GameEvent[MaxSupportedPlayers];

    [Tooltip("P1, P2, P3, P4")]
    [SerializeField] private GameEvent[] collectNormalGroceryItemCartEvent = new GameEvent[MaxSupportedPlayers];

    [Tooltip("P1, P2, P3, P4")]
    [SerializeField] private GameEvent[] collectExpensiveGroceryItemCartEvent = new GameEvent[MaxSupportedPlayers];

    #endregion

    #region Grocery Item State

    [Header("Grocery Item")]
    [SerializeField] private bool hasGroceryItem;
    [SerializeField] private bool hasNormalGroceryItem;
    [SerializeField] private bool hasExpensiveGroceryItem;

    [SerializeField] private GameObject normalGroceryItemVisual;
    [SerializeField] private GameObject expensiveGroceryItemVisual;

    #endregion

    #region Runtime Collection State

    // Destroy() is deferred until the end of the frame. With compound player
    // colliders, several OnTriggerEnter callbacks can happen before destruction.
    // This guard ensures one loose cart raises exactly one collection event.
    private bool collectionCommitted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (cartMaterialManager == null)
        {
            cartMaterialManager = GetComponentInChildren<CartMaterialManager>(true);
        }

        if (collectVFX == null)
        {
            Debug.LogWarning("[ChainedCartManager] Collect VFX is not assigned.", this);
        }

        if (cartRenderer == null)
        {
            Debug.LogWarning("[ChainedCartManager] Cart Renderer is not assigned.", this);
        }

        RefreshGroceryItemVisuals();
        SetCartTeamColor();
    }

    private void Update()
    {
        UpdateDisappearTimer();
    }

    #endregion

    #region Collection

    private void OnTriggerEnter(Collider other)
    {
        if (!isAvailable || other == null) return;

        if (!TryResolveCollectingPlayer(other, out int playerIndex, out LeadingCartBattleController battleController))
        {
            return;
        }

        // A player in battle ghost/cooldown cannot immediately recollect loose carts.
        if (battleController != null && battleController.IsInGhostMode)
        {
            return;
        }

        CommitCollection(playerIndex);
    }

    private bool TryResolveCollectingPlayer(
        Collider other,
        out int playerIndex,
        out LeadingCartBattleController battleController)
    {
        playerIndex = -1;
        battleController = null;

        // Both the leading cart and its collected followers live below the owning
        // ChainOfCarts / SnakeCartManager hierarchy, so this works regardless of
        // which physical collider actually touched the loose cart.
        SnakeCartManager collectingSnake = other.GetComponentInParent<SnakeCartManager>();

        if (collectingSnake == null)
        {
            return false;
        }

        playerIndex = collectingSnake.GetPlayerId() - 1;

        if (playerIndex < 0 || playerIndex >= MaxSupportedPlayers)
        {
            return false;
        }

        // Ghost mode belongs to the leading cart's battle controller.
        // Resolve it only when a loose-cart collection contact actually occurs,
        // rather than polling all players every frame.
        var snakeBody = collectingSnake.GetSnakeBody();

        if (snakeBody != null &&
            snakeBody.Count > 0 &&
            snakeBody[0] != null)
        {
            battleController =
                snakeBody[0].GetComponentInChildren<LeadingCartBattleController>(true);
        }

        return true;
    }

    private void CommitCollection(int playerIndex)
    {
        if (collectionCommitted) return;

        collectionCommitted = true;

        if (hasGroceryItem && hasNormalGroceryItem)
        {
            RaisePlayerEvent(collectNormalGroceryItemCartEvent, playerIndex);
        }
        else if (hasGroceryItem && hasExpensiveGroceryItem)
        {
            RaisePlayerEvent(collectExpensiveGroceryItemCartEvent, playerIndex);
        }
        else
        {
            RaisePlayerEvent(collectEmptyCartEvent, playerIndex);
        }

        Destroy(gameObject);
    }

    private void RaisePlayerEvent(GameEvent[] events, int playerIndex)
    {
        if (events == null ||
            playerIndex < 0 ||
            playerIndex >= events.Length)
        {
            Debug.LogError(
                $"[ChainedCartManager] Missing collection event slot for player index {playerIndex}.",
                this
            );

            return;
        }

        if (events[playerIndex] == null)
        {
            Debug.LogError(
                $"[ChainedCartManager] Collection GameEvent for Player {playerIndex + 1} is not assigned.",
                this
            );

            return;
        }

        events[playerIndex].Raise();
    }

    #endregion

    #region Collection State

    public void CollectByPlayer()
    {
        isCollectedByPlayer = true;
        isCollectedByAI = false;

        ResetDisappearCountDown();
        SetCartTeamColor();
    }

    public void CollectByAI()
    {
        isCollectedByAI = true;
        isCollectedByPlayer = false;

        ResetDisappearCountDown();
        SetCartTeamColor();
    }

    public void ResetDisappearCountDown()
    {
        countTimer = 0f;
        disappearWarningStarted = false;
    }

    #endregion

    #region Detach

    public void OnDetach()
    {
        Vector3 detachDirection = GetRandomPlanarDirection();

        Detach(
            detachDirection,
            Random.Range(10f, 30f),
            0f
        );
    }

    public void OnDetach(Vector3 hitDirection)
    {
        Vector3 planarDirection =
            Vector3.ProjectOnPlane(hitDirection, Vector3.up);

        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            planarDirection = GetRandomPlanarDirection();
        }
        else
        {
            planarDirection.Normalize();
        }

        Detach(
            planarDirection,
            Random.Range(30f, 50f),
            30f
        );
    }

    private void Detach(
        Vector3 baseDirection,
        float forceMagnitude,
        float randomDirectionAngle)
    {
        if (rb == null) return;

        gameObject.tag = "Item";

        isCollectedByPlayer = false;
        isCollectedByAI = false;
        collectionCommitted = false;

        ResetDisappearCountDown();
        SetCartTeamColor();

        Vector3 forceDirection = baseDirection;

        if (randomDirectionAngle > 0f)
        {
            float randomAngle =
                Random.Range(
                    -randomDirectionAngle,
                    randomDirectionAngle
                );

            forceDirection =
                Quaternion.Euler(0f, randomAngle, 0f) *
                forceDirection;
        }

        rb.AddForce(
            forceDirection.normalized * forceMagnitude,
            ForceMode.Impulse
        );

        Vector3 randomTorque =
            Random.insideUnitSphere *
            Random.Range(20f, 30f);

        rb.AddTorque(
            randomTorque,
            ForceMode.Impulse
        );
    }

    private Vector3 GetRandomPlanarDirection()
    {
        Vector3 direction =
            Vector3.ProjectOnPlane(
                Random.insideUnitSphere,
                Vector3.up
            );

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
    }

    #endregion

    #region Spawner Hold

    public void OnSpawnerHoldStart()
    {
        heldBySpawner = true;
        ResetDisappearCountDown();
    }

    public void OnSpawnerHoldEnd()
    {
        heldBySpawner = false;
        ResetDisappearCountDown();
    }

    #endregion

    #region Self-Destruct

    private void UpdateDisappearTimer()
    {
        if (heldBySpawner)
        {
            countTimer = 0f;
            return;
        }

        if (!isAvailable)
        {
            countTimer = 0f;
            disappearWarningStarted = false;
            return;
        }

        countTimer += Time.deltaTime;

        float warningStartTime =
            Mathf.Max(
                0f,
                disappearTime - disappearWarningDuration
            );

        if (!disappearWarningStarted &&
            disappearWarningDuration > 0f &&
            countTimer >= warningStartTime)
        {
            disappearWarningStarted = true;

            if (cartMaterialManager != null)
            {
                cartMaterialManager.SetGhostMode(
                    disappearWarningDuration
                );
            }
        }

        if (countTimer >= disappearTime)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Team Color

    public void SetCartTeamColor()
    {
        if (cartRenderer == null) return;

        Material[] materials = cartRenderer.materials;

        if (teamColorMaterialIndex < 0 ||
            teamColorMaterialIndex >= materials.Length ||
            materials[teamColorMaterialIndex] == null)
        {
            return;
        }

        Color targetColor = defaultColor;

        if (isCollectedByPlayer)
        {
            int playerIndex =
                TagToPlayerIndex(gameObject.tag);

            if (playerIndex >= 0 &&
                playerIndex < playerTeamColors.Length)
            {
                targetColor =
                    playerTeamColors[playerIndex];
            }
        }

        materials[teamColorMaterialIndex].color =
            targetColor;

        cartRenderer.materials =
            materials;
    }

    private int TagToPlayerIndex(string objectTag)
    {
        switch (objectTag)
        {
            case "Player1":
                return 0;

            case "Player2":
                return 1;

            case "Player3":
                return 2;

            case "Player4":
                return 3;

            default:
                return -1;
        }
    }

    #endregion

    #region Collect VFX

    public void PlayVFX()
    {
        if (collectVFX == null) return;

        collectVFX.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        collectVFX.Play();
    }

    #endregion

    #region Grocery Item State

    public void EnableNormalGroveryItem()
    {
        hasGroceryItem = true;
        hasNormalGroceryItem = true;
        hasExpensiveGroceryItem = false;

        RefreshGroceryItemVisuals();
    }

    public void EnableExpensiveGroveryItem()
    {
        hasGroceryItem = true;
        hasNormalGroceryItem = false;
        hasExpensiveGroceryItem = true;

        RefreshGroceryItemVisuals();
    }

    private void RefreshGroceryItemVisuals()
    {
        if (normalGroceryItemVisual != null)
        {
            normalGroceryItemVisual.SetActive(
                hasGroceryItem &&
                hasNormalGroceryItem
            );
        }

        if (expensiveGroceryItemVisual != null)
        {
            expensiveGroceryItemVisual.SetActive(
                hasGroceryItem &&
                hasExpensiveGroceryItem
            );
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

    #endregion

    #region Validation

    private void OnValidate()
    {
        disappearTime = Mathf.Max(0f, disappearTime);

        disappearWarningDuration = Mathf.Clamp(
            disappearWarningDuration,
            0f,
            disappearTime
        );

        teamColorMaterialIndex =
            Mathf.Max(0, teamColorMaterialIndex);
    }

    #endregion
}
