using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player/chain-level coordinator for cargo capacity and loot allocation.
///
/// Authoritative cargo data remains on each ChainCartCargo.
/// This controller:
/// - maintains the ordered set of CURRENTLY OWNED follower carts;
/// - aggregates TotalCapacity / TotalLoad / TotalScore / GlobalOverload;
/// - chooses which physical cart receives newly collected GroceryLoot;
/// - follows the front-to-tail safe-slot rule for NEW loot;
/// - distributes overload in configurable rounds;
/// - when owned capacity increases, transfers existing LOCAL overload into
///   newly available safe capacity, checking overloaded donors tail-to-front.
///
/// It does NOT:
/// - continuously rebalance safe cargo;
/// - apply Hype burn yet;
/// - spawn world pickups yet;
/// - perform checkout yet.
/// </summary>
[DisallowMultipleComponent]
public class CargoCapacityController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private SnakeCartManager snakeCartManager;
    [SerializeField] private CargoSystemSettings settings;

    #endregion

    #region Prototype Test Data

    [Header("Prototype Test Data")]
    [Tooltip("Temporary GroceryLootDefinition assets used by Context Menu allocation tests.")]
    [SerializeField] private GroceryLootDefinition[] debugTestLootDefinitions;

    #endregion

    #region Runtime Debug

    [Header("Owned Cargo Carts - Read Only")]
    [SerializeField] private int ownedFollowerCount;
    [SerializeField] private int registeredCargoCartCount;
    [SerializeField] private int missingCargoComponentCount;

    [Header("Global Cargo State - Read Only")]
    [SerializeField] private int totalCapacity;
    [SerializeField] private int totalLoad;
    [SerializeField] private int totalScoreValue;
    [SerializeField] private int overloadAmount;
    [SerializeField] private bool isOverloaded;
    [SerializeField] private float capacityNormalized;
    [SerializeField] private float movementSpeedMultiplier = 1f;

    [Header("Allocation Debug - Read Only")]
    [SerializeField] private string lastAssignedCartName;
    [SerializeField] private int lastAssignedOwnedIndex = -1;
    [SerializeField] private bool lastAssignmentWasOverflow;
    [SerializeField] private int lastSelectedOverflowRound;

    [Header("Overload Transfer Debug - Read Only")]
    [SerializeField] private int lastNormalizationTransferCount;
    [SerializeField] private bool lastNormalizationWasCapacityIncrease;

    #endregion

    #region Runtime Collections

    private readonly List<GameObject> ownedFollowerObjects = new List<GameObject>(16);
    private readonly List<ChainCartCargo> orderedCargoCarts = new List<ChainCartCargo>(16);

    private bool isNormalizingCargoTransfers;

    #endregion

    #region Public State

    public int OwnedFollowerCount => ownedFollowerCount;
    public int RegisteredCargoCartCount => registeredCargoCartCount;
    public int MissingCargoComponentCount => missingCargoComponentCount;

    public int TotalCapacity => totalCapacity;
    public int TotalLoad => totalLoad;
    public int TotalScoreValue => totalScoreValue;
    public int OverloadAmount => overloadAmount;
    public bool IsOverloaded => isOverloaded;
    public float CapacityNormalized => capacityNormalized;

    /// <summary>
    /// Cached movement multiplier derived from GLOBAL overload.
    /// It refreshes whenever owned carts or cargo state changes.
    /// </summary>
    public float MovementSpeedMultiplier => movementSpeedMultiplier;

    public IReadOnlyList<ChainCartCargo> OrderedCargoCarts => orderedCargoCarts;

    #endregion

    #region Events

    public event Action<int> OnCartCountChanged;
    public event Action<int> OnCapacityChanged;
    public event Action<int> OnLoadChanged;
    public event Action<int> OnScoreValueChanged;
    public event Action<int> OnOverloadChanged;
    public event Action<float> OnMovementSpeedMultiplierChanged;

    public event Action<CargoEntry, ChainCartCargo> OnCargoAdded;
    public event Action<CargoEntry, ChainCartCargo> OnCargoRemoved;

    /// <summary>
    /// Fired for automatic overload normalization transfers.
    /// This is NOT a new loot pickup and does not change score/load totals.
    /// </summary>
    public event Action<CargoEntry, ChainCartCargo, ChainCartCargo> OnCargoTransferred;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (snakeCartManager == null) snakeCartManager = GetComponent<SnakeCartManager>();
        if (settings == null) Debug.LogError("[CargoCapacityController] CargoSystemSettings is not assigned.", this);
        if (snakeCartManager == null) Debug.LogError("[CargoCapacityController] SnakeCartManager is missing.", this);
    }

    private void OnEnable()
    {
        if (snakeCartManager != null)
        {
            snakeCartManager.OnOwnedFollowersChanged -= HandleOwnedFollowersChanged;
            snakeCartManager.OnOwnedFollowersChanged += HandleOwnedFollowersChanged;
        }
    }

    private void Start()
    {
        RefreshOwnedCargoCarts();
    }

    private void OnDisable()
    {
        if (snakeCartManager != null) snakeCartManager.OnOwnedFollowersChanged -= HandleOwnedFollowersChanged;
        UnsubscribeFromCargoCarts();
    }

    #endregion

    #region Owned Cart Registration

    private void HandleOwnedFollowersChanged()
    {
        RefreshOwnedCargoCarts();
    }

    public void RefreshOwnedCargoCarts()
    {
        int previousOwnedFollowerCount = ownedFollowerCount;
        int previousCapacity = totalCapacity;

        UnsubscribeFromCargoCarts();

        ownedFollowerObjects.Clear();
        orderedCargoCarts.Clear();

        missingCargoComponentCount = 0;

        if (snakeCartManager != null)
        {
            snakeCartManager.FillOwnedFollowerCarts(ownedFollowerObjects);
        }

        ownedFollowerCount = ownedFollowerObjects.Count;

        for (int i = 0; i < ownedFollowerObjects.Count; i++)
        {
            GameObject cartObject = ownedFollowerObjects[i];
            if (cartObject == null) continue;

            ChainCartCargo cargo = cartObject.GetComponentInChildren<ChainCartCargo>(true);

            if (cargo == null)
            {
                missingCargoComponentCount++;

                Debug.LogError(
                    $"[CargoCapacityController] Owned follower '{cartObject.name}' is missing ChainCartCargo.",
                    cartObject
                );

                continue;
            }

            if (settings != null && cargo.Settings != settings)
            {
                Debug.LogWarning(
                    $"[CargoCapacityController] '{cartObject.name}' uses a different CargoSystemSettings asset than the chain controller.",
                    cargo
                );
            }

            orderedCargoCarts.Add(cargo);
            SubscribeToCargoCart(cargo);
        }

        if (ownedFollowerCount != previousOwnedFollowerCount) OnCartCountChanged?.Invoke(ownedFollowerCount);

        int rebuiltCapacity = CalculateRegisteredCapacity();
        bool capacityIncreased = rebuiltCapacity > previousCapacity;

        lastNormalizationWasCapacityIncrease = capacityIncreased;
        lastNormalizationTransferCount = capacityIncreased
            ? NormalizeLocalOverloadIntoSafeCapacity()
            : 0;

        RecalculateGlobalState();
    }

    private int CalculateRegisteredCapacity()
    {
        int capacity = 0;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo != null) capacity += Mathf.Max(0, cargo.SafeCapacity);
        }

        return capacity;
    }

    private void SubscribeToCargoCart(ChainCartCargo cargo)
    {
        if (cargo == null) return;

        cargo.OnCargoAdded -= HandleCartCargoAdded;
        cargo.OnCargoRemoved -= HandleCartCargoRemoved;
        cargo.OnCargoChanged -= HandleCartCargoChanged;

        cargo.OnCargoAdded += HandleCartCargoAdded;
        cargo.OnCargoRemoved += HandleCartCargoRemoved;
        cargo.OnCargoChanged += HandleCartCargoChanged;
    }

    private void UnsubscribeFromCargoCarts()
    {
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo == null) continue;

            cargo.OnCargoAdded -= HandleCartCargoAdded;
            cargo.OnCargoRemoved -= HandleCartCargoRemoved;
            cargo.OnCargoChanged -= HandleCartCargoChanged;
        }
    }

    #endregion

    #region Global State

    public void RecalculateGlobalState()
    {
        int previousCapacity = totalCapacity;
        int previousLoad = totalLoad;
        int previousScore = totalScoreValue;
        int previousOverload = overloadAmount;
        float previousMovementSpeedMultiplier = movementSpeedMultiplier;

        registeredCargoCartCount = 0;
        totalCapacity = 0;
        totalLoad = 0;
        totalScoreValue = 0;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo == null) continue;

            registeredCargoCartCount++;
            totalCapacity += Mathf.Max(0, cargo.SafeCapacity);
            totalLoad += Mathf.Max(0, cargo.LocalLoad);
            totalScoreValue += Mathf.Max(0, cargo.LocalScoreValue);
        }

        overloadAmount = Mathf.Max(0, totalLoad - totalCapacity);
        isOverloaded = overloadAmount > 0;

        capacityNormalized = totalCapacity > 0
            ? Mathf.Clamp01(totalLoad / (float)totalCapacity)
            : 0f;

        movementSpeedMultiplier = settings != null ? settings.GetOverloadSpeedMultiplier(overloadAmount) : 1f;

        if (totalCapacity != previousCapacity) OnCapacityChanged?.Invoke(totalCapacity);
        if (totalLoad != previousLoad) OnLoadChanged?.Invoke(totalLoad);
        if (totalScoreValue != previousScore) OnScoreValueChanged?.Invoke(totalScoreValue);
        if (overloadAmount != previousOverload) OnOverloadChanged?.Invoke(overloadAmount);
        if (!Mathf.Approximately(movementSpeedMultiplier, previousMovementSpeedMultiplier)) OnMovementSpeedMultiplierChanged?.Invoke(movementSpeedMultiplier);
    }

    private void HandleCartCargoAdded(ChainCartCargo cart, CargoEntry entry)
    {
        if (isNormalizingCargoTransfers) return;
        OnCargoAdded?.Invoke(entry, cart);
    }

    private void HandleCartCargoRemoved(ChainCartCargo cart, CargoEntry entry)
    {
        if (isNormalizingCargoTransfers) return;
        OnCargoRemoved?.Invoke(entry, cart);
    }

    private void HandleCartCargoChanged(ChainCartCargo cart)
    {
        if (isNormalizingCargoTransfers) return;
        RecalculateGlobalState();
    }

    #endregion

    #region Capacity-Increase Overload Normalization

    /// <summary>
    /// Moves only LOCAL overload cargo into currently unused SAFE capacity.
    ///
    /// Donor priority is TAIL -> FRONT so limited new capacity clears later
    /// carts first and leaves any unavoidable overload concentrated toward C1.
    ///
    /// Receiver priority is FRONT -> TAIL so transferred overload fills the
    /// earliest available safe capacity in normal chain order.
    ///
    /// Safe cargo is never generally rebalanced.
    /// </summary>
    public int NormalizeLocalOverloadIntoSafeCapacity()
    {
        if (orderedCargoCarts.Count <= 1) return 0;

        int transferCount = 0;
        isNormalizingCargoTransfers = true;

        try
        {
            for (int donorIndex = orderedCargoCarts.Count - 1; donorIndex >= 0; donorIndex--)
            {
                ChainCartCargo donor = orderedCargoCarts[donorIndex];
                if (donor == null) continue;

                while (donor.IsOverloaded)
                {
                    if (!TryFindTransferDestinationAndEntry(
                            donorIndex,
                            donor,
                            out ChainCartCargo destination,
                            out CargoEntry entry))
                    {
                        break;
                    }

                    if (!donor.RemoveCargo(entry)) break;

                    if (!destination.TryAddCargoEntry(entry))
                    {
                        // Defensive rollback. Under normal setup this should not
                        // fail because destination safe capacity and technical
                        // capacity were checked before removing from the donor.
                        donor.TryAddCargoEntry(entry);

                        Debug.LogError(
                            $"[CargoCapacityController] Failed to transfer cargo from '{donor.name}' to '{destination.name}'. Entry was restored to the donor.",
                            this
                        );

                        break;
                    }

                    transferCount++;
                    OnCargoTransferred?.Invoke(entry, donor, destination);
                }
            }
        }
        finally
        {
            isNormalizingCargoTransfers = false;
        }

        return transferCount;
    }

    private bool TryFindTransferDestinationAndEntry(
        int donorIndex,
        ChainCartCargo donor,
        out ChainCartCargo destination,
        out CargoEntry entry)
    {
        destination = null;
        entry = null;

        if (donor == null || !donor.IsOverloaded) return false;

        // Front -> tail receiver search. Donor priority and destination
        // priority are intentionally different:
        // - choose overloaded donors tail -> front;
        // - place transferred cargo into safe capacity front -> tail.
        for (int destinationIndex = 0; destinationIndex < orderedCargoCarts.Count; destinationIndex++)
        {
            if (destinationIndex == donorIndex) continue;

            ChainCartCargo candidateDestination = orderedCargoCarts[destinationIndex];
            if (!CanTechnicallyAcceptCargo(candidateDestination)) continue;

            int remainingSafeCapacity = candidateDestination.GetRemainingSafeCapacity();
            if (remainingSafeCapacity <= 0) continue;

            if (!donor.TryGetTransferableOverloadEntry(remainingSafeCapacity, out CargoEntry candidateEntry)) continue;

            destination = candidateDestination;
            entry = candidateEntry;
            return true;
        }

        return false;
    }

    #endregion

    #region Loot Allocation

    /// <summary>
    /// Adds one collected GroceryLootDefinition to the player's owned carts.
    ///
    /// Allocation rule:
    /// 1) Scan front -> tail for gameplay-safe capacity.
    /// 2) Only when EVERY cart is safe-full, distribute overflow in rounds.
    /// 3) A cart receives OverflowRoundSize overflow ENTRIES before allocation
    ///    moves to the next cart.
    /// 4) After every cart completes that overflow round, begin the next round
    ///    again from the front.
    ///
    /// Existing SAFE cargo is never generally rebalanced. Existing LOCAL
    /// overload may be transferred only when owned capacity increases.
    /// </summary>
    public bool TryAddLoot(GroceryLootDefinition lootDefinition)
    {
        return TryAddLoot(lootDefinition, out _);
    }

    public bool TryAddLoot(GroceryLootDefinition lootDefinition, out ChainCartCargo assignedCart)
    {
        assignedCart = null;

        if (lootDefinition == null) return false;
        if (orderedCargoCarts.Count == 0) return false;

        ResetAllocationDebug();

        // PASS 1: Always consume safe capacity first, front -> tail.
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (!CanTechnicallyAcceptCargo(cargo)) continue;
            if (!cargo.HasSafeCapacityFor(lootDefinition)) continue;

            if (!cargo.TryAddLoot(lootDefinition)) continue;

            assignedCart = cargo;
            RecordAllocationDebug(cargo, i, false, -1);
            return true;
        }

        // PASS 2: Every usable cart is safe-full. Distribute visual overflow in
        // front-to-tail rounds. This rule is currently entry-based because the
        // prototype milestone uses 1 Loot = 1 CargoEntry = 1 Load.
        int overflowRoundSize = settings != null ? Mathf.Max(1, settings.OverflowRoundSize) : 1;
        int lowestOverflowRound = int.MaxValue;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (!CanTechnicallyAcceptCargo(cargo)) continue;

            int cartRound = cargo.VisualOverflowEntryCount / overflowRoundSize;
            if (cartRound < lowestOverflowRound) lowestOverflowRound = cartRound;
        }

        if (lowestOverflowRound == int.MaxValue) return false;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (!CanTechnicallyAcceptCargo(cargo)) continue;

            int cartRound = cargo.VisualOverflowEntryCount / overflowRoundSize;
            if (cartRound != lowestOverflowRound) continue;

            if (!cargo.TryAddLoot(lootDefinition)) continue;

            assignedCart = cargo;
            RecordAllocationDebug(cargo, i, true, lowestOverflowRound);
            return true;
        }

        return false;
    }

    public bool CanAcceptAnyLoot()
    {
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            if (CanTechnicallyAcceptCargo(orderedCargoCarts[i])) return true;
        }

        return false;
    }

    private bool CanTechnicallyAcceptCargo(ChainCartCargo cargo)
    {
        if (cargo == null || cargo.Settings == null) return false;
        return cargo.CargoEntryCount < cargo.Settings.TechnicalMaxCargoPerCart;
    }

    private void RecordAllocationDebug(ChainCartCargo cargo, int ownedIndex, bool overflow, int overflowRound)
    {
        lastAssignedCartName = cargo != null ? cargo.gameObject.name : string.Empty;
        lastAssignedOwnedIndex = ownedIndex;
        lastAssignmentWasOverflow = overflow;
        lastSelectedOverflowRound = overflowRound;
    }

    private void ResetAllocationDebug()
    {
        lastAssignedCartName = string.Empty;
        lastAssignedOwnedIndex = -1;
        lastAssignmentWasOverflow = false;
        lastSelectedOverflowRound = -1;
    }

    #endregion

    #region Prototype Context Menu Tests

    [ContextMenu("TEST - Add First Loot")]
    private void DebugAddFirstLoot()
    {
        if (!RequirePlayModeForTest()) return;

        GroceryLootDefinition loot = GetFirstValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[CargoCapacityController] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        if (!TryAddLoot(loot))
        {
            Debug.LogWarning("[CargoCapacityController] TEST Add Loot failed. Check owned carts / ChainCartCargo setup.", this);
        }
    }

    [ContextMenu("TEST - Add Random Loot")]
    private void DebugAddRandomLoot()
    {
        if (!RequirePlayModeForTest()) return;

        GroceryLootDefinition loot = GetRandomValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[CargoCapacityController] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        if (!TryAddLoot(loot))
        {
            Debug.LogWarning("[CargoCapacityController] TEST Add Loot failed. Check owned carts / ChainCartCargo setup.", this);
        }
    }

    [ContextMenu("TEST - Add 10 Random Loot")]
    private void DebugAddTenRandomLoot()
    {
        if (!RequirePlayModeForTest()) return;

        for (int i = 0; i < 10; i++)
        {
            GroceryLootDefinition loot = GetRandomValidDebugLoot();

            if (loot == null || !TryAddLoot(loot))
            {
                Debug.LogWarning($"[CargoCapacityController] TEST stopped after {i} successful additions.", this);
                return;
            }
        }
    }

    [ContextMenu("TEST - Recalculate / Refresh Owned Carts")]
    private void DebugRefreshOwnedCarts()
    {
        if (!RequirePlayModeForTest()) return;
        RefreshOwnedCargoCarts();
    }

    [ContextMenu("TEST - Normalize Local Overload")]
    private void DebugNormalizeLocalOverload()
    {
        if (!RequirePlayModeForTest()) return;

        lastNormalizationWasCapacityIncrease = false;
        lastNormalizationTransferCount = NormalizeLocalOverloadIntoSafeCapacity();
        RecalculateGlobalState();

        Debug.Log(
            $"[CargoCapacityController] TEST normalization moved {lastNormalizationTransferCount} cargo entries.",
            this
        );
    }

    [ContextMenu("TEST - Log Cargo Distribution")]
    private void DebugLogDistribution()
    {
        if (!RequirePlayModeForTest()) return;

        string message =
            $"[CargoCapacityController] Distribution | Owned:{ownedFollowerCount} | Registered:{registeredCargoCartCount} | " +
            $"Load:{totalLoad}/{totalCapacity} | Overload:{overloadAmount}\n";

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];

            if (cargo == null)
            {
                message += $"[{i}] NULL\n";
                continue;
            }

            message +=
                $"[{i}] {cargo.gameObject.name} | Entries:{cargo.CargoEntryCount} | " +
                $"Load:{cargo.LocalLoad}/{cargo.SafeCapacity} | LocalOverload:{cargo.LocalOverload} | " +
                $"VisualOverflow:{cargo.VisualOverflowEntryCount}\n";
        }

        Debug.Log(message, this);
    }

    private GroceryLootDefinition GetFirstValidDebugLoot()
    {
        if (debugTestLootDefinitions == null) return null;

        for (int i = 0; i < debugTestLootDefinitions.Length; i++)
        {
            if (debugTestLootDefinitions[i] != null) return debugTestLootDefinitions[i];
        }

        return null;
    }

    private GroceryLootDefinition GetRandomValidDebugLoot()
    {
        if (debugTestLootDefinitions == null || debugTestLootDefinitions.Length == 0) return null;

        int startIndex = UnityEngine.Random.Range(0, debugTestLootDefinitions.Length);

        for (int offset = 0; offset < debugTestLootDefinitions.Length; offset++)
        {
            int index = (startIndex + offset) % debugTestLootDefinitions.Length;
            if (debugTestLootDefinitions[index] != null) return debugTestLootDefinitions[index];
        }

        return null;
    }

    private bool RequirePlayModeForTest()
    {
        if (Application.isPlaying) return true;

        Debug.LogWarning("[CargoCapacityController] Prototype Context Menu tests are Play Mode only.", this);
        return false;
    }

    #endregion
}
