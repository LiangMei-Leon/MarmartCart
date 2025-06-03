using UnityEngine;

public class DinoBehaviour : MonoBehaviour
{
    [Header("Stage Settings")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;
    private int currentStage => Mathf.Clamp(maxHealth - currentHealth, 0, 2);

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

    void Start()
    {
        currentHealth = maxHealth;
        attackTimer = stageAttackInterval[currentStage];
        dinoTileManager.SetPathLength(stagePathLength[currentStage]);
        dinoTileManager.SetTurnCounts(stageTurnCounts[currentStage]);
        dinoTileManager.GeneratePath();
    }

    void Update()
    {
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
        Vector3 origin = transform.position + Vector3.up * 0.5f;

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
        }
    }
    public void TakeDamage()
    {
        if (currentHealth <= 1)
        {
            Debug.Log("Dino defeated!");
            Destroy(gameObject); // Or trigger death animation/event instead
        }
        else
        {
            currentHealth--;
            Debug.Log($"Dino took damage! Stage is now {currentStage + 1}");
            attackTimer = stageAttackInterval[currentStage]; // Reset timer for new stage

            //update path diffculy level and generate
            dinoTileManager.SetPathLength(stagePathLength[currentStage]);
            dinoTileManager.SetTurnCounts(stageTurnCounts[currentStage]);
            dinoTileManager.GeneratePath();
        }
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stageAttackRadius[currentStage]);
    }
}
