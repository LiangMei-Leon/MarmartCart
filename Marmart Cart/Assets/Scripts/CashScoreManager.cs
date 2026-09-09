using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns persistent player/team score and checkout-session reward calculation.
///
/// NEW checkout scoring:
/// - base score comes from CargoEntry.ScoreValue;
/// - streak / milestone bonuses are based on total CargoEntry count submitted
///   during the current checkout session;
/// - physical cart count is tracked for presentation only, not streak value.
///
/// </summary>
public class CashScoreManager : MonoBehaviour
{
    public const int MaxPlayers = 4;
    public const int MaxLanes = 2;


    #region Cargo Streak Bonuses

    [Header("Cargo Checkout Milestone Bonuses")]
    [Tooltip("Thresholds are now TOTAL CARGO ENTRIES submitted during one checkout session.")]
    [SerializeField] private int streakThresholdLvl1 = 10;
    [SerializeField] private int streakThresholdLvl2 = 20;
    [SerializeField] private int streakThresholdLvl3 = 30;
    [SerializeField] private int streakThresholdLvl4 = 30;
    [SerializeField] private int streakThresholdLvl5 = 30;
    [SerializeField] private int streakThresholdLvl6 = 30;

    [SerializeField] private int streakThresholdBonusPts1 = 50;
    [SerializeField] private int streakThresholdBonusPts2 = 70;
    [SerializeField] private int streakThresholdBonusPts3 = 100;
    [SerializeField] private int streakThresholdBonusPts4 = 140;
    [SerializeField] private int streakThresholdBonusPts5 = 190;
    [SerializeField] private int streakThresholdBonusPts6 = 250;

    #endregion

    #region Team Mapping

    [Header("Team Mapping (used only in TeamBattle mode)")]
    [Tooltip("Example: Team 1 = players 1 & 3")]
    [SerializeField] private int[] team1Players = new[] { 1, 3 };

    [Tooltip("Example: Team 2 = players 2 & 4")]
    [SerializeField] private int[] team2Players = new[] { 2, 4 };

    #endregion

    #region Totals

    [Header("Totals (debug)")]
    [SerializeField] private float[] playerTotalScore = new float[MaxPlayers];

    [Header("Checkout Reward Debug")]
    [Min(0)]
    [SerializeField] private int debugProjectedCargoCount = 10;

    #endregion

    #region Context Menu Tests

    [ContextMenu("TEST - Log Projected Cargo Reward")]
    private void DebugLogProjectedCargoReward()
    {
        int cargoCount = Mathf.Max(0, debugProjectedCargoCount);
        int tier = GetProjectedStreakTier(cargoCount);
        int bonus = GetProjectedBonusPoints(cargoCount);
        int nextThreshold = GetNextStreakThreshold(cargoCount);

        Debug.Log(
            $"[CashScoreManager] Projected Cargo Reward | Cargo:{cargoCount} | " +
            $"StreakAchievable:{IsCheckoutStreakAchievable(cargoCount)} | Tier:{tier} | " +
            $"Bonus:{bonus} | NextThreshold:{nextThreshold}",
            this
        );
    }

    #endregion

    #region Checkout UI

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
        public TextMeshPro bonusPointText;
        public GameObject bonusPointUI;
        public TextMeshPro basePointText;
    }

    private Coroutine[,] subtotalPulseAnims = new Coroutine[MaxPlayers, MaxLanes];

    #endregion

    #region Session Data

    [Serializable]
    public class CheckoutSessionData
    {
        public bool isActive;
        public int laneIndex;

        [Header("Cargo Checkout")]
        public int cargoCount;
        public int checkedOutCartCount;

        public float basePoints;
        public float bonusPoints;
        public float subtotal;


        public void Reset()
        {
            isActive = false;
            laneIndex = -1;

            cargoCount = 0;
            checkedOutCartCount = 0;

            basePoints = 0f;
            bonusPoints = 0f;
            subtotal = 0f;

        }

        public void CopyFrom(CheckoutSessionData other)
        {
            if (other == null)
            {
                Reset();
                return;
            }

            isActive = other.isActive;
            laneIndex = other.laneIndex;
            cargoCount = other.cargoCount;
            checkedOutCartCount = other.checkedOutCartCount;
            basePoints = other.basePoints;
            bonusPoints = other.bonusPoints;
            subtotal = other.subtotal;
        }
    }

    private readonly CheckoutSessionData[] currentSession = new CheckoutSessionData[MaxPlayers];
    private readonly CheckoutSessionData[] lastSession = new CheckoutSessionData[MaxPlayers];

    #endregion

    #region Events

    public event Action<int, int, int> OnPlayerScoreGained;
    public event Action<int, int, int> OnTeamScoreGained;

    /// <summary>
    /// playerIndex, current cargo count, current subtotal
    /// </summary>
    public event Action<int, int, int> OnCheckoutSessionUpdated;

    #endregion

    #region Unity Lifecycle

    private int ActivePlayerCount => Mathf.Clamp(GMode.Instance ? GMode.Instance.PlayerCount() : 2, 1, MaxPlayers);

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

        for (int p = 1; p <= ActivePlayerCount; p++)
        {
            for (int lane = 1; lane <= MaxLanes; lane++)
            {
                ShowCheckoutUI(p, lane, false);
            }

            for (int laneIndex = 0; laneIndex < MaxLanes; laneIndex++)
            {
                ResetCheckoutSessionUI(p, laneIndex);
            }
        }
    }

    #endregion

    #region Checkout Session

    public void StartCheckoutSession(int playerIndex, int laneIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        CheckoutSessionData session = currentSession[playerIndex - 1];
        session.Reset();
        session.isActive = true;
        session.laneIndex = laneIndex;

        UpdateCheckoutSessionUI(playerIndex, session);
    }

    /// <summary>
    /// Registers one physical follower cart being checked out.
    ///
    /// Every CargoEntry on that cart contributes:
    /// - +1 cargo toward streak thresholds;
    /// - CargoEntry.ScoreValue toward base score.
    ///
    /// Returns the base score added by this physical cart.
    /// Empty carts register nothing and do not increment checkedOutCartCount.
    /// </summary>
    public int RegisterCargoCheckout(int playerIndex, IReadOnlyList<CargoEntry> cargoEntries)
    {
        if (!IsValidPlayer(playerIndex) || cargoEntries == null || cargoEntries.Count == 0) return 0;

        CheckoutSessionData session = EnsureActiveSession(playerIndex);

        int cargoAdded = 0;
        int scoreAdded = 0;

        for (int i = 0; i < cargoEntries.Count; i++)
        {
            CargoEntry entry = cargoEntries[i];
            if (entry == null) continue;

            cargoAdded++;
            scoreAdded += Mathf.Max(0, entry.ScoreValue);
        }

        if (cargoAdded <= 0) return 0;

        session.checkedOutCartCount++;
        session.cargoCount += cargoAdded;
        session.basePoints += scoreAdded;
        RefreshSessionReward(session);

        UpdateCheckoutSessionUI(playerIndex, session);
        OnCheckoutSessionUpdated?.Invoke(playerIndex, session.cargoCount, Mathf.RoundToInt(session.subtotal));

        return scoreAdded;
    }

    public void EndCheckoutSession(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        CheckoutSessionData session = currentSession[playerIndex - 1];
        CheckoutSessionData last = lastSession[playerIndex - 1];

        if (!session.isActive || session.cargoCount <= 0)
        {
            int laneIndex = session.laneIndex;
            session.Reset();

            if (laneIndex >= 0) ResetCheckoutSessionUI(playerIndex, laneIndex);
            return;
        }

        int gain = Mathf.RoundToInt(session.subtotal);

        playerTotalScore[playerIndex - 1] += gain;
        int newTotal = GetPlayerScore(playerIndex);

        OnPlayerScoreGained?.Invoke(playerIndex, gain, newTotal);

        if (GMode.Instance && GMode.Instance.IsTeamBattle)
        {
            int team = GetTeamIndexForPlayer(playerIndex);

            if (team != 0)
            {
                int teamTotal = GetTeamScore(team == 1 ? team1Players : team2Players);
                OnTeamScoreGained?.Invoke(team, gain, teamTotal);
            }
        }

        last.CopyFrom(session);
        last.isActive = false;

        int completedLaneIndex = session.laneIndex;
        session.Reset();

        if (completedLaneIndex >= 0) ResetCheckoutSessionUI(playerIndex, completedLaneIndex);
    }

    public CheckoutSessionData GetCurrentSessionData(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return null;
        return currentSession[playerIndex - 1];
    }

    public CheckoutSessionData GetLastSessionData(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return null;
        return lastSession[playerIndex - 1];
    }

    #endregion

    #region Projected Cargo Streak API

    /// <summary>
    /// True when this cargo count reaches at least the first configured milestone.
    /// Intended for future "streak achievable" HUD messaging.
    /// </summary>
    public bool IsCheckoutStreakAchievable(int cargoCount)
    {
        return GetProjectedStreakTier(cargoCount) > 0;
    }

    /// <summary>
    /// Number of configured milestone thresholds reached by this cargo count.
    /// Duplicate thresholds intentionally count as separate configured milestones,
    /// matching the existing milestone-bonus behavior.
    /// </summary>
    public int GetProjectedStreakTier(int cargoCount)
    {
        int tier = 0;

        if (cargoCount >= streakThresholdLvl1) tier++;
        if (cargoCount >= streakThresholdLvl2) tier++;
        if (cargoCount >= streakThresholdLvl3) tier++;
        if (cargoCount >= streakThresholdLvl4) tier++;
        if (cargoCount >= streakThresholdLvl5) tier++;
        if (cargoCount >= streakThresholdLvl6) tier++;

        return tier;
    }

    public int GetProjectedBonusPoints(int cargoCount)
    {
        return GetMilestoneBonus(Mathf.Max(0, cargoCount));
    }

    /// <summary>
    /// Returns the smallest configured threshold greater than cargoCount.
    /// Returns -1 when every configured milestone has already been reached.
    /// </summary>
    public int GetNextStreakThreshold(int cargoCount)
    {
        int next = int.MaxValue;

        TrySelectNextThreshold(streakThresholdLvl1, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl2, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl3, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl4, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl5, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl6, cargoCount, ref next);

        return next == int.MaxValue ? -1 : next;
    }

    public int GetFirstStreakThreshold()
    {
        int first = int.MaxValue;

        SelectPositiveMinimum(streakThresholdLvl1, ref first);
        SelectPositiveMinimum(streakThresholdLvl2, ref first);
        SelectPositiveMinimum(streakThresholdLvl3, ref first);
        SelectPositiveMinimum(streakThresholdLvl4, ref first);
        SelectPositiveMinimum(streakThresholdLvl5, ref first);
        SelectPositiveMinimum(streakThresholdLvl6, ref first);

        return first == int.MaxValue ? -1 : first;
    }

    private void TrySelectNextThreshold(int threshold, int cargoCount, ref int currentNext)
    {
        if (threshold > cargoCount && threshold < currentNext) currentNext = threshold;
    }

    private void SelectPositiveMinimum(int threshold, ref int currentMinimum)
    {
        if (threshold > 0 && threshold < currentMinimum) currentMinimum = threshold;
    }

    #endregion


    #region Score / Team API

    public int GetPlayerScore(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return 0;
        return Mathf.RoundToInt(playerTotalScore[playerIndex - 1]);
    }

    public int GetTeamScore(int[] teamPlayers)
    {
        int sum = 0;
        if (teamPlayers == null) return 0;

        for (int i = 0; i < teamPlayers.Length; i++)
        {
            sum += GetPlayerScore(teamPlayers[i]);
        }

        return sum;
    }

    public int GetTeamIndexForPlayer(int playerIndex)
    {
        if (team1Players != null)
        {
            for (int i = 0; i < team1Players.Length; i++)
            {
                if (team1Players[i] == playerIndex) return 1;
            }
        }

        if (team2Players != null)
        {
            for (int i = 0; i < team2Players.Length; i++)
            {
                if (team2Players[i] == playerIndex) return 2;
            }
        }

        return 0;
    }

    public void ShowCheckoutUI(int playerIndex, int laneIndex, bool show)
    {
        if (!IsValidPlayer(playerIndex)) return;
        if (laneIndex < 1 || laneIndex > MaxLanes) return;

        int index = RootIndex(playerIndex, laneIndex - 1);

        GameObject root =
            checkoutLaneUIRoots != null &&
            index >= 0 &&
            index < checkoutLaneUIRoots.Length
                ? checkoutLaneUIRoots[index]
                : null;

        if (root != null) root.SetActive(show);
    }

    #endregion

    #region Internal Reward Calculation

    private CheckoutSessionData EnsureActiveSession(int playerIndex)
    {
        CheckoutSessionData session = currentSession[playerIndex - 1];

        if (!session.isActive)
        {
            session.Reset();
            session.isActive = true;
        }

        return session;
    }

    private void RefreshSessionReward(CheckoutSessionData session)
    {
        session.bonusPoints = GetMilestoneBonus(session.cargoCount);
        session.subtotal = session.basePoints + session.bonusPoints;
    }

    private int GetMilestoneBonus(int cargoCount)
    {
        int bonus = 0;

        if (cargoCount >= streakThresholdLvl1) bonus += streakThresholdBonusPts1;
        if (cargoCount >= streakThresholdLvl2) bonus += streakThresholdBonusPts2;
        if (cargoCount >= streakThresholdLvl3) bonus += streakThresholdBonusPts3;
        if (cargoCount >= streakThresholdLvl4) bonus += streakThresholdBonusPts4;
        if (cargoCount >= streakThresholdLvl5) bonus += streakThresholdBonusPts5;
        if (cargoCount >= streakThresholdLvl6) bonus += streakThresholdBonusPts6;

        return bonus;
    }

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

    #endregion

    #region Checkout UI

    private void UpdateCheckoutSessionUI(int playerIndex, CheckoutSessionData session)
    {
        if (session == null || session.laneIndex < 0) return;

        int laneIndex = session.laneIndex;
        CheckoutLaneUI ui = GetLaneUI(playerIndex, laneIndex);

        if (ui == null) return;

        if (ui.itemsCountText != null)
        {
            ui.itemsCountText.text = session.cargoCount.ToString();
        }

        bool hasStreak = IsCheckoutStreakAchievable(session.cargoCount);

        if (ui.streakTextUI != null) ui.streakTextUI.SetActive(hasStreak);
        if (ui.bonusPointUI != null) ui.bonusPointUI.SetActive(hasStreak);

        if (ui.bonusPointText != null)
        {
            ui.bonusPointText.text = "+" + session.bonusPoints.ToString("F0");
        }

        if (ui.basePointText != null)
        {
            ui.basePointText.text = "+" + session.basePoints.ToString("F0");

            int playerArrayIndex = playerIndex - 1;

            if (subtotalPulseAnims[playerArrayIndex, laneIndex] != null)
            {
                StopCoroutine(subtotalPulseAnims[playerArrayIndex, laneIndex]);
            }

            subtotalPulseAnims[playerArrayIndex, laneIndex] = StartCoroutine(
                AnimateTextPulse(ui.basePointText.transform, 1.4f)
            );
        }
    }

    private CheckoutLaneUI GetLaneUI(int playerIndex, int laneIndex)
    {
        int index = LaneIndex(playerIndex, laneIndex);

        if (laneUIs == null || index < 0 || index >= laneUIs.Length) return null;

        return laneUIs[index];
    }

    private void ResetCheckoutSessionUI(int playerIndex, int laneIndex)
    {
        CheckoutLaneUI ui = GetLaneUI(playerIndex, laneIndex);
        if (ui == null) return;

        if (ui.itemsCountText != null) ui.itemsCountText.text = "0";
        if (ui.streakTextUI != null) ui.streakTextUI.SetActive(false);
        if (ui.basePointText != null) ui.basePointText.text = "0";
        if (ui.bonusPointUI != null) ui.bonusPointUI.SetActive(false);
        if (ui.bonusPointText != null) ui.bonusPointText.text = "0";
    }

    private IEnumerator AnimateTextPulse(Transform target, float scaleMultiplier = 1.5f, float duration = 0.3f)
    {
        if (target == null) yield break;

        Vector3 originalScale = Vector3.one;
        target.localScale = originalScale;

        float half = Mathf.Max(0.01f, duration * 0.5f);
        float time = 0f;

        while (time < half)
        {
            float t = time / half;
            target.localScale = Vector3.Lerp(originalScale, originalScale * scaleMultiplier, t);
            time += Time.deltaTime;
            yield return null;
        }

        time = 0f;

        while (time < half)
        {
            float t = time / half;
            target.localScale = Vector3.Lerp(originalScale * scaleMultiplier, originalScale, t);
            time += Time.deltaTime;
            yield return null;
        }

        target.localScale = originalScale;
    }

    private static int LaneIndex(int playerIndex, int laneIndex)
    {
        return (playerIndex - 1) * MaxLanes + laneIndex;
    }

    private static int RootIndex(int playerIndex, int laneIndex)
    {
        return (playerIndex - 1) * MaxLanes + laneIndex;
    }

    #endregion
}
