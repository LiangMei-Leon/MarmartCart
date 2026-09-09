using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Builds a pending Hype reward while the player maintains a valid drift.
/// Clean successful drift endings pay the pending Hype into CartControlScript.
/// Interrupted/failed drifts lose the pending reward.
/// </summary>
public class CartDriftHypeReward : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private CartControlScript cartController;

    #endregion

    #region Reward Rules

    [Header("Reward Toggle")]
    [FormerlySerializedAs("enableDriftFuelReward")]
    [SerializeField] private bool enableDriftHypeReward = true;

    [Header("Success Rules")]
    [SerializeField] private float minimumSuccessDuration = 0.6f;

    [Header("Base Reward")]
    [FormerlySerializedAs("baseFuelGainPerSecond")]
    [Tooltip("Base Hype gained per second before tightness and duration multipliers.")]
    [SerializeField] private float baseHypeGainPerSecond = 5f;

    [FormerlySerializedAs("maxFuelRewardPerDrift")]
    [Tooltip("Maximum Hype that can be earned from one drift.")]
    [SerializeField] private float maxHypeRewardPerDrift = 25f;

    [Header("Tightness Reward")]
    [Tooltip("No pending Hype is gained below this drift tightness.")]
    [SerializeField, Range(0f, 1f)] private float minimumRewardTightness = 0.2f;

    [Tooltip("Multiplier when tightness is at Minimum Reward Tightness.")]
    [SerializeField] private float minTightnessRewardMultiplier = 0.5f;

    [Tooltip("Multiplier when tightness is 1.")]
    [SerializeField] private float maxTightnessRewardMultiplier = 1.3f;

    [Header("Duration Reward Ramp")]
    [Tooltip("How many seconds it takes for duration reward rate to reach maximum.")]
    [SerializeField] private float durationToReachMaxRewardRate = 3f;

    [Tooltip("Reward multiplier at the beginning of the drift.")]
    [SerializeField] private float startingDurationRewardMultiplier = 0.35f;

    [Tooltip("Reward multiplier after Duration To Reach Max Reward Rate.")]
    [SerializeField] private float maxDurationRewardMultiplier = 2f;

    [Tooltip("Higher values make long drifts ramp reward faster near the end.")]
    [SerializeField]
    private AnimationCurve durationRewardCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.4f, 0.25f),
        new Keyframe(0.75f, 0.7f),
        new Keyframe(1f, 1f)
    );

    [Header("Clean End Rules")]
    [Tooltip("If true, pressing Speedup while drifting still pays out pending drift Hype.")]
    [SerializeField] private bool payoutWhenCancelledBySpeedup = true;

    #endregion

    #region Runtime

    [Header("Debug")]
    [SerializeField] private bool debugReward = true;

    private bool isTrackingDrift;
    private float currentDriftDuration;
    private float pendingHypeReward;

    private float currentDurationRewardMultiplier = 1f;
    private float currentTightnessRewardMultiplier = 1f;

    public bool IsTrackingDrift => isTrackingDrift;
    public float CurrentDriftDuration => currentDriftDuration;
    public float PendingHypeReward => pendingHypeReward;
    public float PreviewHypeAmount => GetPreviewHypeAmount01();
    public bool HasPendingReward => pendingHypeReward > 0.01f;

    public float CurrentDurationRewardMultiplier => currentDurationRewardMultiplier;
    public float CurrentTightnessRewardMultiplier => currentTightnessRewardMultiplier;
    public float CurrentTotalRewardMultiplier => currentDurationRewardMultiplier * currentTightnessRewardMultiplier;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (driftController == null) driftController = GetComponentInParent<CartDriftController>();
        if (cartController == null) cartController = GetComponentInParent<CartControlScript>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearStateOnly();
    }

    private void Update()
    {
        if (!enableDriftHypeReward || !isTrackingDrift) return;
        if (driftController == null || cartController == null) return;
        if (!driftController.IsDrifting) return;

        UpdatePendingHype();
    }

    #endregion

    #region Drift Events

    private void Subscribe()
    {
        if (driftController == null) return;

        driftController.OnDriftStarted += HandleDriftStarted;
        driftController.OnDriftEndedClean += HandleDriftEndedClean;
        driftController.OnDriftInterrupted += HandleDriftInterrupted;
    }

    private void Unsubscribe()
    {
        if (driftController == null) return;

        driftController.OnDriftStarted -= HandleDriftStarted;
        driftController.OnDriftEndedClean -= HandleDriftEndedClean;
        driftController.OnDriftInterrupted -= HandleDriftInterrupted;
    }

    private void HandleDriftStarted()
    {
        if (!enableDriftHypeReward) return;

        isTrackingDrift = true;
        currentDriftDuration = 0f;
        pendingHypeReward = 0f;

        currentDurationRewardMultiplier = startingDurationRewardMultiplier;
        currentTightnessRewardMultiplier = 1f;

        if (debugReward) Debug.Log("[Drift Hype] Started tracking drift reward.");
    }

    private void HandleDriftEndedClean(string reason)
    {
        if (!isTrackingDrift) return;

        bool isSpeedupCancel = !string.IsNullOrEmpty(reason) && reason.ToLowerInvariant().Contains("speedup");

        if (isSpeedupCancel && !payoutWhenCancelledBySpeedup)
        {
            ClearPendingReward("Clean Speedup cancel, payout disabled");
            return;
        }

        if (currentDriftDuration < minimumSuccessDuration)
        {
            ClearPendingReward("Too short");
            return;
        }

        if (pendingHypeReward <= 0.01f)
        {
            ClearPendingReward("No pending reward");
            return;
        }

        cartController.AddHype(pendingHypeReward);

        if (debugReward)
        {
            Debug.Log($"[Drift Hype] SUCCESS | +{pendingHypeReward:F1} Hype | duration:{currentDriftDuration:F2}s | durationMult:{currentDurationRewardMultiplier:F2} | tightnessMult:{currentTightnessRewardMultiplier:F2} | reason:{reason}");
        }

        ClearStateOnly();
    }

    private void HandleDriftInterrupted(string reason)
    {
        if (!isTrackingDrift) return;

        if (debugReward)
        {
            Debug.Log($"[Drift Hype] FAILED | lost {pendingHypeReward:F1} pending Hype | duration:{currentDriftDuration:F2}s | reason:{reason}");
        }

        ClearStateOnly();
    }

    #endregion

    #region Reward Accumulation

    private void UpdatePendingHype()
    {
        currentDriftDuration += Time.deltaTime;

        float tightness = Mathf.Clamp01(driftController.CurrentTightness);
        if (tightness < minimumRewardTightness) return;

        currentTightnessRewardMultiplier = GetTightnessRewardMultiplier(tightness);
        currentDurationRewardMultiplier = GetDurationRewardMultiplier();

        float gain = baseHypeGainPerSecond * currentTightnessRewardMultiplier * currentDurationRewardMultiplier * Time.deltaTime;
        pendingHypeReward = Mathf.Clamp(pendingHypeReward + gain, 0f, maxHypeRewardPerDrift);
    }

    private float GetTightnessRewardMultiplier(float tightness)
    {
        float tightness01 = Mathf.InverseLerp(minimumRewardTightness, 1f, tightness);
        return Mathf.Lerp(minTightnessRewardMultiplier, maxTightnessRewardMultiplier, tightness01);
    }

    private float GetDurationRewardMultiplier()
    {
        if (durationToReachMaxRewardRate <= 0.01f) return maxDurationRewardMultiplier;

        float duration01 = Mathf.Clamp01(currentDriftDuration / durationToReachMaxRewardRate);
        float curveValue = durationRewardCurve != null ? Mathf.Clamp01(durationRewardCurve.Evaluate(duration01)) : duration01;

        return Mathf.Lerp(startingDurationRewardMultiplier, maxDurationRewardMultiplier, curveValue);
    }

    #endregion

    #region State / Preview

    private void ClearPendingReward(string reason)
    {
        if (debugReward)
        {
            Debug.Log($"[Drift Hype] No payout | pending:{pendingHypeReward:F1} | duration:{currentDriftDuration:F2}s | reason:{reason}");
        }

        ClearStateOnly();
    }

    private void ClearStateOnly()
    {
        isTrackingDrift = false;
        currentDriftDuration = 0f;
        pendingHypeReward = 0f;

        currentDurationRewardMultiplier = 1f;
        currentTightnessRewardMultiplier = 1f;
    }

    private float GetPreviewHypeAmount01()
    {
        if (cartController == null || cartController.MaxHype <= 0.01f) return 0f;

        float previewHype = cartController.CurrentHype + pendingHypeReward;
        return Mathf.Clamp01(previewHype / cartController.MaxHype);
    }

    #endregion
}
