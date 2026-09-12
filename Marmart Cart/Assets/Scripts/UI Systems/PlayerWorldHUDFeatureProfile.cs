using UnityEngine;

/// <summary>
/// Experimental information-channel toggles for the near-cart world HUD.
/// These answer "should we communicate this?", not "what should it look like?".
/// </summary>
[CreateAssetMenu(menuName = "Marmart Carts/Player HUD/World HUD Feature Profile", fileName = "PlayerWorldHUDFeatureProfile")]
public class PlayerWorldHUDFeatureProfile : ScriptableObject
{
    [Header("Hype Information")]
    [SerializeField] private bool showHype = true;
    [SerializeField] private bool showHypeExactValues = false;
    [SerializeField] private bool showHypeBurnFeedback = true;
    [SerializeField] private bool showSpeedingUpFeedback = true;
    [SerializeField] private bool showDriftRewardPreview = true;

    [Header("Load Information")]
    [SerializeField] private bool showLoad = true;
    [SerializeField] private bool showLoadExactValues = false;
    [SerializeField] private bool showRemainingSafeCapacity = true;
    [SerializeField] private bool showOverloadAmount = true;
    [SerializeField] private bool showOverloadSpeedWarning = true;

    [Header("Speed Information")]
    [Tooltip("General speed-consequence communication. Does not imply displaying an actual speed number.")]
    [SerializeField] private bool showSpeedConsequence = true;

    [Header("Checkout / Streak Information")]
    [SerializeField] private bool showCheckoutStreakEligibility = false;
    [SerializeField] private bool showCheckoutStreakProgress = false;

    public bool ShowHype => showHype;
    public bool ShowHypeExactValues => showHypeExactValues;
    public bool ShowHypeBurnFeedback => showHypeBurnFeedback;
    public bool ShowSpeedingUpFeedback => showSpeedingUpFeedback;
    public bool ShowDriftRewardPreview => showDriftRewardPreview;
    public bool ShowLoad => showLoad;
    public bool ShowLoadExactValues => showLoadExactValues;
    public bool ShowRemainingSafeCapacity => showRemainingSafeCapacity;
    public bool ShowOverloadAmount => showOverloadAmount;
    public bool ShowOverloadSpeedWarning => showOverloadSpeedWarning;
    public bool ShowSpeedConsequence => showSpeedConsequence;
    public bool ShowCheckoutStreakEligibility => showCheckoutStreakEligibility;
    public bool ShowCheckoutStreakProgress => showCheckoutStreakProgress;
}
