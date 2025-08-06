using UnityEngine;
using TMPro;

public class CashScoreManager : MonoBehaviour
{
    // Player 1 submitted cart counts
    private int p1CommonCount = 0;
    private int p1RareCount = 0;
    private int p1EpicCount = 0;
    private int p1LegendaryCount = 0;
    private float p1ComboBonus = 0f;

    // Player 2 submitted cart counts
    private int p2CommonCount = 0;
    private int p2RareCount = 0;
    private int p2EpicCount = 0;
    private int p2LegendaryCount = 0;
    private float p2ComboBonus = 0f;

    // Values
    [SerializeField] private float commonCartValue = 10f;
    [SerializeField] private float rareCartValue = 30f;
    [SerializeField] private float epicCartValue = 100f;
    [SerializeField] private float legendaryCartValue = 500f;

    [SerializeField] private float p1Final = 0f;
    [SerializeField] private float p2Final = 0f;

    [SerializeField] private TextMeshProUGUI cashScoreP1;
    [SerializeField] private TextMeshProUGUI cashScoreP2;

    void Start()
    {
        ResetAllScores();
    }

    void Update()
    {
        UpdateTotalCashEarnedP1();
        UpdateTotalCashEarnedP2();

        cashScoreP1.text = p1Final.ToString("F0");
        cashScoreP2.text = p2Final.ToString("F0");
    }

    public void AddCartToPlayer(CartRarity rarity, int playerIndex)
    {
        switch (playerIndex)
        {
            case 1:
                IncrementP1(rarity);
                break;
            case 2:
                IncrementP2(rarity);
                break;
        }
    }

    private void IncrementP1(CartRarity rarity)
    {
        switch (rarity)
        {
            case CartRarity.Common: p1CommonCount++; break;
            case CartRarity.Rare: p1RareCount++; break;
            case CartRarity.Epic: p1EpicCount++; break;
            case CartRarity.Legendary: p1LegendaryCount++; break;
        }
    }

    private void IncrementP2(CartRarity rarity)
    {
        switch (rarity)
        {
            case CartRarity.Common: p2CommonCount++; break;
            case CartRarity.Rare: p2RareCount++; break;
            case CartRarity.Epic: p2EpicCount++; break;
            case CartRarity.Legendary: p2LegendaryCount++; break;
        }
    }
    public void AddComboRewardToPlayer(float points, int playerIndex)
    {
        if (playerIndex == 1)
        {
            p1ComboBonus += points;
        }
        else if (playerIndex == 2)
        {
            p2ComboBonus += points;
        }

        //cashScoreP1.text = p1Final.ToString();
        //cashScoreP2.text = p2Final.ToString();
    }
    private void UpdateTotalCashEarnedP1()
    {
        float subtotal =
            p1CommonCount * commonCartValue +
            p1RareCount * rareCartValue +
            p1EpicCount * epicCartValue +
            p1LegendaryCount * legendaryCartValue;

        p1Final = subtotal + p1ComboBonus;
    }

    private void UpdateTotalCashEarnedP2()
    {
        float subtotal =
            p2CommonCount * commonCartValue +
            p2RareCount * rareCartValue +
            p2EpicCount * epicCartValue +
            p2LegendaryCount * legendaryCartValue;

        p2Final = subtotal + p2ComboBonus;
    }

    private void ResetAllScores()
    {
        p1CommonCount = p1RareCount = p1EpicCount = p1LegendaryCount = 0;
        p2CommonCount = p2RareCount = p2EpicCount = p2LegendaryCount = 0;

        p1Final = p2Final = 0f;
    }
}
