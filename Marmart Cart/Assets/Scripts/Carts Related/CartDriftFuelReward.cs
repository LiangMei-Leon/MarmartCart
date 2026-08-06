using UnityEngine;

public class CartDriftFuelReward : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private CartControlScript cartController;

    [Header("Reward Toggle")]
    [SerializeField] private bool enableDriftFuelReward = true;

    [Header("Success Rules")]
    [SerializeField] private float minimumSuccessDuration = 0.6f;

    [Header("Base Reward")]
    [Tooltip("Base fuel gained per second before tightness and duration multipliers. Meter is 0-100.")]
    [SerializeField] private float baseFuelGainPerSecond = 5f;

    [Tooltip("Maximum fuel that can be earned from one drift.")]
    [SerializeField] private float maxFuelRewardPerDrift = 25f;

    [Header("Tightness Reward")]
    [Tooltip("No pending fuel is gained below this drift tightness.")]
    [SerializeField, Range(0f, 1f)] private float minimumRewardTightness = 0.2f;

    [Tooltip("Multiplier when tightness is at minimumRewardTightness.")]
    [SerializeField] private float minTightnessRewardMultiplier = 0.5f;

    [Tooltip("Multiplier when tightness is 1.")]
    [SerializeField] private float maxTightnessRewardMultiplier = 1.3f;

    [Header("Duration Reward Ramp")]
    [Tooltip("How many seconds it takes for duration reward rate to reach maximum.")]
    [SerializeField] private float durationToReachMaxRewardRate = 3f;

    [Tooltip("Reward multiplier at the beginning of the drift.")]
    [SerializeField] private float startingDurationRewardMultiplier = 0.35f;

    [Tooltip("Reward multiplier after Duration To Reach Max Reward Rate.")]
    [SerializeField] private float maxDurationRewardMultiplier = 2.0f;

    [Tooltip("Higher values make long drifts ramp reward faster near the end.")]
    [SerializeField]
    private AnimationCurve durationRewardCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.4f, 0.25f),
        new Keyframe(0.75f, 0.7f),
        new Keyframe(1f, 1f)
    );

    [Header("Clean End Rules")]
    [Tooltip("If true, pressing boost while drifting still pays out pending drift fuel.")]
    [SerializeField] private bool payoutWhenCancelledBySpeedup = true;

    [Header("Debug")]
    [SerializeField] private bool debugReward = true;

    public bool IsTrackingDrift => isTrackingDrift;
    public float CurrentDriftDuration => currentDriftDuration;
    public float PendingFuelReward => pendingFuelReward;
    public float PreviewFuelAmount => GetPreviewFuelAmount01();
    public bool HasPendingReward => pendingFuelReward > 0.01f;

    public float CurrentDurationRewardMultiplier => currentDurationRewardMultiplier;
    public float CurrentTightnessRewardMultiplier => currentTightnessRewardMultiplier;
    public float CurrentTotalRewardMultiplier => currentDurationRewardMultiplier * currentTightnessRewardMultiplier;

    private bool isTrackingDrift = false;
    private float currentDriftDuration = 0f;
    private float pendingFuelReward = 0f;

    private float currentDurationRewardMultiplier = 1f;
    private float currentTightnessRewardMultiplier = 1f;

    private void Awake()
    {
        if (!driftController)
            driftController = GetComponentInParent<CartDriftController>();

        if (!cartController)
            cartController = GetComponentInParent<CartControlScript>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!enableDriftFuelReward)
            return;

        if (!isTrackingDrift)
            return;

        if (driftController == null || cartController == null)
            return;

        if (!driftController.IsDrifting)
            return;

        UpdatePendingFuel();
    }

    private void Subscribe()
    {
        if (driftController == null)
            return;

        driftController.OnDriftStarted += HandleDriftStarted;
        driftController.OnDriftEndedClean += HandleDriftEndedClean;
        driftController.OnDriftInterrupted += HandleDriftInterrupted;
    }

    private void Unsubscribe()
    {
        if (driftController == null)
            return;

        driftController.OnDriftStarted -= HandleDriftStarted;
        driftController.OnDriftEndedClean -= HandleDriftEndedClean;
        driftController.OnDriftInterrupted -= HandleDriftInterrupted;
    }

    private void HandleDriftStarted()
    {
        if (!enableDriftFuelReward)
            return;

        isTrackingDrift = true;
        currentDriftDuration = 0f;
        pendingFuelReward = 0f;

        currentDurationRewardMultiplier = startingDurationRewardMultiplier;
        currentTightnessRewardMultiplier = 1f;

        if (debugReward)
            Debug.Log("[Drift Fuel] Started tracking drift reward.");
    }

    private void UpdatePendingFuel()
    {
        currentDriftDuration += Time.deltaTime;

        float tightness = Mathf.Clamp01(driftController.CurrentTightness);

        if (tightness < minimumRewardTightness)
            return;

        currentTightnessRewardMultiplier = GetTightnessRewardMultiplier(tightness);
        currentDurationRewardMultiplier = GetDurationRewardMultiplier();

        float gain =
            baseFuelGainPerSecond *
            currentTightnessRewardMultiplier *
            currentDurationRewardMultiplier *
            Time.deltaTime;

        pendingFuelReward = Mathf.Clamp(
            pendingFuelReward + gain,
            0f,
            maxFuelRewardPerDrift
        );
    }

    private float GetTightnessRewardMultiplier(float tightness)
    {
        float tightness01 = Mathf.InverseLerp(
            minimumRewardTightness,
            1f,
            tightness
        );

        return Mathf.Lerp(
            minTightnessRewardMultiplier,
            maxTightnessRewardMultiplier,
            tightness01
        );
    }

    private float GetDurationRewardMultiplier()
    {
        if (durationToReachMaxRewardRate <= 0.01f)
            return maxDurationRewardMultiplier;

        float duration01 = Mathf.Clamp01(
            currentDriftDuration / durationToReachMaxRewardRate
        );

        float curveValue = durationRewardCurve.Evaluate(duration01);
        curveValue = Mathf.Clamp01(curveValue);

        return Mathf.Lerp(
            startingDurationRewardMultiplier,
            maxDurationRewardMultiplier,
            curveValue
        );
    }

    private void HandleDriftEndedClean(string reason)
    {
        if (!isTrackingDrift)
            return;

        bool isSpeedupCancel = reason.ToLower().Contains("speedup");

        if (isSpeedupCancel && !payoutWhenCancelledBySpeedup)
        {
            ClearPendingReward("Clean speedup cancel, payout disabled");
            return;
        }

        if (currentDriftDuration < minimumSuccessDuration)
        {
            ClearPendingReward("Too short");
            return;
        }

        if (pendingFuelReward <= 0.01f)
        {
            ClearPendingReward("No pending reward");
            return;
        }

        cartController.RefillSpeedUpMeter(pendingFuelReward);

        if (debugReward)
        {
            Debug.Log(
                $"[Drift Fuel] SUCCESS | +" +
                $"{pendingFuelReward:F1} fuel | " +
                $"duration:{currentDriftDuration:F2}s | " +
                $"durationMult:{currentDurationRewardMultiplier:F2} | " +
                $"tightnessMult:{currentTightnessRewardMultiplier:F2} | " +
                $"reason:{reason}"
            );
        }

        ClearStateOnly();
    }

    private void HandleDriftInterrupted(string reason)
    {
        if (!isTrackingDrift)
            return;

        if (debugReward)
        {
            Debug.Log(
                $"[Drift Fuel] FAILED | lost {pendingFuelReward:F1} pending fuel | " +
                $"duration:{currentDriftDuration:F2}s | " +
                $"reason:{reason}"
            );
        }

        ClearStateOnly();
    }

    private void ClearPendingReward(string reason)
    {
        if (debugReward)
        {
            Debug.Log(
                $"[Drift Fuel] No payout | pending:{pendingFuelReward:F1} | " +
                $"duration:{currentDriftDuration:F2}s | reason:{reason}"
            );
        }

        ClearStateOnly();
    }

    private void ClearStateOnly()
    {
        isTrackingDrift = false;
        currentDriftDuration = 0f;
        pendingFuelReward = 0f;

        currentDurationRewardMultiplier = 1f;
        currentTightnessRewardMultiplier = 1f;
    }

    private float GetPreviewFuelAmount01()
    {
        if (cartController == null)
            return 0f;

        float currentFuel = cartController.GetSpeedUpMeter();
        float previewFuel = currentFuel + pendingFuelReward;

        return Mathf.Clamp01(previewFuel / 100f);
    }
}