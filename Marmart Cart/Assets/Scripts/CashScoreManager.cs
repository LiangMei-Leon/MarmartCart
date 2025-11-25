using UnityEngine;
using TMPro;

public class CashScoreManager : MonoBehaviour
{
    [Header("Item Values")]
    [SerializeField] private int normalItemValue = 10;
    [SerializeField] private int expensiveItemValue = 50;

    [SerializeField] private float p1TotalScore = 0f;
    [SerializeField] private float p2TotalScore = 0f;

    [SerializeField] private TextMeshProUGUI totalScoreP1TMP;
    [SerializeField] private TextMeshProUGUI totalScoreP2TMP;

    // --------- SESSION DATA STRUCT ---------
    [System.Serializable]
    public class CheckoutSessionData
    {
        public bool isActive;

        public int itemsCount;
        public int normalCount;
        public int expensiveCount;

        public float basePoints;    // Sum before combo multiplier
        public float multiplier;    // Multiplier used when session ends
        public float subtotal;      // basePoints * multiplier

        public void Reset()
        {
            isActive = false;
            itemsCount = 0;
            normalCount = 0;
            expensiveCount = 0;
            basePoints = 0f;
            multiplier = 1f;
            subtotal = 0f;
        }
    }

    // Current session for each player
    private CheckoutSessionData p1CurrentSession = new CheckoutSessionData();
    private CheckoutSessionData p2CurrentSession = new CheckoutSessionData();

    // Last finished session snapshot (for UI / VFX)
    private CheckoutSessionData p1LastSession = new CheckoutSessionData();
    private CheckoutSessionData p2LastSession = new CheckoutSessionData();

    void Start()
    {
        ResetAllScores();
    }

    void Update()
    {
        totalScoreP1TMP.text = p1TotalScore.ToString("F0");
        totalScoreP2TMP.text = p2TotalScore.ToString("F0");
    }
    // ------------- PUBLIC API -------------

    // Called when player ENTERS a checkout lane
    public void StartCheckoutSession(int playerIndex)
    {
        CheckoutSessionData session = GetCurrentSession(playerIndex);
        if (session == null)
            return;

        session.Reset();
        session.isActive = true;
    }

    // Called once per cart scanned (from CheckOutNextCartWithItem)
    public void RegisterItemCheckout(int playerIndex, bool isExpensive)
    {
        CheckoutSessionData session = GetCurrentSession(playerIndex);
        if (session == null)
            return;

        // Lazy start: if somehow session wasn't started, start it now
        if (!session.isActive)
        {
            session.Reset();
            session.isActive = true;
        }

        session.itemsCount++;

        if (isExpensive)
        {
            session.expensiveCount++;
            session.basePoints += expensiveItemValue;
        }
        else
        {
            session.normalCount++;
            session.basePoints += normalItemValue;
        }

        // Compute multiplier with soft exponential / 10-cart gate
        session.multiplier = GetComboMultiplier(session.itemsCount);
        session.subtotal = session.basePoints * session.multiplier;
    }

    // Called when player LEAVES the checkout lane or finishes scanning
    public void EndCheckoutSession(int playerIndex)
    {
        CheckoutSessionData session = GetCurrentSession(playerIndex);
        CheckoutSessionData lastSession = GetLastSession(playerIndex);
        if (session == null || lastSession == null)
            return;

        if (!session.isActive || session.itemsCount <= 0)
        {
            // Nothing was checked out in this session
            session.Reset();
            return;
        }

        // Add to total score
        if (playerIndex == 1)
            p1TotalScore += session.subtotal;
        else if (playerIndex == 2)
            p2TotalScore += session.subtotal;

        // Copy to lastSession snapshot so UI can read it later
        lastSession.isActive = false;  // finished
        lastSession.itemsCount = session.itemsCount;
        lastSession.normalCount = session.normalCount;
        lastSession.expensiveCount = session.expensiveCount;
        lastSession.basePoints = session.basePoints;
        lastSession.multiplier = session.multiplier;
        lastSession.subtotal = session.subtotal;

        // Reset current session for next time
        session.Reset();
    }

    // Expose last-session data for UI (read-only)
    public CheckoutSessionData GetLastSessionData(int playerIndex)
    {
        return GetLastSession(playerIndex);
    }
    // ------------- INTERNAL HELPERS -------------

    private CheckoutSessionData GetCurrentSession(int playerIndex)
    {
        switch (playerIndex)
        {
            case 1: return p1CurrentSession;
            case 2: return p2CurrentSession;
            default: return null;
        }
    }

    private CheckoutSessionData GetLastSession(int playerIndex)
    {
        switch (playerIndex)
        {
            case 1: return p1LastSession;
            case 2: return p2LastSession;
            default: return null;
        }
    }

    private void ResetAllScores()
    {
        p1TotalScore = 0f;
        p2TotalScore = 0f;

        p1CurrentSession.Reset();
        p2CurrentSession.Reset();
        p1LastSession.Reset();
        p2LastSession.Reset();
    }

    // SOFT-EXPONENTIAL COMBO CURVE:
    // - <=10 carts => x1.0
    // - 11–20      => +0.1 per extra cart (1.1–2.0)
    // - 21–30      => +0.2 per extra cart (2.2–4.0)
    // - >30        => capped at x4.0
    private float GetComboMultiplier(int itemsCount)
    {
        if (itemsCount <= 10)
            return 1f;

        if (itemsCount <= 20)
        {
            // 11 => 1.1, 20 => 2.0
            return 1f + 0.1f * (itemsCount - 10);
        }

        if (itemsCount <= 30)
        {
            // 21 => 2.2, 30 => 4.0
            return 2f + 0.2f * (itemsCount - 20);
        }

        // Cap
        return 4f;
    }
}
