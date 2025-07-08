using UnityEngine;
using TMPro;

public class CashScoreManager : MonoBehaviour
{
    private int p1CheckedoutNormalCartCount = 0;
    private int p2CheckedoutNormalCartCount = 0;
    private int p1CheckedoutBonusCartCount = 0;
    private int p2CheckedoutBonusCartCount = 0;

    [SerializeField] private float normalCartValue = 100f;
    [SerializeField] private float bonusCartValue = 1000f;

    [SerializeField] private float p1Final = 0f;
    [SerializeField] private float p2Final = 0f;

    [SerializeField] private TextMeshProUGUI cashScoreP1;
    [SerializeField] private TextMeshProUGUI cashScoreP2;
    void Start()
    {
        p1CheckedoutNormalCartCount = 0;
        p2CheckedoutNormalCartCount = 0;
        p1CheckedoutBonusCartCount = 0;
        p2CheckedoutBonusCartCount = 0;

        p1Final = 0f;
        p2Final = 0f;
}

    // Update is called once per frame
    void Update()
    {
        UpdateTotalCashEarnedP1();
        UpdateTotalCashEarnedP2();
        cashScoreP1.text = p1Final.ToString();
        cashScoreP2.text = p2Final.ToString();
    }

    public void IncreaseP1CheckedoutNormalCartCount()
    {
        p1CheckedoutNormalCartCount++;
    }
    public void IncreaseP2CheckedoutNormalCartCount()
    {
        p2CheckedoutNormalCartCount++;
    }
    public void IncreaseP1CheckedoutBonusCartCount()
    {
        p1CheckedoutBonusCartCount++;
    }
    public void IncreaseP2CheckedoutBonusCartCount()
    {
        p2CheckedoutBonusCartCount++;
    }

    private void UpdateTotalCashEarnedP1()
    {
        p1Final = p1CheckedoutNormalCartCount * normalCartValue + p1CheckedoutBonusCartCount * bonusCartValue;
    }

    private void UpdateTotalCashEarnedP2()
    {
        p2Final = p2CheckedoutNormalCartCount * normalCartValue + p2CheckedoutBonusCartCount * bonusCartValue;
    }
}
