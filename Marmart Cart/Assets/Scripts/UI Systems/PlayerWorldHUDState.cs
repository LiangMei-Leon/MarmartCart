using System;
using UnityEngine;

/// <summary>
/// Semantic HUD state for ONE player.
///
/// Gameplay truth only:
/// no Shapes geometry, colors, animations, icons, or layout decisions.
/// </summary>
[Serializable]
public sealed class PlayerWorldHUDState
{
    #region Hype

    [Header("Hype - Runtime State")]
    [SerializeField] private float currentHype;
    [SerializeField] private float maxHype = 100f;

    [Tooltip("1 = baseline burn, 1.3 = 30% higher burn.")]
    [SerializeField] private float hypeBurnMultiplier = 1f;

    [SerializeField] private float hypeBurnPerSecond;
    [SerializeField] private bool isSpeedingUp;

    [Header("Drift Success Preview - Runtime State")]
    [SerializeField] private bool driftRewardPreviewActive;
    [SerializeField] private float potentialDriftHypeReward;

    [Header("Drift Failure Preview - Runtime State")]
    [SerializeField] private bool driftPenaltyPreviewActive;

    [Tooltip("Potential EXISTING Hype lost if the current drift fails.")]
    [SerializeField] private float potentialDriftHypePenalty;

    #endregion

    #region Load

    [Header("Load - Runtime State")]
    [SerializeField] private float currentLoad;
    [SerializeField] private float safeCapacity;
    [SerializeField] private float overloadAmount;

    #endregion

    #region Speed Consequence

    [Header("Speed Consequence - Runtime State")]
    [SerializeField] private float maxSpeedMultiplier = 1f;

    #endregion

    #region Checkout / Streak

    [Header("Checkout / Streak - Runtime State")]
    [SerializeField] private bool checkoutStreakEligible;
    [SerializeField] private int checkoutStreakLevel;

    [Range(0f, 1f)]
    [SerializeField] private float checkoutStreakProgressNormalized;

    #endregion

    #region Hype Read API

    public float CurrentHype => currentHype;
    public float MaxHype => maxHype;

    public float HypeNormalized =>
        maxHype > 0.0001f
            ? Mathf.Clamp01(currentHype / maxHype)
            : 0f;

    public float HypeBurnMultiplier => hypeBurnMultiplier;
    public float HypeBurnPerSecond => hypeBurnPerSecond;
    public bool IsSpeedingUp => isSpeedingUp;

    public float HypeBurnAboveBaseline =>
        Mathf.Max(0f, hypeBurnMultiplier - 1f);

    #endregion

    #region Drift Reward Read API

    public bool DriftRewardPreviewActive =>
        driftRewardPreviewActive &&
        potentialDriftHypeReward > 0.0001f;

    public float PotentialDriftHypeReward =>
        Mathf.Max(0f, potentialDriftHypeReward);

    public float PotentialDriftHypeRewardNormalized =>
        maxHype > 0.0001f
            ? Mathf.Clamp01(
                PotentialDriftHypeReward /
                maxHype
            )
            : 0f;

    public float ProjectedHypeAfterDriftReward =>
        maxHype > 0f
            ? Mathf.Clamp(
                currentHype +
                PotentialDriftHypeReward,
                0f,
                maxHype
            )
            : 0f;

    public float ProjectedHypeAfterDriftRewardNormalized =>
        maxHype > 0.0001f
            ? Mathf.Clamp01(
                ProjectedHypeAfterDriftReward /
                maxHype
            )
            : 0f;

    #endregion

    #region Drift Penalty Read API

    public bool DriftPenaltyPreviewActive =>
        driftPenaltyPreviewActive &&
        potentialDriftHypePenalty > 0.0001f &&
        currentHype > 0.0001f;

    public float PotentialDriftHypePenalty =>
        Mathf.Max(0f, potentialDriftHypePenalty);

    public float PotentialDriftHypePenaltyNormalized =>
        maxHype > 0.0001f
            ? Mathf.Clamp01(
                PotentialDriftHypePenalty /
                maxHype
            )
            : 0f;

    public float ProjectedHypeAfterDriftPenalty =>
        Mathf.Max(
            0f,
            currentHype -
            PotentialDriftHypePenalty
        );

    public float ProjectedHypeAfterDriftPenaltyNormalized =>
        maxHype > 0.0001f
            ? Mathf.Clamp01(
                ProjectedHypeAfterDriftPenalty /
                maxHype
            )
            : 0f;

    #endregion

    #region Load Read API

    public float CurrentLoad => currentLoad;
    public float SafeCapacity => safeCapacity;

    public float RemainingSafeCapacity =>
        Mathf.Max(
            0f,
            safeCapacity - currentLoad
        );

    public float OverloadAmount => overloadAmount;
    public bool IsOverloaded => overloadAmount > 0.0001f;

    public float SafeLoadNormalized =>
        safeCapacity > 0.0001f
            ? Mathf.Clamp01(
                currentLoad /
                safeCapacity
            )
            : 0f;

    public bool IsAtOrAboveSafeCapacity =>
        safeCapacity > 0.0001f &&
        currentLoad >= safeCapacity;

    #endregion

    #region Speed Read API

    public float MaxSpeedMultiplier => maxSpeedMultiplier;

    public bool HasMaxSpeedPenalty =>
        maxSpeedMultiplier < 0.9999f;

    public float MaxSpeedPenaltyNormalized =>
        Mathf.Clamp01(
            1f - maxSpeedMultiplier
        );

    #endregion

    #region Streak Read API

    public bool CheckoutStreakEligible => checkoutStreakEligible;
    public int CheckoutStreakLevel => checkoutStreakLevel;
    public float CheckoutStreakProgressNormalized => checkoutStreakProgressNormalized;

    #endregion

    #region Write API

    public void SetHype(
        float current,
        float maximum,
        float burnMultiplier,
        float burnPerSecond,
        bool speedingUp)
    {
        maxHype = Mathf.Max(0f, maximum);

        currentHype =
            maxHype > 0f
                ? Mathf.Clamp(
                    current,
                    0f,
                    maxHype
                )
                : 0f;

        hypeBurnMultiplier = Mathf.Max(0f, burnMultiplier);
        hypeBurnPerSecond = Mathf.Max(0f, burnPerSecond);
        isSpeedingUp = speedingUp;
    }

    public void SetDriftRewardPreview(
        bool active,
        float potentialReward)
    {
        driftRewardPreviewActive = active;

        potentialDriftHypeReward =
            Mathf.Max(
                0f,
                potentialReward
            );
    }

    public void SetDriftPenaltyPreview(
        bool active,
        float potentialPenalty)
    {
        driftPenaltyPreviewActive = active;

        potentialDriftHypePenalty =
            Mathf.Max(
                0f,
                potentialPenalty
            );
    }

    public void ClearDriftRewardPreview()
    {
        driftRewardPreviewActive = false;
        potentialDriftHypeReward = 0f;
    }

    public void ClearDriftPenaltyPreview()
    {
        driftPenaltyPreviewActive = false;
        potentialDriftHypePenalty = 0f;
    }

    public void ClearAllDriftPreviews()
    {
        ClearDriftRewardPreview();
        ClearDriftPenaltyPreview();
    }

    public void SetLoad(
        float load,
        float capacity,
        float overload)
    {
        currentLoad = Mathf.Max(0f, load);
        safeCapacity = Mathf.Max(0f, capacity);
        overloadAmount = Mathf.Max(0f, overload);
    }

    public void SetMaxSpeedMultiplier(float multiplier)
    {
        maxSpeedMultiplier =
            Mathf.Max(
                0f,
                multiplier
            );
    }

    public void SetCheckoutStreak(
        bool eligible,
        int level,
        float progressNormalized)
    {
        checkoutStreakEligible = eligible;
        checkoutStreakLevel = Mathf.Max(0, level);

        checkoutStreakProgressNormalized =
            Mathf.Clamp01(
                progressNormalized
            );
    }

    public void ResetState()
    {
        currentHype = 0f;
        maxHype = 100f;

        hypeBurnMultiplier = 1f;
        hypeBurnPerSecond = 0f;
        isSpeedingUp = false;

        driftRewardPreviewActive = false;
        potentialDriftHypeReward = 0f;

        driftPenaltyPreviewActive = false;
        potentialDriftHypePenalty = 0f;

        currentLoad = 0f;
        safeCapacity = 0f;
        overloadAmount = 0f;

        maxSpeedMultiplier = 1f;

        checkoutStreakEligible = false;
        checkoutStreakLevel = 0;
        checkoutStreakProgressNormalized = 0f;
    }

    #endregion
}
