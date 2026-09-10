using System.Collections;
using UnityEngine;

/// <summary>
/// Exact-budget prototype loot spawner for one arena zone.
///
/// The phase decides the total budget and duration.
/// This component decides only WHERE the loot appears and WHICH prototype
/// GroceryLootDefinition is assigned.
///
/// Spawn points are reused across pulses intentionally, allowing a small number
/// of authored drop locations to support long events without random box spawning.
/// </summary>
[DisallowMultipleComponent]
public class ZoneLootSpawner : MonoBehaviour
{
    [Header("Prototype Pickup")]
    [Tooltip("Common prototype world pickup prefab containing GroceryLootPickup.")]
    [SerializeField] private GameObject lootPickupPrefab;

    [Tooltip("Prototype loot definitions used by this zone. With Randomize disabled they cycle deterministically.")]
    [SerializeField] private GroceryLootDefinition[] lootDefinitions;

    [SerializeField] private bool randomizeDefinitionSelection;

    [Header("Authored Loot Drop Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Optional world-space lift from each authored drop point.")]
    [SerializeField] private float spawnHeightOffset = 0.25f;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isSpawning;
    [SerializeField] private int lastRequestedBudget;
    [SerializeField] private int lastSpawnedCount;

    private int spawnPointCursor;
    private int definitionCursor;

    public bool IsSpawning => isSpawning;
    public int LastRequestedBudget => lastRequestedBudget;
    public int LastSpawnedCount => lastSpawnedCount;

    public IEnumerator SpawnExactBudget(int totalCount, float activeDuration, int batchSize)
    {
        if (isSpawning)
        {
            Debug.LogWarning("[ZoneLootSpawner] A loot event is already running.", this);
            yield break;
        }

        lastRequestedBudget = Mathf.Max(0, totalCount);
        lastSpawnedCount = 0;
        spawnPointCursor = 0;
        definitionCursor = 0;

        if (lastRequestedBudget <= 0) yield break;

        if (lootPickupPrefab == null)
        {
            Debug.LogError("[ZoneLootSpawner] Loot Pickup Prefab is missing.", this);
            yield break;
        }

        if (CountValidSpawnPoints() <= 0)
        {
            Debug.LogError("[ZoneLootSpawner] No valid authored loot spawn points are assigned.", this);
            yield break;
        }

        batchSize = Mathf.Max(1, batchSize);
        int pulseCount = Mathf.CeilToInt((float)lastRequestedBudget / batchSize);
        float pulseInterval = activeDuration > 0f ? activeDuration / pulseCount : 0f;

        isSpawning = true;

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            int countThisPulse = Mathf.Min(batchSize, lastRequestedBudget - lastSpawnedCount);

            for (int i = 0; i < countThisPulse; i++)
            {
                if (SpawnOneLoot()) lastSpawnedCount++;
            }

            if (pulseInterval > 0f) yield return new WaitForSeconds(pulseInterval);
        }

        isSpawning = false;
    }

    public void CancelSpawning()
    {
        isSpawning = false;
    }

    private bool SpawnOneLoot()
    {
        Transform spawnPoint = GetNextSpawnPoint();
        if (spawnPoint == null) return false;

        Vector3 position = spawnPoint.position + Vector3.up * spawnHeightOffset;
        GameObject instance = Instantiate(lootPickupPrefab, position, spawnPoint.rotation);

        GroceryLootDefinition definition = GetNextLootDefinition();
        GroceryLootPickup pickup = instance.GetComponent<GroceryLootPickup>();

        if (pickup == null)
        {
            Debug.LogError("[ZoneLootSpawner] Spawned Loot Pickup Prefab has no GroceryLootPickup on its root.", instance);
            Destroy(instance);
            return false;
        }

        // If no pool is assigned, preserve the definition already authored on
        // the prototype prefab. This is useful for one-definition balance tests.
        if (definition != null) pickup.Initialize(definition);

        return true;
    }

    private Transform GetNextSpawnPoint()
    {
        int validCount = CountValidSpawnPoints();
        if (validCount <= 0) return null;

        int targetValidIndex = spawnPointCursor % validCount;
        spawnPointCursor++;

        int currentValidIndex = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;

            if (currentValidIndex == targetValidIndex) return spawnPoints[i];
            currentValidIndex++;
        }

        return null;
    }

    private GroceryLootDefinition GetNextLootDefinition()
    {
        int validCount = CountValidDefinitions();
        if (validCount <= 0) return null;

        int targetValidIndex;

        if (randomizeDefinitionSelection)
        {
            targetValidIndex = Random.Range(0, validCount);
        }
        else
        {
            targetValidIndex = definitionCursor % validCount;
            definitionCursor++;
        }

        int currentValidIndex = 0;

        for (int i = 0; i < lootDefinitions.Length; i++)
        {
            if (lootDefinitions[i] == null) continue;

            if (currentValidIndex == targetValidIndex) return lootDefinitions[i];
            currentValidIndex++;
        }

        return null;
    }

    private int CountValidSpawnPoints()
    {
        if (spawnPoints == null) return 0;

        int count = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null) count++;
        }

        return count;
    }

    private int CountValidDefinitions()
    {
        if (lootDefinitions == null) return 0;

        int count = 0;

        for (int i = 0; i < lootDefinitions.Length; i++)
        {
            if (lootDefinitions[i] != null) count++;
        }

        return count;
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[i];
            if (point == null) continue;

            Vector3 position = point.position + Vector3.up * spawnHeightOffset;
            Gizmos.DrawWireSphere(position, 0.3f);
        }
    }
}
