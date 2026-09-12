using System;
using UnityEngine;

[Flags]
public enum PlayerWorldHUDStateChange
{
    None = 0,
    Hype = 1 << 0,
    DriftReward = 1 << 1,
    Load = 1 << 2,
    Speed = 1 << 3,
    Streak = 1 << 4,
    All = Hype | DriftReward | Load | Speed | Streak
}

/// <summary>
/// Four-player semantic HUD state store.
///
/// PlayerWorldHUDSystem = who/where/camera binding.
/// PlayerWorldHUDStateSystem = what gameplay information belongs to each slot.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDStateSystem : MonoBehaviour
{
    public const int MaxPlayerSlots = 4;

    [Header("Runtime Player HUD State")]
    [SerializeField] private PlayerWorldHUDState[] playerStates = new PlayerWorldHUDState[MaxPlayerSlots];

    public event Action<int, PlayerWorldHUDStateChange> OnStateChanged;

    private void Reset()
    {
        EnsureFourStates();
        ResetAllStates();
    }

    private void Awake()
    {
        EnsureFourStates();
    }

    private void OnValidate()
    {
        EnsureFourStates();
    }

    public PlayerWorldHUDState GetState(int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return null;
        return playerStates[index];
    }

    public bool TryGetState(int playerIndex, out PlayerWorldHUDState state)
    {
        state = null;
        if (!TryGetStateIndex(playerIndex, out int index)) return false;
        state = playerStates[index];
        return state != null;
    }

    public void SetHype(int playerIndex, float currentHype, float maxHype, float burnMultiplier, float burnPerSecond, bool isSpeedingUp)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].SetHype(currentHype, maxHype, burnMultiplier, burnPerSecond, isSpeedingUp);
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.Hype);
    }

    public void SetDriftRewardPreview(int playerIndex, bool active, float potentialReward)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].SetDriftRewardPreview(active, potentialReward);
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.DriftReward);
    }

    public void ClearDriftRewardPreview(int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].ClearDriftRewardPreview();
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.DriftReward);
    }

    public void SetLoad(int playerIndex, float currentLoad, float safeCapacity, float overloadAmount)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].SetLoad(currentLoad, safeCapacity, overloadAmount);
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.Load);
    }

    public void SetMaxSpeedMultiplier(int playerIndex, float multiplier)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].SetMaxSpeedMultiplier(multiplier);
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.Speed);
    }

    public void SetCheckoutStreak(int playerIndex, bool eligible, int streakLevel, float progressNormalized)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].SetCheckoutStreak(eligible, streakLevel, progressNormalized);
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.Streak);
    }

    public void ResetPlayerState(int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index)) return;
        playerStates[index].ResetState();
        OnStateChanged?.Invoke(playerIndex, PlayerWorldHUDStateChange.All);
    }

    [ContextMenu("Reset All HUD States")]
    public void ResetAllStates()
    {
        EnsureFourStates();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            playerStates[i].ResetState();
            OnStateChanged?.Invoke(i + 1, PlayerWorldHUDStateChange.All);
        }
    }

    private bool TryGetStateIndex(int playerIndex, out int index)
    {
        index = playerIndex - 1;
        if (index >= 0 && index < MaxPlayerSlots) return true;

        Debug.LogError($"[PlayerWorldHUDStateSystem] Player index {playerIndex} is invalid. Expected 1..{MaxPlayerSlots}.", this);
        index = -1;
        return false;
    }

    private void EnsureFourStates()
    {
        if (playerStates == null || playerStates.Length != MaxPlayerSlots)
        {
            PlayerWorldHUDState[] oldStates = playerStates;
            playerStates = new PlayerWorldHUDState[MaxPlayerSlots];

            if (oldStates != null)
            {
                int copyCount = Mathf.Min(oldStates.Length, MaxPlayerSlots);
                for (int i = 0; i < copyCount; i++) playerStates[i] = oldStates[i];
            }
        }

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (playerStates[i] == null) playerStates[i] = new PlayerWorldHUDState();
        }
    }
}
