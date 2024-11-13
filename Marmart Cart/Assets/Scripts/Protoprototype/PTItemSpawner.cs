using UnityEngine;

public class PTItemSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float radius = 10f;             // Radius within which items will spawn
    [SerializeField] private float spawnFrequency = 2f;      // Frequency (seconds) for item spawn
    [SerializeField] private float minDistanceFromCenter = 2f; // Minimum distance from the center to spawn items

    [Header("Item Prefabs")]
    [SerializeField] private GameObject itemPrefab1;         // First item prefab
    [SerializeField] private GameObject itemPrefab2;         // Second item prefab

    [Header("Spawn Probabilities")]
    [Range(0, 100)]
    [SerializeField] private int item1Probability = 84;      // Probability for itemPrefab1 (0-100)

    private float nextSpawnTime;

    void Update()
    {
        // Spawn items at intervals based on spawnFrequency
        if (Time.time >= nextSpawnTime)
        {
            SpawnItem();
            nextSpawnTime = Time.time + spawnFrequency;
        }
    }

    private void SpawnItem()
    {
        // Generate a random point within the radius, ensuring it’s beyond minDistanceFromCenter
        Vector2 randomPoint;
        do
        {
            randomPoint = Random.insideUnitCircle * radius;
        } while (randomPoint.magnitude < minDistanceFromCenter);

        Vector3 spawnPosition = new Vector3(randomPoint.x, 0, randomPoint.y) + transform.position;

        // Choose which item to spawn based on the probability
        GameObject itemToSpawn = (Random.Range(0, 100) < item1Probability) ? itemPrefab1 : itemPrefab2;

        if (itemToSpawn != null)
        {
            Instantiate(itemToSpawn, spawnPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogError("One or both item prefabs are missing. Please assign them in the Inspector.");
        }
    }

    private void OnDrawGizmos()
    {
        // Set the color for the radius and min distance circles
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius); // Outer radius

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minDistanceFromCenter); // Minimum spawn distance
    }
}
