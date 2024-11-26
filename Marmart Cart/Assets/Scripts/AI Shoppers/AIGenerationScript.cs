using UnityEngine;

public class AIGenerationScript : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnCenter;

    [Header("Spawn Area Settings")]
    [SerializeField] private float radius = 50f; // Radius for AI spawn around the player
    [SerializeField] private float minDistanceFromCenter = 20f; // Minimum distance from the center to spawn AIs
    [SerializeField] private LayerMask groundLayer; // Layer mask for ground detection

    [Header("Phase 1 Settings")]
    [SerializeField] private float phase1Duration = 60f;
    [SerializeField] private float phase1SpawnInterval = 6f;
    [SerializeField] private int phase1AIsPerWave = 3;

    [Header("Phase 2 Settings")]
    [SerializeField] private float phase2Duration = 60f; // Phase 2 starts after Phase 1
    [SerializeField] private float phase2SpawnInterval = 5f;
    [SerializeField] private int phase2AIsPerWave = 4;

    [Header("Phase 3 Settings")]
    [SerializeField] private float phase3SpawnInterval = 4f;
    [SerializeField] private int phase3AIsPerWave = 6;

    [Header("AI Prefabs")]
    [SerializeField] private GameObject[] aiPrefabs; // Array of AI shopper prefabs

    private int currentPhase = 0;
    private float elapsedGameTime = 0f;
    private float spawnInterval;
    private int aiPerWave;

    private float nextSpawnTime;

    private void Start()
    {
        // Find the spawn center (typically the player)
        spawnCenter = GameObject.FindGameObjectWithTag("Player").transform;
        if (spawnCenter == null)
        {
            Debug.LogError("Failed to locate the center of AI generation.");
        }

        // Initialize the first phase
        UpdateGamePhase();
    }

    private void Update()
    {
        // Update game time progression
        elapsedGameTime += Time.deltaTime;

        // Adjust spawn settings based on game progression
        UpdateGamePhase();

        // Spawn AIs at intervals
        if (Time.time >= nextSpawnTime)
        {
            SpawnAIWave();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void UpdateGamePhase()
    {
        if (elapsedGameTime < phase1Duration && currentPhase != 1)
        {
            // Phase 1
            currentPhase = 1;
            spawnInterval = phase1SpawnInterval;
            aiPerWave = phase1AIsPerWave;
        }
        else if (elapsedGameTime >= phase1Duration && elapsedGameTime < phase1Duration + phase2Duration && currentPhase != 2)
        {
            // Phase 2
            currentPhase = 2;
            spawnInterval = phase2SpawnInterval;
            aiPerWave = phase2AIsPerWave;
        }
        else if (elapsedGameTime >= phase1Duration + phase2Duration && currentPhase != 3)
        {
            // Phase 3
            currentPhase = 3;
            spawnInterval = phase3SpawnInterval;
            aiPerWave = phase3AIsPerWave;
        }
    }

    private void SpawnAIWave()
    {
        for (int i = 0; i < aiPerWave; i++)
        {
            // Keep trying to find a valid spawn position
            Vector3 spawnPosition = GetValidSpawnPosition();
            if (spawnPosition != Vector3.zero)
            {
                // Randomly select an AI prefab to spawn
                GameObject aiToSpawn = aiPrefabs[Random.Range(0, aiPrefabs.Length)];
                Instantiate(aiToSpawn, spawnPosition + new Vector3(0, 0.5f, 0), Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("Failed to find a valid spawn position after multiple attempts.");
            }
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        int maxRetries = 20; // Limit the number of retries to prevent infinite loops
        int attempts = 0;

        while (attempts < maxRetries)
        {
            // Generate a random point within the radius, ensuring it’s beyond minDistanceFromCenter
            Vector2 randomPoint = Random.insideUnitCircle * radius;
            if (randomPoint.magnitude >= minDistanceFromCenter)
            {
                Vector3 spawnPosition = new Vector3(randomPoint.x, 0, randomPoint.y) + spawnCenter.position;

                // Raycast to detect any surface
                if (Physics.Raycast(spawnPosition, Vector3.down, out RaycastHit hit, Mathf.Infinity))
                {
                    // Check if the hit object is on the ground layer
                    if (((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                    {
                        return hit.point; // Valid ground point
                    }
                }
            }

            attempts++;
        }

        return Vector3.zero; // Return an invalid position if no ground is found after retries
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}