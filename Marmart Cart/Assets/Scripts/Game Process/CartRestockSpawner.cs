using System.Collections;
using UnityEngine;

/// <summary>
/// Exact-budget empty-cart restock spawner for the central arena.
///
/// This intentionally does NOT use random box spawning. The designer authors
/// every possible cart spawn location using ordered Transform spawn points.
/// One restock session never reuses a spawn point, so requested cart count is
/// easy to reason about during balance tests.
/// </summary>
[DisallowMultipleComponent]
public class CartRestockSpawner : MonoBehaviour
{
    [Header("Cart")]
    [SerializeField] private GameObject emptyCartPrefab;
    [SerializeField] private Transform spawnedCartParent;

    [Header("Authored Center Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Optional world-space lift from each authored point.")]
    [SerializeField] private float spawnHeightOffset;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isSpawning;
    [SerializeField] private int lastRequestedBudget;
    [SerializeField] private int lastSpawnedCount;

    public bool IsSpawning => isSpawning;
    public int LastRequestedBudget => lastRequestedBudget;
    public int LastSpawnedCount => lastSpawnedCount;

    public IEnumerator SpawnExactBudget(int totalCount, float distributionDuration, int batchSize)
    {
        if (isSpawning)
        {
            Debug.LogWarning("[CartRestockSpawner] A restock is already running.", this);
            yield break;
        }

        lastRequestedBudget = Mathf.Max(0, totalCount);
        lastSpawnedCount = 0;

        if (lastRequestedBudget <= 0) yield break;

        if (emptyCartPrefab == null)
        {
            Debug.LogError("[CartRestockSpawner] Empty Cart Prefab is missing.", this);
            yield break;
        }

        int validPointCount = CountValidSpawnPoints();

        if (validPointCount < lastRequestedBudget)
        {
            Debug.LogError(
                $"[CartRestockSpawner] Requested {lastRequestedBudget} carts but only {validPointCount} valid authored spawn points exist. " +
                "Restock is capped to the authored point count so carts never overlap at reused points.",
                this
            );
        }

        int actualBudget = Mathf.Min(lastRequestedBudget, validPointCount);
        if (actualBudget <= 0) yield break;

        batchSize = Mathf.Max(1, batchSize);
        int pulseCount = Mathf.CeilToInt((float)actualBudget / batchSize);
        float pulseInterval = distributionDuration > 0f ? distributionDuration / pulseCount : 0f;

        isSpawning = true;
        int pointCursor = 0;

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            int countThisPulse = Mathf.Min(batchSize, actualBudget - lastSpawnedCount);

            for (int i = 0; i < countThisPulse; i++)
            {
                Transform spawnPoint = GetValidSpawnPointByOrder(pointCursor++);
                if (spawnPoint == null) continue;

                Vector3 position = spawnPoint.position + Vector3.up * spawnHeightOffset;
                Instantiate(emptyCartPrefab, position, spawnPoint.rotation, spawnedCartParent);
                lastSpawnedCount++;
            }

            if (pulseInterval > 0f) yield return new WaitForSeconds(pulseInterval);
        }

        isSpawning = false;
    }

    public void CancelSpawning()
    {
        isSpawning = false;
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

    private Transform GetValidSpawnPointByOrder(int validIndex)
    {
        if (spawnPoints == null || validIndex < 0) return null;

        int currentValidIndex = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;

            if (currentValidIndex == validIndex) return spawnPoints[i];
            currentValidIndex++;
        }

        return null;
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[i];
            if (point == null) continue;

            Vector3 position = point.position + Vector3.up * spawnHeightOffset;
            Gizmos.DrawWireCube(position, Vector3.one * 0.6f);
            Gizmos.DrawLine(position, position + point.forward);
        }
    }
}
