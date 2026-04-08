using System.Collections;
using UnityEngine;
using TMPro;

[System.Serializable]
public class WeightedMapEventEntry
{
    public MapEventType eventType;
    [Min(0)] public int weight = 1;
    public bool enabled = true;
}
public class MapEventManager : MonoBehaviour
{
    [Header("Sections")]
    [SerializeField] private MapSection[] sections;

    [Header("Event Config")]
    [SerializeField] private RareItemSaleConfig normalSaleConfig = new RareItemSaleConfig();
    [SerializeField] private RareItemSaleConfig rareSaleConfig = new RareItemSaleConfig();
    [SerializeField] private CartRainConfig cartRainConfig = new CartRainConfig();
    [SerializeField] private PowerupStormConfig powerupStormConfig = new PowerupStormConfig();
    [SerializeField] private ShopperRushConfig shopperRushConfig = new ShopperRushConfig();

    [Header("Event Loop Settings")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private float firstEventDelay = 30f;
    [SerializeField] private float minTimeBetweenEvents = 15f;
    [SerializeField] private float maxTimeBetweenEvents = 30f;

    [Header("Weighted Random Events")]
    [SerializeField] private WeightedMapEventEntry[] weightedEvents;
    [Header("Repetition Control")]
    [SerializeField] private bool limitConsecutiveRepeats = true;
    [SerializeField] private int maxConsecutiveSameEvent = 2;

    private MapEventType lastRandomEventType;
    private int currentSameEventStreak = 0;
    private bool hasPickedRandomEventBefore = false;

    [Header("Scripted Event Sequence (Optional)")]
    [SerializeField] private bool useScriptedSequence = false;
    [SerializeField] private MapEventType[] scriptedEvents;
    private int scriptedIndex = 0;

    [Header("UI - Rolling Text (Per Player)")]
    [SerializeField] private GameObject rollingTextRootP1;
    [SerializeField] private GameObject rollingTextRootP2;
    [SerializeField] private GameObject rollingTextRootP3;
    [SerializeField] private GameObject rollingTextRootP4;
    [SerializeField] private TextMeshProUGUI rollingTextP1;
    [SerializeField] private TextMeshProUGUI rollingTextP2;
    [SerializeField] private TextMeshProUGUI rollingTextP3;
    [SerializeField] private TextMeshProUGUI rollingTextP4;

    private Coroutine eventLoopRoutine;
    private bool isRunningEvent = false;

    private void Start()
    {
        // Auto-find sections if not assigned
        if (sections == null || sections.Length == 0)
        {
            sections = FindObjectsOfType<MapSection>();
        }

        SetAllSectionsNormal();

        if (autoStart)
        {
            eventLoopRoutine = StartCoroutine(EventLoop());
        }
    }

    // ------------------------------------------------------
    // MAIN EVENT LOOP
    // ------------------------------------------------------
    private IEnumerator EventLoop()
    {
        // Hard delay before the very first event
        if (firstEventDelay > 0f)
            yield return new WaitForSeconds(firstEventDelay);

        while (true)
        { 
            MapEventType nextType = GetNextEventType();

            switch (nextType)
            {
                case MapEventType.RareItemSale:
                    yield return StartCoroutine(RunRareItemSaleEvent());
                    break;

                case MapEventType.CartRain:
                    yield return StartCoroutine(RunCartRainEvent());
                    break;

                case MapEventType.PowerupStorm:
                    yield return StartCoroutine(RunPowerupStormEvent());
                    break;

                case MapEventType.ShopperRush:
                    yield return StartCoroutine(RunShopperRushEvent());
                    break;

                case MapEventType.NormalItemSale:                      
                    yield return StartCoroutine(RunNormalItemSaleEvent());
                    break;
            }
            // Wait some time before next event
            float wait = Random.Range(minTimeBetweenEvents, maxTimeBetweenEvents);
            yield return new WaitForSeconds(wait);
        }
    }

    private MapEventType GetNextEventType()
    {
        // Scripted sequence has priority if enabled
        if (useScriptedSequence && scriptedEvents != null && scriptedEvents.Length > 0)
        {
            var t = scriptedEvents[scriptedIndex];

            scriptedIndex++;

            if (scriptedIndex >= scriptedEvents.Length)
            {
                useScriptedSequence = false;
            }

            return t;
        }

        MapEventType selected = GetWeightedRandomEventTypeWithRepeatLimit();
        RegisterRandomEventPick(selected);
        return selected;
    }

    // ------------------------------------------------------
    // NORMAL ITEM SALE
    // ------------------------------------------------------
    private IEnumerator RunNormalItemSaleEvent()
    {
        if (isRunningEvent || sections == null || sections.Length == 0)
            yield break;

        isRunningEvent = true;

        MapSection section = PickRandomSectionWithGenerator();
        if (section == null)
        {
            isRunningEvent = false;
            yield break;
        }

        yield return StartCoroutine(WarningPhase(
            section,
            normalSaleConfig.warningDuration,
            "Section " + section.sectionId + "\nClearance sale in "
        ));

        section.SetActive();

        ShowRollingText("Section " + section.sectionId + "\nClearance sale! ");

        int total = normalSaleConfig.GetRandomTotal();
        float interval = normalSaleConfig.spawnInterval;
        int perSpawn = normalSaleConfig.itemsPerSpawn;

        section.eventGenerator.StartNormalItemEvent(total, interval, perSpawn);

        float activeDuration = ComputeDuration(total, interval, perSpawn);
        yield return new WaitForSeconds(activeDuration + 0.5f);

        section.eventGenerator.StopEvent();
        section.SetNormal();
        HideRollingText();

        isRunningEvent = false;
    }
    // ------------------------------------------------------
    // RARE ITEM SALE
    // ------------------------------------------------------
    private IEnumerator RunRareItemSaleEvent()
    {
        if (isRunningEvent || sections == null || sections.Length == 0)
            yield break;

        isRunningEvent = true;

        MapSection section = PickRandomSectionWithGenerator();
        if (section == null)
        {
            isRunningEvent = false;
            yield break;
        }

        yield return StartCoroutine(WarningPhase(
            section,
            rareSaleConfig.warningDuration,
            "Section " + section.sectionId + "\ndrop extremely valuable items in "
        ));

        section.SetActive();
        ShowRollingText("Section " + section.sectionId + "\nValuable items incoming!!!");

        int total = rareSaleConfig.GetRandomTotal();
        float interval = rareSaleConfig.spawnInterval;
        int perSpawn = rareSaleConfig.itemsPerSpawn;

        section.eventGenerator.StartRareItemEvent(total, interval, perSpawn);

        float activeDuration = ComputeDuration(total, interval, perSpawn);
        yield return new WaitForSeconds(activeDuration + 0.5f);

        section.eventGenerator.StopEvent();
        section.SetNormal();
        HideRollingText();

        isRunningEvent = false;
    }

    // ------------------------------------------------------
    // CART RAIN
    // ------------------------------------------------------
    private IEnumerator RunCartRainEvent()
    {
        if (isRunningEvent || sections == null || sections.Length == 0)
            yield break;

        isRunningEvent = true;

        MapSection section = PickRandomSectionWithGenerator();
        if (section == null)
        {
            isRunningEvent = false;
            yield break;
        }

        yield return StartCoroutine(WarningPhase(
            section,
            cartRainConfig.warningDuration,
            "Section " + section.sectionId + "\nrestock empty carts in "
        ));

        section.SetActive();
        ShowRollingText("Section " + section.sectionId + "\nEmpty carts restocking!!!");

        int total = cartRainConfig.GetRandomTotal();
        float interval = cartRainConfig.spawnInterval;
        int perSpawn = cartRainConfig.itemsPerSpawn;

        section.eventGenerator.StartEmptyCartEvent(total, interval, perSpawn);

        float activeDuration = ComputeDuration(total, interval, perSpawn);
        yield return new WaitForSeconds(activeDuration + 0.5f);

        section.eventGenerator.StopEvent();
        section.SetNormal();
        HideRollingText();

        isRunningEvent = false;
    }

    // ------------------------------------------------------
    // POWERUP STORM
    // ------------------------------------------------------
    private IEnumerator RunPowerupStormEvent()
    {
        if (isRunningEvent || sections == null || sections.Length == 0)
            yield break;

        isRunningEvent = true;

        MapSection section = PickRandomSectionWithGenerator();
        if (section == null)
        {
            isRunningEvent = false;
            yield break;
        }

        yield return StartCoroutine(WarningPhase(
            section,
            powerupStormConfig.warningDuration,
            "Section " + section.sectionId + "\ndrop deadly powerups in "
        ));

        section.SetActive();
        ShowRollingText("Section " + section.sectionId + "\nDeadly powerups dropping!!!");

        int total = powerupStormConfig.GetRandomTotal();
        float interval = powerupStormConfig.spawnInterval;
        int perSpawn = powerupStormConfig.itemsPerSpawn;

        section.eventGenerator.StartPowerupEvent(total, interval, perSpawn);

        float activeDuration = ComputeDuration(total, interval, perSpawn);
        yield return new WaitForSeconds(activeDuration + 0.5f);

        section.eventGenerator.StopEvent();
        section.SetNormal();
        HideRollingText();

        isRunningEvent = false;
    }

    // ------------------------------------------------------
    // SHOPPER RUSH
    // ------------------------------------------------------
    private IEnumerator RunShopperRushEvent()
    {
        if (isRunningEvent || sections == null || sections.Length == 0)
            yield break;

        isRunningEvent = true;

        MapSection section = PickRandomSectionWithGenerator();
        if (section == null)
        {
            isRunningEvent = false;
            yield break;
        }

        yield return StartCoroutine(WarningPhase(
            section,
            shopperRushConfig.warningDuration,
            "Section " + section.sectionId + "\nwill be filled by Shoppers in "
        ));

        section.SetActive();
        ShowRollingText("Section " + section.sectionId + "\nShoppers breaking in!!!");

        int total = shopperRushConfig.GetRandomTotal();
        float interval = shopperRushConfig.spawnInterval;
        int perSpawn = shopperRushConfig.shoppersPerSpawn;

        section.eventGenerator.StartShopperRushEvent(total, interval, perSpawn);

        float activeDuration = ComputeDuration(total, interval, perSpawn);
        yield return new WaitForSeconds(activeDuration + 0.5f);

        // We DON'T necessarily despawn shoppers; they just stay as chaos.
        section.eventGenerator.StopEvent();
        section.SetNormal();
        HideRollingText();

        isRunningEvent = false;
    }
    // ------------------------------------------------------
    // COMMON HELPERS
    // ------------------------------------------------------
    private MapEventType GetWeightedRandomEventTypeWithRepeatLimit()
    {
        if (weightedEvents == null || weightedEvents.Length == 0)
        {
            Debug.LogWarning("[MapEventManager] No weighted events configured. Falling back to RareItemSale.");
            return MapEventType.RareItemSale;
        }

        int totalWeight = 0;

        for (int i = 0; i < weightedEvents.Length; i++)
        {
            var entry = weightedEvents[i];
            if (entry == null || !entry.enabled || entry.weight <= 0)
                continue;

            if (WouldExceedRepeatLimit(entry.eventType))
                continue;

            totalWeight += entry.weight;
        }

        // Fallback: if repeat limit filtered everything out, ignore the limit once
        if (totalWeight <= 0)
        {
            totalWeight = 0;

            for (int i = 0; i < weightedEvents.Length; i++)
            {
                var entry = weightedEvents[i];
                if (entry == null || !entry.enabled || entry.weight <= 0)
                    continue;

                totalWeight += entry.weight;
            }

            if (totalWeight <= 0)
            {
                Debug.LogWarning("[MapEventManager] All weighted events are disabled or have 0 weight. Falling back to RareItemSale.");
                return MapEventType.RareItemSale;
            }

            Debug.LogWarning("[MapEventManager] Repeat limit filtered out all events. Ignoring repeat limit for this roll.");
        }

        int roll = Random.Range(0, totalWeight);
        int running = 0;

        for (int i = 0; i < weightedEvents.Length; i++)
        {
            var entry = weightedEvents[i];
            if (entry == null || !entry.enabled || entry.weight <= 0)
                continue;

            if (totalWeight > 0 && !CanUseEntryForCurrentRoll(entry.eventType, totalWeight))
                continue;

            running += entry.weight;

            if (roll < running)
                return entry.eventType;
        }

        Debug.LogWarning("[MapEventManager] Weighted selection failed unexpectedly. Falling back to RareItemSale.");
        return MapEventType.RareItemSale;
    }

    private bool WouldExceedRepeatLimit(MapEventType candidate)
    {
        if (!limitConsecutiveRepeats)
            return false;

        if (!hasPickedRandomEventBefore)
            return false;

        if (candidate != lastRandomEventType)
            return false;

        return currentSameEventStreak >= maxConsecutiveSameEvent;
    }

    private bool CanUseEntryForCurrentRoll(MapEventType candidate, int filteredTotalWeight)
    {
        // If at least one valid filtered option exists, enforce the repeat rule
        bool filteredModeActive = false;

        for (int i = 0; i < weightedEvents.Length; i++)
        {
            var entry = weightedEvents[i];
            if (entry == null || !entry.enabled || entry.weight <= 0)
                continue;

            if (!WouldExceedRepeatLimit(entry.eventType))
            {
                filteredModeActive = true;
                break;
            }
        }

        if (!filteredModeActive)
            return true;

        return !WouldExceedRepeatLimit(candidate);
    }

    private void RegisterRandomEventPick(MapEventType picked)
    {
        if (!hasPickedRandomEventBefore)
        {
            hasPickedRandomEventBefore = true;
            lastRandomEventType = picked;
            currentSameEventStreak = 1;
            return;
        }

        if (picked == lastRandomEventType)
        {
            currentSameEventStreak++;
        }
        else
        {
            lastRandomEventType = picked;
            currentSameEventStreak = 1;
        }
    }
    private MapSection PickRandomSectionWithGenerator()
    {
        if (sections == null || sections.Length == 0) return null;

        // collect valid sections with eventGenerator
        var valid = new System.Collections.Generic.List<MapSection>();
        foreach (var s in sections)
        {
            if (s != null && s.eventGenerator != null)
                valid.Add(s);
        }

        if (valid.Count == 0) return null;

        int idx = Random.Range(0, valid.Count);
        return valid[idx];
    }

    private IEnumerator WarningPhase(MapSection section, float duration, string prefix)
    {
        section.SetWarning();

        float remaining = duration;

        while (remaining > 0f)
        {
            int seconds = Mathf.CeilToInt(remaining);
            string msg = $"{prefix}{seconds}!";

            ShowRollingText(msg);

            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private float ComputeDuration(int total, float interval, int perSpawn)
    {
        int ticks = Mathf.CeilToInt((float)total / Mathf.Max(1, perSpawn));
        return ticks * interval;
    }

    private void ShowRollingText(string msg)
    {
        if (rollingTextRootP1 != null) rollingTextRootP1.SetActive(true);
        if (rollingTextRootP2 != null) rollingTextRootP2.SetActive(true);
        if (rollingTextRootP3 != null) rollingTextRootP3.SetActive(true);
        if (rollingTextRootP4 != null) rollingTextRootP4.SetActive(true);


        if (rollingTextP1 != null) rollingTextP1.text = msg;
        if (rollingTextP2 != null) rollingTextP2.text = msg;
        if (rollingTextP3 != null) rollingTextP3.text = msg;
        if (rollingTextP4 != null) rollingTextP4.text = msg;
    }

    private void HideRollingText()
    {
        if (rollingTextRootP1 != null) rollingTextRootP1.SetActive(false);
        if (rollingTextRootP2 != null) rollingTextRootP2.SetActive(false);
        if (rollingTextRootP3 != null) rollingTextRootP3.SetActive(false);
        if (rollingTextRootP4 != null) rollingTextRootP4.SetActive(false);
    }

    private void SetAllSectionsNormal()
    {
        if (sections == null) return;

        foreach (var s in sections)
        {
            if (s != null)
                s.SetNormal();
        }
    }

}