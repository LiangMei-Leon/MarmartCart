using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Authoritative macro match-flow sequencer.
///
/// The director decides WHAT happens and WHEN.
/// Specialized arena components decide HOW carts, loot, zones, and checkout
/// stations perform their own work.
///
/// No weighted randomness is used in this first balance prototype.
/// </summary>
[DisallowMultipleComponent]
public class MatchFlowDirector : MonoBehaviour
{
    #region Configuration

    [Header("Flow")]
    [SerializeField] private MatchFlowProfile profile;
    [SerializeField] private bool autoStart = true;

    [Header("Arena Bindings")]
    [SerializeField] private CartRestockSpawner cartRestockSpawner;
    [SerializeField] private ArenaZone[] zones;
    [SerializeField] private CheckoutStationFlowController[] checkoutStations;

    [Header("Debug")]
    [SerializeField] private bool logSessions = true;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isRunning;
    [SerializeField] private int currentSessionIndex = -1;
    [SerializeField] private MatchFlowSessionType currentSessionType;
    [SerializeField] private string currentSessionLabel;
    [SerializeField] private float plannedProfileDuration;

    private Coroutine flowRoutine;

    public bool IsRunning => isRunning;
    public int CurrentSessionIndex => currentSessionIndex;
    public MatchFlowSessionType CurrentSessionType => currentSessionType;
    public string CurrentSessionLabel => currentSessionLabel;
    public float PlannedProfileDuration => plannedProfileDuration;

    public event Action<int, MatchFlowSession> OnSessionStarted;
    public event Action<int, MatchFlowSession> OnSessionEnded;
    public event Action OnFlowCompleted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (zones == null || zones.Length == 0) zones = FindObjectsByType<ArenaZone>(FindObjectsSortMode.None);
        if (checkoutStations == null || checkoutStations.Length == 0) checkoutStations = FindObjectsByType<CheckoutStationFlowController>(FindObjectsSortMode.None);
        if (cartRestockSpawner == null) cartRestockSpawner = FindFirstObjectByType<CartRestockSpawner>();

        ResetArenaMacroState();
        plannedProfileDuration = profile != null ? profile.GetPlannedDuration() : 0f;
    }

    private void Start()
    {
        if (autoStart) StartFlow();
    }

    private void OnDisable()
    {
        StopFlow();
    }

    #endregion

    #region Flow Control

    [ContextMenu("START Match Flow")]
    public void StartFlow()
    {
        if (isRunning) return;

        if (profile == null)
        {
            Debug.LogError("[MatchFlowDirector] Match Flow Profile is missing.", this);
            return;
        }

        if (profile.Sessions == null || profile.Sessions.Count == 0)
        {
            Debug.LogError("[MatchFlowDirector] Match Flow Profile contains no sessions.", this);
            return;
        }

        ResetArenaMacroState();
        plannedProfileDuration = profile.GetPlannedDuration();

        flowRoutine = StartCoroutine(RunFlow());
    }

    [ContextMenu("STOP Match Flow")]
    public void StopFlow()
    {
        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        isRunning = false;
        currentSessionIndex = -1;
        currentSessionLabel = string.Empty;

        cartRestockSpawner?.CancelSpawning();

        if (zones != null)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].LootSpawner != null) zones[i].LootSpawner.CancelSpawning();
            }
        }

        ResetArenaMacroState();
    }

    [ContextMenu("RESTART Match Flow")]
    public void RestartFlow()
    {
        StopFlow();
        StartFlow();
    }

    private IEnumerator RunFlow()
    {
        isRunning = true;

        do
        {
            for (int i = 0; i < profile.Sessions.Count; i++)
            {
                MatchFlowSession session = profile.Sessions[i];
                if (session == null) continue;

                currentSessionIndex = i;
                currentSessionType = session.type;
                currentSessionLabel = string.IsNullOrWhiteSpace(session.label) ? session.type.ToString() : session.label;

                if (logSessions)
                {
                    Debug.Log($"[MatchFlow] START {i}: {currentSessionLabel} ({session.type})", this);
                }

                OnSessionStarted?.Invoke(i, session);

                yield return RunSession(session);

                OnSessionEnded?.Invoke(i, session);

                if (logSessions)
                {
                    Debug.Log($"[MatchFlow] END {i}: {currentSessionLabel}", this);
                }
            }
        }
        while (profile.LoopSequence);

        isRunning = false;
        flowRoutine = null;
        currentSessionIndex = -1;
        currentSessionLabel = string.Empty;

        ResetArenaMacroState();
        OnFlowCompleted?.Invoke();
    }

    #endregion

    #region Session Execution

    private IEnumerator RunSession(MatchFlowSession session)
    {
        switch (session.type)
        {
            case MatchFlowSessionType.FreePlay:
                yield return WaitSeconds(session.duration);
                break;

            case MatchFlowSessionType.CartRestock:
                yield return RunCartRestock(session);
                break;

            case MatchFlowSessionType.ZoneLoot:
                yield return RunZoneLoot(session);
                break;

            case MatchFlowSessionType.CheckoutWindow:
                yield return RunCheckoutWindow(session);
                break;
        }
    }

    private IEnumerator RunCartRestock(MatchFlowSession session)
    {
        if (cartRestockSpawner == null)
        {
            Debug.LogError("[MatchFlowDirector] Cart Restock session has no CartRestockSpawner.", this);
            yield return WaitSeconds(session.duration);
            yield break;
        }

        yield return cartRestockSpawner.SpawnExactBudget(session.resourceBudget, session.duration, session.batchSize);
    }

    private IEnumerator RunZoneLoot(MatchFlowSession session)
    {
        ArenaZone zone = FindZone(session.zone);

        if (zone == null)
        {
            Debug.LogError($"[MatchFlowDirector] Could not find ArenaZone {session.zone}.", this);
            yield return WaitSeconds(session.telegraphDuration + session.duration);
            yield break;
        }

        zone.SetWarning();
        yield return WaitSeconds(session.telegraphDuration);

        zone.SetActive();

        if (zone.LootSpawner != null)
        {
            yield return zone.LootSpawner.SpawnExactBudget(session.resourceBudget, session.duration, session.batchSize);
        }
        else
        {
            Debug.LogError($"[MatchFlowDirector] ArenaZone {session.zone} has no ZoneLootSpawner.", zone);
            yield return WaitSeconds(session.duration);
        }

        zone.SetIdle();
    }

    private IEnumerator RunCheckoutWindow(MatchFlowSession session)
    {
        CloseAllCheckoutStations();
        SetCheckoutTelegraph(session.checkoutStations, true);

        yield return WaitSeconds(session.telegraphDuration);

        SetCheckoutTelegraph(session.checkoutStations, false);
        SetCheckoutOpen(session.checkoutStations, true);

        yield return WaitSeconds(session.duration);

        SetCheckoutOpen(session.checkoutStations, false);
    }

    private IEnumerator WaitSeconds(float duration)
    {
        if (duration <= 0f) yield break;
        yield return new WaitForSeconds(duration);
    }

    #endregion

    #region Arena Lookup / State

    private ArenaZone FindZone(ArenaZoneId zoneId)
    {
        if (zones == null) return null;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].ZoneId == zoneId) return zones[i];
        }

        return null;
    }

    private void ResetArenaMacroState()
    {
        if (zones != null)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null) zones[i].SetIdle();
            }
        }

        CloseAllCheckoutStations();
    }

    private void CloseAllCheckoutStations()
    {
        if (checkoutStations == null) return;

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            if (checkoutStations[i] != null) checkoutStations[i].SetOpen(false);
        }
    }

    private void SetCheckoutTelegraph(CheckoutStationMask mask, bool telegraphing)
    {
        if (checkoutStations == null) return;

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            CheckoutStationFlowController station = checkoutStations[i];
            if (station == null || !MaskContainsStation(mask, station.StationId)) continue;

            station.SetTelegraphing(telegraphing);
        }
    }

    private void SetCheckoutOpen(CheckoutStationMask mask, bool open)
    {
        if (checkoutStations == null) return;

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            CheckoutStationFlowController station = checkoutStations[i];
            if (station == null || !MaskContainsStation(mask, station.StationId)) continue;

            station.SetOpen(open);
        }
    }

    private bool MaskContainsStation(CheckoutStationMask mask, CheckoutStationId stationId)
    {
        CheckoutStationMask stationMask;

        switch (stationId)
        {
            case CheckoutStationId.North:
                stationMask = CheckoutStationMask.North;
                break;

            case CheckoutStationId.East:
                stationMask = CheckoutStationMask.East;
                break;

            case CheckoutStationId.South:
                stationMask = CheckoutStationMask.South;
                break;

            case CheckoutStationId.West:
                stationMask = CheckoutStationMask.West;
                break;

            default:
                return false;
        }

        return (mask & stationMask) != 0;
    }

    #endregion
}
