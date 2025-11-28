using System.Collections;
using UnityEngine;

public class EventItemGenerator : MonoBehaviour
{
    [Header("Spawn Box Boundary Settings")]
    [SerializeField] private float boxWidth = 40f;
    [SerializeField] private float boxLength = 30f;
    [SerializeField] private float boxHeight = 30f;
    [SerializeField] private float yOffset = 20f;         // spawn height before raycast
    [SerializeField] private LayerMask groundLayer;

    [Header("Event Prefabs")]
    [SerializeField] private GameObject normalItemPrefab;
    [SerializeField] private GameObject rareItemPrefab;
    [SerializeField] private GameObject emptyCartPrefab;
    [SerializeField] private GameObject powerupPrefab;
    [Header("AI Shopper Variants")]
    [SerializeField] private GameObject[] aiShopperPrefabs;

    private Coroutine spawnRoutine;
    [Header("Poor and Temp fix on prefab scale issue")]
    [SerializeField] private SnakeCartManager snakeCartManager1;
    [SerializeField] private SnakeCartManager snakeCartManager2;
    [SerializeField] private bool applyPrefabScaleFix = false;

    // -------------------------------------------------------
    // PUBLIC API  (called by EventManager)
    // -------------------------------------------------------
    public void StartNormalItemEvent(int total, float interval, int itemsPerSpawn)
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnEventCoroutine(normalItemPrefab, total, interval, itemsPerSpawn));
    }
    public void StartRareItemEvent(int total, float interval, int itemsPerSpawn)
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnEventCoroutine(rareItemPrefab, total, interval, itemsPerSpawn));
    }

    public void StartEmptyCartEvent(int total, float interval, int itemsPerSpawn)
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnCartEventCoroutine(emptyCartPrefab, total, interval, itemsPerSpawn));
    }

    public void StartPowerupEvent(int total, float interval, int itemsPerSpawn)
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnEventCoroutine(powerupPrefab, total, interval, itemsPerSpawn));
    }

    public void StartShopperRushEvent(int total, float interval, int itemsPerSpawn)
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnShopperRushCoroutine(total, interval, itemsPerSpawn));
    }

    public void StopEvent()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = null;
    }

    // -------------------------------------------------------
    // INTERNAL: Core spawning logic for ALL event types
    // -------------------------------------------------------

    private IEnumerator SpawnEventCoroutine(GameObject prefab, int total, float interval, int itemsPerSpawn)
    {
        if (prefab == null) yield break;
        if (total <= 0 || interval <= 0 || itemsPerSpawn <= 0) yield break;

        int spawned = 0;

        while (spawned < total)
        {
            int batch = Mathf.Min(itemsPerSpawn, total - spawned);
            SpawnSpecificPrefab(prefab, batch);
            spawned += batch;

            yield return new WaitForSeconds(interval);
        }

        spawnRoutine = null; // finished
    }
    private IEnumerator SpawnCartEventCoroutine(GameObject prefab, int total, float interval, int itemsPerSpawn)
    {
        if (prefab == null) yield break;
        if (total <= 0 || interval <= 0 || itemsPerSpawn <= 0) yield break;

        int spawned = 0;

        while (spawned < total)
        {
            int batch = Mathf.Min(itemsPerSpawn, total - spawned);
            SpawnCartPrefab(prefab, batch);
            spawned += batch;

            yield return new WaitForSeconds(interval);
        }

        spawnRoutine = null; // finished
    }
    private void SpawnSpecificPrefab(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetValidSpawnPosition();
            if (pos != Vector3.zero)
            {
                Instantiate(prefab, pos + new Vector3(0, yOffset, 0), prefab.transform.rotation);
            }
        }
    }
    private void SpawnCartPrefab(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetValidSpawnPosition();
            if (pos != Vector3.zero)
            {
                GameObject spawned = Instantiate(prefab, pos + new Vector3(0, yOffset, 0), prefab.transform.rotation);
                if (applyPrefabScaleFix && (snakeCartManager1.needScaleup || snakeCartManager2.needScaleup))
                {
                    spawned.transform.localScale = new Vector3(5f, 5f, 5f);
                }
            }
        }
    }
    private IEnumerator SpawnShopperRushCoroutine(int total, float interval, int itemsPerSpawn)
    {
        if (aiShopperPrefabs == null || aiShopperPrefabs.Length == 0) yield break;
        if (total <= 0 || interval <= 0 || itemsPerSpawn <= 0) yield break;

        int spawned = 0;

        while (spawned < total)
        {
            int batch = Mathf.Min(itemsPerSpawn, total - spawned);
            SpawnRandomAIShoppers(batch);
            spawned += batch;

            yield return new WaitForSeconds(interval);
        }

        spawnRoutine = null;
    }

    private void SpawnRandomAIShoppers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (aiShopperPrefabs == null || aiShopperPrefabs.Length == 0)
                return;

            GameObject prefab = aiShopperPrefabs[Random.Range(0, aiShopperPrefabs.Length)];
            Vector3 pos = GetValidSpawnPosition();
            if (pos != Vector3.zero)
            {
                // AI: spawn right on ground (tiny lift if you like)
                GameObject ai = Instantiate(prefab, pos + Vector3.up * 0.5f, prefab.transform.rotation);
                ai.SetActive(true);
                // Reset the AI logic so they start clean for the event
                var aiBehaviour = ai.GetComponent<AIShopperBehaviour>();
                if (aiBehaviour != null)
                {
                    aiBehaviour.ResetState();
                }
            }
        }
    }


    // -------------------------------------------------------
    // Ground-raycast placement within section box
    // -------------------------------------------------------

    private Vector3 GetValidSpawnPosition()
    {
        int maxRetries = 30;
        int attempts = 0;

        while (attempts < maxRetries)
        {
            float halfWidth = boxWidth * 0.5f;
            float halfLength = boxLength * 0.5f;

            float xOffset = Random.Range(-halfWidth, halfWidth);
            float zOffset = Random.Range(-halfLength, halfLength);

            Vector3 origin = transform.position + new Vector3(xOffset, yOffset, zOffset);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Infinity))
            {
                if (((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                {
                    return hit.point;
                }
            }

            attempts++;
        }

        return Vector3.zero; // failed
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(boxWidth, boxHeight, boxLength));
    }
}
