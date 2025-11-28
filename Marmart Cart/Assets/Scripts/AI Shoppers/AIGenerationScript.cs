using UnityEngine;

public class AIGenerationScript : MonoBehaviour
{
    [Header("Spawn Box Boundary Settings")]
    [SerializeField] private float boxWidth = 40f;   // Total width of the spawn box
    [SerializeField] private float boxLength = 30f;  // Total length of the spawn box
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
    [SerializeField] private GameObjectPool[] aiPools; // Array of pools of AI shopper prefabs

    private int currentPhase = 0;
    private float elapsedGameTime = 0f;
    private float spawnInterval;
    private int aiPerWave;
    private bool isSpawning = false;

    private float nextSpawnTime;

    private void Start()
    {
        // Initialize pools for all AI prefabs
        foreach (var pool in aiPools)
        {
            pool.SpawnPool();
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
        if (isSpawning) return; // Prevent overlapping calls
        isSpawning = true;

        //Debug.Log("SpawnAIWave called");
        for (int i = 0; i < aiPerWave; i++)
        {
            Vector3 spawnPosition = GetValidSpawnPosition();
            if (spawnPosition != Vector3.zero)
            {
                var randomPool = aiPools[Random.Range(0, aiPools.Length)];
                GameObject ai = randomPool.GetGameObject(spawnPosition + Vector3.up * 0.5f, Quaternion.identity);
                //Debug.Log("AI generated");
                var aiBehaviour = ai.GetComponent<AIShopperBehaviour>();
                if (aiBehaviour != null)
                {
                    aiBehaviour.ResetState();
                }
            }
        }

        isSpawning = false; // Reset spawning state
    }

    private Vector3 GetValidSpawnPosition()
    {
        int maxRetries = 50; // Limit the number of retries to prevent infinite loops
        int attempts = 0;

        while (attempts < maxRetries)
        {
            // Generate a random point within the box
            float halfWidth = boxWidth * 0.5f;
            float halfLength = boxLength * 0.5f;

            float xOffset = Random.Range(-halfWidth, halfWidth);
            float zOffset = Random.Range(-halfLength, halfLength);

            Vector3 spawnPosition = this.transform.position + new Vector3(xOffset, 20f, zOffset); // height can be adjusted if needed

            // Raycast to detect any surface
            if (Physics.Raycast(spawnPosition, Vector3.down, out RaycastHit hit, Mathf.Infinity))
            {
                // Check if the hit object is on the ground layer
                if (((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                {
                    return hit.point; // Valid ground point
                }
            }
            attempts++;
        }
        
        return Vector3.zero; // Return an invalid position if no ground is found after retries
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        Vector3 center = this.transform.position;
        Vector3 size = new Vector3(boxWidth, 20f, boxLength);

        Gizmos.DrawWireCube(center, size);
    }
}