using UnityEngine;

public class DinoBehaviour : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;
    [SerializeField] private int currentStage = 0;

    [Header("Attack Settings (Stage-based)")]
    [SerializeField] private float[] stageAttackRadius = { 10f, 12f, 15f };
    [SerializeField] private float[] stageAttackInterval = { 15f, 12f, 10f };
    [SerializeField] private int[] stagePathLength = { 10, 15, 20 };
    [SerializeField] private int[] stageTurnCounts = { 1, 2, 3 };
    [SerializeField] private LayerMask obstacleLayer;

    private float attackTimer;

    [Header("Movement Settings")]
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float angleStep = 15f;
    [SerializeField] private float raycastDistance = 1000f;
    private bool isMoving = false;
    private Vector3 moveTarget;
    [SerializeField] private DinoTileManager dinoTileManager;

    [SerializeField] private GameTimeManager gameTimeManager;

    [SerializeField] private GameObject bonusItemPrefab;
    [SerializeField] private LayerMask groundLayer;

    void Awake()
    {
    }
    void Start()
    {

        currentHealth = maxHealth;
        gameTimeManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameTimeManager>();
        currentStage = gameTimeManager.GetCurrentGameStage();
        attackTimer = stageAttackInterval[currentStage];
        dinoTileManager.SetPathLength(stagePathLength[currentStage]);
        dinoTileManager.SetTurnCounts(stageTurnCounts[currentStage]);
        dinoTileManager.GeneratePath();
    }

    void Update()
    {
        currentStage = gameTimeManager.GetCurrentGameStage();
        dinoTileManager.SetPathLength(stagePathLength[currentStage]);
        dinoTileManager.SetTurnCounts(stageTurnCounts[currentStage]);

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            PerformAttack();
            TryFindMovementDirection();
            attackTimer = stageAttackInterval[currentStage];
        }

        if (isMoving)
        {
            MoveTowardsTarget();
            return;
        }

//         if (Input.GetKeyDown(KeyCode.K))
//         {
//             TakeDamage();
//         }
    }

    private void PerformAttack()
    {
        float radius = stageAttackRadius[currentStage];
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, obstacleLayer);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Obstacles"))
            {
                Destroy(hit.gameObject);
            }
        }
    }
    private void TryFindMovementDirection()
    {
        float randomStartAngle = Random.Range(0f, 360f);
        Vector3 origin = transform.position;
        origin.y = 1f;

        for (float angleOffset = 0; angleOffset < 360f; angleOffset += angleStep)
        {
            float angle = randomStartAngle + angleOffset;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, obstacleLayer))
            {
                if (hit.collider.CompareTag("Obstacles"))
                {
                    moveTarget = transform.position + direction.normalized * moveDistance;
                    isMoving = true;
                    return;
                }
            }
        }
    }
    private void MoveTowardsTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, moveTarget) < 0.01f)
        {
            isMoving = false;
            //dinoTileManager.GeneratePath();
        }
    }
    public void TakeDamage()
    {
        currentHealth--;
        if (currentHealth <= 0)
        {
            Debug.Log("Dino defeated!");
            for (int i = 0; i < 10; i++)
            {
                // Keep trying to find a valid spawn position
                Vector3 spawnPosition = GetValidSpawnPosition();
                if (spawnPosition != Vector3.zero)
                {
                    Instantiate(bonusItemPrefab, spawnPosition + new Vector3(0, 10f, 0), Quaternion.identity);
                }
                else
                {
                    Debug.LogWarning("Failed to find a valid spawn position after multiple attempts.");
                }
            }
            dinoTileManager.ClearExistingTiles();
            Destroy(this.gameObject); // Or trigger death animation/event instead
        }
        else
        {
            
            Debug.Log($"Dino took damage! Stage is now {currentStage + 1}");
            attackTimer = stageAttackInterval[currentStage]; // Reset timer for new stage

            //update path diffculy level and generate
            dinoTileManager.SetPathLength(stagePathLength[currentStage]);
            dinoTileManager.SetTurnCounts(stageTurnCounts[currentStage]);
            dinoTileManager.GeneratePath();
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        int maxRetries = 50; // Limit the number of retries to prevent infinite loops
        int attempts = 0;

        while (attempts < maxRetries)
        {
            // Generate a random point within the radius, ensuring it’s beyond minDistanceFromCenter
            Vector2 randomPoint = Random.insideUnitCircle * 20f;
            if (randomPoint.magnitude >= 1f)
            {
                Vector3 spawnPosition = new Vector3(randomPoint.x, 20, randomPoint.y) + this.transform.position;

                // Raycast to detect any surface
                if (Physics.Raycast(spawnPosition, Vector3.down, out RaycastHit hit, Mathf.Infinity))
                {
                    // Check if the hit object is on the ground layer
                    if (((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                    {
                        return hit.point; // Valid ground point
                    }
                    else
                    {
                        // If not on ground layer, continue trying
                        Debug.Log($"Invalid hit on layer {hit.collider.gameObject.layer}, retrying...");
                    }
                }
            }

            attempts++;
        }

        return Vector3.zero; // Return an invalid position if no ground is found after retries
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stageAttackRadius[currentStage]);
    }
}
