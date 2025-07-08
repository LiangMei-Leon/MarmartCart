using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PitsManager : MonoBehaviour
{
    [Header("Phase Durations")]
    [SerializeField] private float phase1Duration = 60f;
    [SerializeField] private float phase2Duration = 60f;

    [Header("Phase Settings")]
    [SerializeField] private float phase1Interval = 12f;
    [SerializeField] private float phase1DurationActive = 8f;
    [SerializeField] private int phase1PitsPerCycle = 1;

    [SerializeField] private float phase2Interval = 8f;
    [SerializeField] private float phase2DurationActive = 10f;
    [SerializeField] private int phase2PitsPerCycle = 2;

    [SerializeField] private float phase3Interval = 5f;
    [SerializeField] private float phase3DurationActive = 12f;
    [SerializeField] private int phase3PitsPerCycle = 3;

    private List<CheckOutManager> allPits = new List<CheckOutManager>();
    private float elapsedGameTime = 0f;
    private float nextToggleTime = 0f;
    private int currentPhase = 0;

    private float interval;
    private float activeDuration;
    private int pitsPerCycle;

    void Start()
    {
        GameObject[] pitObjects = GameObject.FindGameObjectsWithTag("CheckOutStation");
        foreach (var pit in pitObjects)
        {
            var manager = pit.GetComponentInChildren<CheckOutManager>();
            if (manager != null)
            {
                allPits.Add(manager);
                manager.DisableStation(); // start with all pits disabled
            }
        }

        UpdatePhase();
        nextToggleTime = Time.time + interval;
    }

    void Update()
    {
        elapsedGameTime += Time.deltaTime;
        UpdatePhase();

        if (Time.time >= nextToggleTime)
        {
            ActivateRandomPits();
            nextToggleTime = Time.time + interval;
        }
    }

    void UpdatePhase()
    {
        if (elapsedGameTime < phase1Duration && currentPhase != 1)
        {
            currentPhase = 1;
            interval = phase1Interval;
            activeDuration = phase1DurationActive;
            pitsPerCycle = phase1PitsPerCycle;
        }
        else if (elapsedGameTime < phase1Duration + phase2Duration && currentPhase != 2)
        {
            currentPhase = 2;
            interval = phase2Interval;
            activeDuration = phase2DurationActive;
            pitsPerCycle = phase2PitsPerCycle;
        }
        else if (elapsedGameTime >= phase1Duration + phase2Duration && currentPhase != 3)
        {
            currentPhase = 3;
            interval = phase3Interval;
            activeDuration = phase3DurationActive;
            pitsPerCycle = phase3PitsPerCycle;
        }
    }

    void ActivateRandomPits()
    {
        List<CheckOutManager> available = new List<CheckOutManager>();
        foreach (var pit in allPits)
        {
            if (!pit.gameObject.activeInHierarchy) continue;
            if (!pit.enabled) continue;
            if (!pit.gameObject.activeSelf) continue;

            if (!pit.IsStationAvailable()) // Add a getter if needed
                available.Add(pit);
        }

        int count = Mathf.Min(pitsPerCycle, available.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, available.Count);
            var chosenPit = available[index];
            available.RemoveAt(index);

            chosenPit.EnableStation();
            StartCoroutine(DisableAfterTime(chosenPit, activeDuration));
        }
    }

    IEnumerator DisableAfterTime(CheckOutManager pit, float delay)
    {
        yield return new WaitForSeconds(delay);
        pit.DisableStation();
    }
}
