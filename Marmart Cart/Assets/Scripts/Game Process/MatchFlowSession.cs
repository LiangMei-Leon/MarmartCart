using System;
using System.Collections.Generic;
using UnityEngine;

public enum MatchFlowSessionType
{
    FreePlay,
    CartRestock,
    ZoneLoot,
    CheckoutWindow
}

public enum ArenaZoneId
{
    ZoneA,
    ZoneB,
    ZoneC,
    ZoneD
}

[Flags]
public enum CheckoutStationMask
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
    All = North | East | South | West
}

[Serializable]
public class MatchFlowSession
{
    [Tooltip("Designer-facing name only. Example: 'Opening Cart Grab' or 'Fresh Zone Wave 1'.")]
    public string label = "Session";

    public MatchFlowSessionType type = MatchFlowSessionType.FreePlay;

    [Tooltip(
        "FreePlay: free-play duration.\n" +
        "CartRestock: time across which the exact cart budget is released.\n" +
        "ZoneLoot: active loot-drop duration after telegraph.\n" +
        "CheckoutWindow: how long selected checkout stations remain open."
    )]
    [Min(0f)]
    public float duration = 5f;

    [Tooltip("Used only by ZoneLoot and CheckoutWindow. The resource/station remains inactive during this warning period.")]
    [Min(0f)]
    public float telegraphDuration = 2f;

    [Tooltip("CartRestock = exact carts released. ZoneLoot = exact loot pickups released. Ignored by other session types.")]
    [Min(0)]
    public int resourceBudget = 8;

    [Tooltip("How many resources release per pulse. Budget remains authoritative. Ignored by FreePlay/CheckoutWindow.")]
    [Min(1)]
    public int batchSize = 1;

    [Tooltip("Used only by ZoneLoot.")]
    public ArenaZoneId zone = ArenaZoneId.ZoneA;

    [Tooltip("Used only by CheckoutWindow.")]
    public CheckoutStationMask checkoutStations = CheckoutStationMask.North | CheckoutStationMask.South;

    public float GetPlannedDuration()
    {
        switch (type)
        {
            case MatchFlowSessionType.ZoneLoot:
            case MatchFlowSessionType.CheckoutWindow:
                return Mathf.Max(0f, telegraphDuration) + Mathf.Max(0f, duration);

            default:
                return Mathf.Max(0f, duration);
        }
    }
}

[CreateAssetMenu(menuName = "Marmart Carts/Match Flow Profile", fileName = "MatchFlowProfile")]
public class MatchFlowProfile : ScriptableObject
{
    [Tooltip("Runs this exact authored sequence in order.")]
    [SerializeField] private List<MatchFlowSession> sessions = new List<MatchFlowSession>();

    [Tooltip("If enabled, the director returns to Session 0 after the final session.")]
    [SerializeField] private bool loopSequence;

    public IReadOnlyList<MatchFlowSession> Sessions => sessions;
    public bool LoopSequence => loopSequence;

    public float GetPlannedDuration()
    {
        float total = 0f;

        for (int i = 0; i < sessions.Count; i++)
        {
            if (sessions[i] != null) total += sessions[i].GetPlannedDuration();
        }

        return total;
    }
}
