using System.Collections;
using UnityEngine;
using TMPro;


public class MapEventManager : MonoBehaviour
{
    [Header("Sections")]
    [SerializeField] private MapSection[] sections;

    [Header("Rare Item Sale Config")]
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

    [Header("Scripted Event Sequence (Optional)")]
    [SerializeField] private bool useScriptedSequence = false;
    [SerializeField] private MapEventType[] scriptedEvents;
    private int scriptedIndex = 0;

    [Header("UI - Rolling Text (Per Player)")]
    [SerializeField] private GameObject rollingTextRootP1;
    [SerializeField] private GameObject rollingTextRootP2;
    [SerializeField] private TextMeshProUGUI rollingTextP1;
    [SerializeField] private TextMeshProUGUI rollingTextP2;

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
        // If scripted list has events AND we are still using it
        if (useScriptedSequence && scriptedEvents != null && scriptedEvents.Length > 0)
        {
            // Get the current scripted event
            var t = scriptedEvents[scriptedIndex];

            scriptedIndex++;

            // If we've used ALL scripted events, disable scripted mode
            if (scriptedIndex >= scriptedEvents.Length)
            {
                useScriptedSequence = false;
            }

            return t;
        }
        // Simple random between all four types for now
        int v = Random.Range(0, 5);
        return (MapEventType)v;
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
            $"Section {section.sectionId}: Clearance Sale in "
        ));

        section.SetActive();

        ShowRollingText($"Section {section.sectionId}: Clearance Sale happening now!");

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
            "Section " + section.sectionId + ": Flash Sale starts in "
        ));

        section.SetActive();
        ShowRollingText($"Section {section.sectionId}: Flash Sale happening now!");

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
            "Section " + section.sectionId + ": Cart Restock in "
        ));

        section.SetActive();
        ShowRollingText($"Section {section.sectionId}: Cart Restock happening now!");

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
            "Section " + section.sectionId + ": Special Delivery in "
        ));

        section.SetActive();
        ShowRollingText($"Section {section.sectionId}: Special Delivery happening now!");

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
            "Section " + section.sectionId + ": Shoppers Breaking in "
        ));

        section.SetActive();
        ShowRollingText($"Section {section.sectionId}: Shoppers breaking in now!");

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

        if (rollingTextP1 != null) rollingTextP1.text = msg;
        if (rollingTextP2 != null) rollingTextP2.text = msg;
    }

    private void HideRollingText()
    {
        if (rollingTextRootP1 != null) rollingTextRootP1.SetActive(false);
        if (rollingTextRootP2 != null) rollingTextRootP2.SetActive(false);
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