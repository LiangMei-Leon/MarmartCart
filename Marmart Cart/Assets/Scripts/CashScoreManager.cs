using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Hierarchy;

public class CashScoreManager : MonoBehaviour
{
    [Header("Item Values")]
    [SerializeField] private int normalItemValue = 10;
    [SerializeField] private int expensiveItemValue = 50;

    [SerializeField] private float p1TotalScore = 0f;
    [SerializeField] private float p2TotalScore = 0f;

    [SerializeField] private TextMeshProUGUI totalScoreP1TMP;
    [SerializeField] private TextMeshProUGUI totalScoreP2TMP;
    [SerializeField] private TextMeshProUGUI p1LastGainTMP;
    [SerializeField] private TextMeshProUGUI p2LastGainTMP;
    private Coroutine p1PopupAnim;
    private Coroutine p2PopupAnim;

    [Header("Checkout Session UI Root")]
    [SerializeField] private GameObject p1CheckoutLane1UIRoot;
    [SerializeField] private GameObject p1CheckoutLane2UIRoot;
    [SerializeField] private GameObject p2CheckoutLane1UIRoot;
    [SerializeField] private GameObject p2CheckoutLane2UIRoot;

    [Header("Checkout Session UI Element - Player 1 (Lanes)")]
    [SerializeField] private CheckoutLaneUI[] p1LaneUIs;

    [Header("Checkout Session UI Element - Player 2 (Lanes)")]
    [SerializeField] private CheckoutLaneUI[] p2LaneUIs;

    [System.Serializable]
    public class CheckoutLaneUI
    {
        public TextMeshPro itemsCountText;
        public GameObject streakTextUI;
        public TextMeshPro subtotalText;
    }
    // For simplicity: one coroutine per player per lane for subtotal
    private Coroutine[,] itemCountAnims = new Coroutine[2, 2];
    // [playerIndex-1, laneIndex]

    // --------- SESSION DATA STRUCT ---------
    [System.Serializable]
    public class CheckoutSessionData
    {
        public bool isActive;

        public int laneIndex; // Which lane the player is using

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
            laneIndex = -1;
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
        ShowCheckoutUI(1, 1, false);
        ShowCheckoutUI(1, 2, false);
        ShowCheckoutUI(2, 1, false);
        ShowCheckoutUI(2, 2, false);
        ResetCheckoutSessionUI(1,0);
        ResetCheckoutSessionUI(1,1);
        ResetCheckoutSessionUI(2,0);
        ResetCheckoutSessionUI(2,1);
        p1TotalScore = 0f;
        p2TotalScore = 0f;
        totalScoreP1TMP.text = p1TotalScore.ToString("F0");
        totalScoreP2TMP.text = p2TotalScore.ToString("F0");
    }

    void Update()
    {
        //totalScoreP1TMP.text = p1TotalScore.ToString("F0");
        //totalScoreP2TMP.text = p2TotalScore.ToString("F0");
    }
    // ------------- PUBLIC API -------------

    // Called when player ENTERS a checkout lane
    public void StartCheckoutSession(int playerIndex, int laneIndex)
    {
        CheckoutSessionData session = GetCurrentSession(playerIndex);
        if (session == null)
            return;

        session.Reset();
        session.isActive = true;
        session.laneIndex = laneIndex;
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
        // Update the correct lane UI
        UpdateCheckoutSessionUI(playerIndex, session);
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

        if (playerIndex == 1)
        {
            p1TotalScore += session.subtotal;
            ShowLastGainPopup(1, session.subtotal);
        }
        else if (playerIndex == 2)
        {
            p2TotalScore += session.subtotal;
            ShowLastGainPopup(2, session.subtotal);
        }

        // Copy to lastSession snapshot so UI can read it later
        lastSession.isActive = false;  // finished
        lastSession.itemsCount = session.itemsCount;
        lastSession.normalCount = session.normalCount;
        lastSession.expensiveCount = session.expensiveCount;
        lastSession.basePoints = session.basePoints;
        lastSession.multiplier = session.multiplier;
        lastSession.subtotal = session.subtotal;

        ResetCheckoutSessionUI(playerIndex, session.laneIndex);
        // Reset current session for next time
        session.Reset();
    }

    // Expose last-session data for UI (read-only)
    public CheckoutSessionData GetLastSessionData(int playerIndex)
    {
        return GetLastSession(playerIndex);
    }
    public void ShowCheckoutUI(int playerIndex, int laneIndex, bool show)
    {
        if (playerIndex == 1 && laneIndex == 1 && p1CheckoutLane1UIRoot != null)
            p1CheckoutLane1UIRoot.SetActive(show);
        else if (playerIndex == 1 && laneIndex == 2 && p1CheckoutLane2UIRoot != null)
            p1CheckoutLane2UIRoot.SetActive(show);
        else if (playerIndex == 2 && laneIndex == 1 && p2CheckoutLane1UIRoot != null)
            p2CheckoutLane1UIRoot.SetActive(show);
        else if (playerIndex == 2 && laneIndex == 2 && p2CheckoutLane2UIRoot != null)
            p2CheckoutLane2UIRoot.SetActive(show);
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
    // Method to update the UI for a player's checkout session

    private void UpdateCheckoutSessionUI(int playerIndex, CheckoutSessionData session)
    {
        if (session.laneIndex < 0)
            return;

        CheckoutLaneUI laneUI = null;

        if (playerIndex == 1)
        {
            if (p1LaneUIs == null || session.laneIndex >= p1LaneUIs.Length)
                return;
            laneUI = p1LaneUIs[session.laneIndex];
        }
        else if (playerIndex == 2)
        {
            if (p2LaneUIs == null || session.laneIndex >= p2LaneUIs.Length)
                return;
            laneUI = p2LaneUIs[session.laneIndex];
        }

        if (laneUI == null)
            return;

        if (laneUI.itemsCountText != null)
        {
            if(session.itemsCount >= 10)
            {
                laneUI.streakTextUI.SetActive(true);
                laneUI.itemsCountText.text = session.itemsCount.ToString();
            }
            else
            {
                laneUI.streakTextUI.SetActive(false);
            }
        }

        if (laneUI.subtotalText != null)
            laneUI.subtotalText.text = session.subtotal.ToString("F0");

        // trigger animations
        int pIdx = playerIndex - 1;
        int laneIdx = session.laneIndex;

        if (laneUI.subtotalText != null)
        {
            laneUI.subtotalText.text = "+" + session.subtotal.ToString("F0");

            if (itemCountAnims[pIdx, laneIdx] != null)
                StopCoroutine(itemCountAnims[pIdx, laneIdx]);

            itemCountAnims[pIdx, laneIdx] = StartCoroutine(
                AnimateTextPulse(laneUI.subtotalText.transform, 1.4f)
            );
        }
    }
    private IEnumerator AnimateTextPulse(Transform target, float scaleMultiplier = 1.5f, float duration = 0.3f)
    {
        Vector3 originalScale = new Vector3 (1f,1f,1f);
        target.localScale = originalScale;
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        // Scale up
        while (elapsed < halfDuration)
        {
            float t = elapsed / halfDuration;
            target.localScale = Vector3.Lerp(originalScale, originalScale * scaleMultiplier, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Scale down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float t = elapsed / halfDuration;
            target.localScale = Vector3.Lerp(originalScale * scaleMultiplier, originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localScale = originalScale;
    }
    private void ShowLastGainPopup(int playerIndex, float amount)
    {
        TextMeshProUGUI tmp = null;
        TextMeshProUGUI finalTmp = null;
        float total = 0f;
        ref Coroutine anim = ref p1PopupAnim;

        if (playerIndex == 1)
        {
            tmp = p1LastGainTMP;
            finalTmp = totalScoreP1TMP;
            anim = ref p1PopupAnim;
            total = p1TotalScore;
        }
        else
        {
            tmp = p2LastGainTMP;
            finalTmp = totalScoreP2TMP;
            anim = ref p2PopupAnim;
            total = p2TotalScore;
        }

        if (tmp == null)
            return;

        // Replace old popup instantly
        if (anim != null)
            StopCoroutine(anim);

        anim = StartCoroutine(AnimateScorePopup(tmp, finalTmp, amount, total));
    }
    private IEnumerator AnimateScorePopup(TextMeshProUGUI tmp, TextMeshProUGUI finalTotal, float amount, float total)
    {
        tmp.gameObject.SetActive(true);
        tmp.text = "+" + amount.ToString("F0");

        // Start values
        tmp.alpha = 1f;
        Vector3 originalPosition = tmp.rectTransform.anchoredPosition;
        Vector3 startPos = originalPosition;
        Vector3 endPos = startPos + new Vector3(0, 25f, 0);

        float duration = 1.5f;
        float time = 0f;

        while (time < duration)
        {
            float t = time / duration;

            // Move upward
            tmp.rectTransform.anchoredPosition = Vector3.Lerp(startPos, endPos, t);

            // Fade out
            if(time > duration * 0.9f)
                tmp.alpha = Mathf.Lerp(1f, 0f, t);

            time += Time.deltaTime;
            yield return null;
        }
        finalTotal.text = total.ToString("F0");
        tmp.rectTransform.anchoredPosition = originalPosition;
        tmp.gameObject.SetActive(false);
    }
    private void ResetCheckoutSessionUI(int playerIndex, int laneIndex)
    {
        if (laneIndex < 0)
            return;

        CheckoutLaneUI laneUI = null;

        if (playerIndex == 1)
        {
            if (p1LaneUIs == null || laneIndex >= p1LaneUIs.Length)
                return;
            laneUI = p1LaneUIs[laneIndex];
        }
        else if (playerIndex == 2)
        {
            if (p2LaneUIs == null || laneIndex >= p2LaneUIs.Length)
                return;
            laneUI = p2LaneUIs[laneIndex];
        }

        if (laneUI == null)
            return;

        if (laneUI.itemsCountText != null)
            laneUI.itemsCountText.text = "0";

        laneUI.streakTextUI.SetActive(false);

        if (laneUI.subtotalText != null)
            laneUI.subtotalText.text = "0";
    }
}
