using UnityEngine;

public class ItemGenerationManager : MonoBehaviour
{
    //[Header("Spawn Settings")]
    //[SerializeField] private bool isForPlayer1 = true;
    //[SerializeField] private Transform spawnCenter;

    [Header("Spawn Box Boundary Settings")]
    [SerializeField] private float boxWidth = 40f;   // Total width of the spawn box
    [SerializeField] private float boxLength = 30f;  // Total length of the spawn box

    [SerializeField] private LayerMask groundLayer; // Layer mask for ground detection

    [Header("Phase 1 Settings")]
    [SerializeField] private float phase1Duration = 60f;
    [SerializeField] private float phase1SpawnInterval = 15f;
    [SerializeField] private int phase1ItemsPerSpawn = 5;

    [Header("Phase 2 Settings")]
    [SerializeField] private float phase2Duration = 60f; // Phase 2 starts after Phase 1
    [SerializeField] private float phase2SpawnInterval = 10f;
    [SerializeField] private int phase2ItemsPerSpawn = 6;

    [Header("Phase 3 Settings")]
    [SerializeField] private float phase3SpawnInterval = 10f;
    [SerializeField] private int phase3ItemsPerSpawn = 8;

    private int currentPhase = 0;
    private float elapsedGameTime = 0f;
    private float spawnInterval;
    private int itemsPerSpawn;


    [Header("Cart Prefabs")]
    [SerializeField] private GameObject cartPrefab;

    [SerializeField] private float yOffset = 20f;

    [Header("Poor and Temp fix on prefab scale issue")]
    [SerializeField] private SnakeCartManager snakeCartManager1;
    [SerializeField] private SnakeCartManager snakeCartManager2;
    [SerializeField] private SnakeCartManager snakeCartManager3;
    [SerializeField] private SnakeCartManager snakeCartManager4;
    [SerializeField] private bool applyPrefabScaleFix = false;


    private float nextSpawnTime;

    private void Start()
    {
       
    }

    void Update()
    {
        // Update game time progression
        elapsedGameTime += Time.deltaTime;

        // Adjust spawn settings based on game progression
        UpdateGamePhase();

        // Spawn items at intervals
        if (Time.time >= nextSpawnTime)
        {
            SpawnItems();
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
            itemsPerSpawn = phase1ItemsPerSpawn;
        }
        else if (elapsedGameTime >= phase1Duration && elapsedGameTime < phase1Duration + phase2Duration && currentPhase != 2)
        {
            // Phase 2
            currentPhase = 2;
            spawnInterval = phase2SpawnInterval;
            itemsPerSpawn = phase2ItemsPerSpawn;
        }
        else if (elapsedGameTime >= phase1Duration + phase2Duration && currentPhase != 3)
        {
            // Phase 3
            currentPhase = 3;
            spawnInterval = phase3SpawnInterval;
            itemsPerSpawn = phase3ItemsPerSpawn;
        }
    }

    private void SpawnItems()
    {
        for (int i = 0; i < itemsPerSpawn; i++)
        {
            Vector3 spawnPosition = GetValidSpawnPosition();
            if (spawnPosition != Vector3.zero)
            {
                GameObject prefabToSpawn = cartPrefab; 
                // Instantiate the item
                GameObject spawned = Instantiate(prefabToSpawn, spawnPosition + new Vector3(0, 10f, 0), prefabToSpawn.transform.rotation);
                //spawned.transform.localScale = new Vector3(5f, 5f, 5f);
                if (applyPrefabScaleFix && (snakeCartManager1.needScaleup || snakeCartManager2.needScaleup || snakeCartManager3.needScaleup || snakeCartManager4.needScaleup))
                {
                    spawned.transform.localScale = new Vector3(5f, 5f, 5f);
                }
            }
            else
            {
                //Debug.LogWarning("Failed to find a valid spawn position after multiple attempts.");
            }
        }
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
        Gizmos.color = Color.yellow;

        Vector3 center = this.transform.position;
        Vector3 size = new Vector3(boxWidth, 20f, boxLength);

        Gizmos.DrawWireCube(center, size);
    }
}
