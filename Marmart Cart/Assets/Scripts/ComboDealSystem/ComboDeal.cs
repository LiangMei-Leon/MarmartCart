using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ComboDeal : MonoBehaviour
{
    [Header("Combo Deal UIs")]
    [SerializeField] private GameObject comboDealUI;
    [SerializeField] private TextMeshProUGUI dealReward;
    [SerializeField] private TextMeshProUGUI commonCartRemainingText;
    [SerializeField] private TextMeshProUGUI rareCartRemainingText;
    [SerializeField] private TextMeshProUGUI epicCartRemainingText;
    [SerializeField] private TextMeshProUGUI legendaryCartRemainingText;
    [SerializeField] private Slider timerSlider;
    public int DealID;
    public float RewardPoints { get; private set; }

    [Header("UI Controller Management")]
    public int stackIndex;
    public float PanelWidth => GetComponent<RectTransform>().rect.width;
    [SerializeField] public ComboDealUIController uiController;
    private Dictionary<CartRarity, int> requirements;
    private float duration;
    private float startTime;
    private bool completed = false;

    public void Initialize(int id, float rewardPoints, Dictionary<CartRarity, int> reqs, float durationSeconds)
    {
        DealID = id;
        RewardPoints = rewardPoints;
        requirements = new Dictionary<CartRarity, int>(reqs);
        duration = durationSeconds;
        startTime = Time.time;

        // Use controller to set start position
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 startPos = uiController.GetSpawnStartPosition();
        rect.anchoredPosition = startPos;

        UpdateUI();
        uiController.Register(this);
    }
    void Update()
    {
        if (comboDealUI == null || completed) return;

        float remainingTime = TimeRemaining;
        timerSlider.value = remainingTime / duration;

        if (IsExpired)
        {
            uiController.Unregister(this);
            comboDealUI.SetActive(false);
        }
    }

    public bool IsExpired => Time.time > startTime + duration;
    public bool IsCompleted => completed;
    public float TimeRemaining => Mathf.Max(0, (startTime + duration) - Time.time);
    public void SetCoreData(float rewardPoints, Dictionary<CartRarity, int> reqs, float durationSeconds)
    {
        RewardPoints = rewardPoints;
        requirements = new Dictionary<CartRarity, int>(reqs);
        duration = durationSeconds;
        startTime = Time.time;
    }
    public void InitializeFromLogic(ComboDeal source, Vector2 startPosition)
    {
        DealID = source.DealID;
        RewardPoints = source.RewardPoints;
        requirements = new Dictionary<CartRarity, int>(source.GetRemainingRequirements());
        duration = source.duration;
        startTime = Time.time;

        RectTransform rect = GetComponent<RectTransform>();
        rect.anchoredPosition = startPosition;

        UpdateUI();
        uiController.Register(this);
    }
    public bool SubmitCart(CartRarity rarity)
    {
        if (completed || !requirements.ContainsKey(rarity) || requirements[rarity] <= 0)
            return false;

        requirements[rarity]--;
        UpdateUI();
        CheckCompletion();
        return true;
    }

    private void CheckCompletion()
    {
        foreach (var cartLeft in requirements)
        {
            if (cartLeft.Value > 0)
                return;
        }
        // get to this point means all requirements are met, all carts value are zero
        completed = true;
        OnCompleted();
    }

    private void OnCompleted()
    {
        Debug.Log($"Combo Deal {DealID} completed! Reward: {RewardPoints} points");
        uiController.Unregister(this);
        comboDealUI.SetActive(false);
    }
    private void UpdateUI()
    {
        if (dealReward != null)
            dealReward.text = RewardPoints.ToString() + " PTS";

        if (commonCartRemainingText != null && requirements.ContainsKey(CartRarity.Common))
            commonCartRemainingText.text = requirements[CartRarity.Common].ToString();

        if (rareCartRemainingText != null && requirements.ContainsKey(CartRarity.Rare))
            rareCartRemainingText.text = requirements[CartRarity.Rare].ToString();

        if (epicCartRemainingText != null && requirements.ContainsKey(CartRarity.Epic))
            epicCartRemainingText.text = requirements[CartRarity.Epic].ToString();

        if (legendaryCartRemainingText != null && requirements.ContainsKey(CartRarity.Legendary))
            legendaryCartRemainingText.text = requirements[CartRarity.Legendary].ToString();
    }
    public Dictionary<CartRarity, int> GetRemainingRequirements()
    {
        return new Dictionary<CartRarity, int>(requirements);
    }
}
