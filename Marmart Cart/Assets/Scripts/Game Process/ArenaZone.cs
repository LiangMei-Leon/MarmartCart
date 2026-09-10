using UnityEngine;

public enum ArenaZoneState
{
    Idle,
    Warning,
    Active
}

/// <summary>
/// Lightweight gameplay wrapper for one of the four resource zones.
/// It deliberately knows nothing about match sequencing.
/// </summary>
[DisallowMultipleComponent]
public class ArenaZone : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private ArenaZoneId zoneId = ArenaZoneId.ZoneA;
    [SerializeField] private string displayName = "Zone A";

    [Header("Loot")]
    [SerializeField] private ZoneLootSpawner lootSpawner;

    [Header("Optional Prototype State Visuals")]
    [SerializeField] private GameObject warningIndicator;
    [SerializeField] private GameObject activeIndicator;

    [Header("Runtime - Read Only")]
    [SerializeField] private ArenaZoneState state = ArenaZoneState.Idle;

    public ArenaZoneId ZoneId => zoneId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? zoneId.ToString() : displayName;
    public ZoneLootSpawner LootSpawner => lootSpawner;
    public ArenaZoneState State => state;

    private void Awake()
    {
        if (lootSpawner == null) lootSpawner = GetComponentInChildren<ZoneLootSpawner>(true);
        SetIdle();
    }

    public void SetIdle()
    {
        state = ArenaZoneState.Idle;

        if (warningIndicator != null) warningIndicator.SetActive(false);
        if (activeIndicator != null) activeIndicator.SetActive(false);
    }

    public void SetWarning()
    {
        state = ArenaZoneState.Warning;

        if (warningIndicator != null) warningIndicator.SetActive(true);
        if (activeIndicator != null) activeIndicator.SetActive(false);
    }

    public void SetActive()
    {
        state = ArenaZoneState.Active;

        if (warningIndicator != null) warningIndicator.SetActive(false);
        if (activeIndicator != null) activeIndicator.SetActive(true);
    }
}
