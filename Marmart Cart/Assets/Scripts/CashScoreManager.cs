using System;
using System.Collections;
using TMPro;
using Unity.Hierarchy;
using UnityEngine;

public class CashScoreManager : MonoBehaviour
{
    public const int MaxPlayers = 4;
    public const int MaxLanes = 2;

    [Header("Item Values")]
    [SerializeField] private int normalItemValue = 10;
    [SerializeField] private int expensiveItemValue = 50;

    [Header("Team Mapping (used only in TeamBattle mode)")]
    [Tooltip("Example: Team 1 = players 1 & 3")]
    [SerializeField] private int[] team1Players = new[] { 1, 3 };
    [Tooltip("Example: Team 2 = players 2 & 4")]
    [SerializeField] private int[] team2Players = new[] { 2, 4 };

    [Header("Totals (debug)")]
    [SerializeField] private float[] playerTotalScore = new float[MaxPlayers];

    private Coroutine[] popupAnims = new Coroutine[MaxPlayers];

    [Header("Checkout Session UI Roots (optional)")]
    [Tooltip("Per player, per lane root. [playerIndex-1, laneIndex]")]
    [SerializeField] private GameObject[] checkoutLaneUIRoots = new GameObject[MaxPlayers * MaxLanes];

    [Header("Checkout Session UI Elements (optional)")]
    [SerializeField] private CheckoutLaneUI[] laneUIs = new CheckoutLaneUI[MaxPlayers * MaxLanes];

    [Serializable]
    public class CheckoutLaneUI
    {
        public TextMeshPro itemsCountText;
        public GameObject streakTextUI;
        public TextMeshPro subtotalText;
    }

    private Coroutine[,] subtotalPulseAnims = new Coroutine[MaxPlayers, MaxLanes];

    // --------- SESSION DATA ---------
    [Serializable]
    public class CheckoutSessionData
    {
        public bool isActive;
        public int laneIndex;

        public int itemsCount;
        public int normalCount;
        public int expensiveCount;

        public float basePoints;
        public float multiplier;
        public float subtotal;

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

    private CheckoutSessionData[] currentSession = new CheckoutSessionData[MaxPlayers];
    private CheckoutSessionData[] lastSession = new CheckoutSessionData[MaxPlayers];

    private int ActivePlayerCount => Mathf.Clamp(GMode.Instance ? GMode.Instance.PlayerCount() : 2, 1, MaxPlayers);

    // ---------- EVENTS ----------
    public event Action<int, int, int> OnPlayerScoreGained; // (playerIndex, gain, newTotal)
    public event Action<int, int, int> OnTeamScoreGained;   // (teamIndex, gain, teamTotal)

    private void Awake()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            currentSession[i] = new CheckoutSessionData();
            lastSession[i] = new CheckoutSessionData();
        }
    }

    private void Start()
    {
        ResetAllScores();

        // Hide lane UIs by default if wired
        for (int p = 1; p <= ActivePlayerCount; p++)
        {
            for (int lane = 1; lane <= MaxLanes; lane++)
                ShowCheckoutUI(p, lane, false);

            for (int laneIdx = 0; laneIdx < MaxLanes; laneIdx++)
                ResetCheckoutSessionUI(p, laneIdx);
        }
    }

    // ---------------- PUBLIC API ----------------

    public void StartCheckoutSession(int playerIndex, int laneIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        var session = currentSession[playerIndex - 1];
        session.Reset();
        session.isActive = true;
        session.laneIndex = laneIndex;
    }

    public void RegisterItemCheckout(int playerIndex, bool isExpensive)
    {
        if (!IsValidPlayer(playerIndex)) return;

        var session = currentSession[playerIndex - 1];

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

        session.multiplier = GetComboMultiplier(session.itemsCount);
        session.subtotal = session.basePoints * session.multiplier;

        UpdateCheckoutSessionUI(playerIndex, session);
    }

    public void EndCheckoutSession(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        var session = currentSession[playerIndex - 1];
        var last = lastSession[playerIndex - 1];

        if (!session.isActive || session.itemsCount <= 0)
        {
            session.Reset();
            return;
        }

        int gain = Mathf.RoundToInt(session.subtotal);

        // Apply to player total
        playerTotalScore[playerIndex - 1] += gain;
        int newTotal = GetPlayerScore(playerIndex);


        // event for per-screen leaderboard popup
        OnPlayerScoreGained?.Invoke(playerIndex, gain, newTotal);

        // If team mode, also fire team gain event (same gain)
        if (GMode.Instance && GMode.Instance.IsTeamBattle)
        {
            int team = GetTeamIndexForPlayer(playerIndex);
            if (team != 0)
            {
                int teamTotal = GetTeamScore(team == 1 ? team1Players : team2Players);
                OnTeamScoreGained?.Invoke(team, gain, teamTotal);
            }
        }

        // snapshot last session
        last.isActive = false;
        last.itemsCount = session.itemsCount;
        last.normalCount = session.normalCount;
        last.expensiveCount = session.expensiveCount;
        last.basePoints = session.basePoints;
        last.multiplier = session.multiplier;
        last.subtotal = session.subtotal;
        last.laneIndex = session.laneIndex;

        ResetCheckoutSessionUI(playerIndex, session.laneIndex);
        session.Reset();
    }

    public CheckoutSessionData GetLastSessionData(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return null;
        return lastSession[playerIndex - 1];
    }

    public void ShowCheckoutUI(int playerIndex, int laneIndex, bool show)
    {
        if (!IsValidPlayer(playerIndex)) return;
        if (laneIndex < 1 || laneIndex > MaxLanes) return;

        int idx = RootIndex(playerIndex, laneIndex - 1);
        var root = checkoutLaneUIRoots != null && idx >= 0 && idx < checkoutLaneUIRoots.Length
            ? checkoutLaneUIRoots[idx]
            : null;

        if (root != null) root.SetActive(show);
    }

    // ---------------- GETTERS ----------------

    public int GetPlayerScore(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return 0;
        return Mathf.RoundToInt(playerTotalScore[playerIndex - 1]);
    }

    public int GetTeamScore(int[] teamPlayers)
    {
        int sum = 0;
        if (teamPlayers == null) return 0;

        foreach (int p in teamPlayers)
            sum += GetPlayerScore(p);

        return sum;
    }

    public int GetTeamIndexForPlayer(int playerIndex)
    {
        if (team1Players != null)
            for (int i = 0; i < team1Players.Length; i++)
                if (team1Players[i] == playerIndex) return 1;

        if (team2Players != null)
            for (int i = 0; i < team2Players.Length; i++)
                if (team2Players[i] == playerIndex) return 2;

        return 0;
    }

    // ---------------- INTERNAL ----------------

    private bool IsValidPlayer(int playerIndex)
    {
        return playerIndex >= 1 && playerIndex <= ActivePlayerCount;
    }

    private void ResetAllScores()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            playerTotalScore[i] = 0f;
            currentSession[i].Reset();
            lastSession[i].Reset();
        }
    }

    private float GetComboMultiplier(int itemsCount)
    {
        if (itemsCount <= 10) return 1f;
        if (itemsCount <= 20) return 1f + 0.1f * (itemsCount - 10);
        if (itemsCount <= 30) return 2f + 0.2f * (itemsCount - 20);
        return 4f;
    }

    private void UpdateCheckoutSessionUI(int playerIndex, CheckoutSessionData session)
    {
        if (session.laneIndex < 0) return;
        int laneIdx = session.laneIndex;

        var ui = GetLaneUI(playerIndex, laneIdx);
        if (ui == null) return;

        if (ui.itemsCountText != null)
        {
            if (session.itemsCount >= 10)
            {
                if (ui.streakTextUI) ui.streakTextUI.SetActive(true);
                ui.itemsCountText.text = session.itemsCount.ToString();
            }
            else
            {
                if (ui.streakTextUI) ui.streakTextUI.SetActive(false);
            }
        }

        if (ui.subtotalText != null)
        {
            ui.subtotalText.text = "+" + session.subtotal.ToString("F0");

            int pIdx = playerIndex - 1;
            if (subtotalPulseAnims[pIdx, laneIdx] != null)
                StopCoroutine(subtotalPulseAnims[pIdx, laneIdx]);

            subtotalPulseAnims[pIdx, laneIdx] = StartCoroutine(
                AnimateTextPulse(ui.subtotalText.transform, 1.4f)
            );
        }
    }

    private CheckoutLaneUI GetLaneUI(int playerIndex, int laneIndex)
    {
        int idx = LaneIndex(playerIndex, laneIndex);
        if (laneUIs == null || idx < 0 || idx >= laneUIs.Length) return null;
        return laneUIs[idx];
    }

    private void ResetCheckoutSessionUI(int playerIndex, int laneIndex)
    {
        var ui = GetLaneUI(playerIndex, laneIndex);
        if (ui == null) return;

        if (ui.itemsCountText != null) ui.itemsCountText.text = "0";
        if (ui.streakTextUI != null) ui.streakTextUI.SetActive(false);
        if (ui.subtotalText != null) ui.subtotalText.text = "0";
    }

    private IEnumerator AnimateTextPulse(Transform target, float scaleMultiplier = 1.5f, float duration = 0.3f)
    {
        Vector3 originalScale = Vector3.one;
        target.localScale = originalScale;

        float half = duration * 0.5f;
        float t = 0f;

        while (t < half)
        {
            float a = t / half;
            target.localScale = Vector3.Lerp(originalScale, originalScale * scaleMultiplier, a);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            float a = t / half;
            target.localScale = Vector3.Lerp(originalScale * scaleMultiplier, originalScale, a);
            t += Time.deltaTime;
            yield return null;
        }

        target.localScale = originalScale;
    }

    private IEnumerator AnimateScorePopup(TextMeshProUGUI tmp, TextMeshProUGUI finalTotal, int gain, int total)
    {
        tmp.gameObject.SetActive(true);
        tmp.text = "+" + gain;

        tmp.alpha = 1f;
        Vector3 originalPosition = tmp.rectTransform.anchoredPosition;
        Vector3 startPos = originalPosition;
        Vector3 endPos = startPos + new Vector3(0, 25f, 0);

        float duration = 1.0f;
        float time = 0f;

        while (time < duration)
        {
            float a = time / duration;
            tmp.rectTransform.anchoredPosition = Vector3.Lerp(startPos, endPos, a);

            if (time > duration * 0.8f)
                tmp.alpha = Mathf.Lerp(1f, 0f, a);

            time += Time.deltaTime;
            yield return null;
        }

        finalTotal.text = total.ToString();
        tmp.rectTransform.anchoredPosition = originalPosition;
        tmp.gameObject.SetActive(false);
    }

    private static T GetArray<T>(T[] arr, int idx) where T : class
    {
        if (arr == null) return null;
        if (idx < 0 || idx >= arr.Length) return null;
        return arr[idx];
    }

    // Flattened indices for inspector-friendly arrays:
    // laneUIs[(player-1)*2 + laneIdx]
    private static int LaneIndex(int playerIndex, int laneIndex) => (playerIndex - 1) * MaxLanes + laneIndex;
    private static int RootIndex(int playerIndex, int laneIndex) => (playerIndex - 1) * MaxLanes + laneIndex;
}
