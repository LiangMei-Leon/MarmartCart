using UnityEngine;
using UnityEngine.AI;

public class AIShopperBehaviour : MonoBehaviour
{
    public AIState currentState = AIState.Wandering;

    [SerializeField] private float collectRange = 5f; // Distance to "collect" item
    [SerializeField] private float wanderRange = 20f;   // Distance for random wandering
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float escapeSpeedMultiplier = 2f;
    private CartRarity carryingRarity = CartRarity.Common;
    [Header("Carrying Cart Visuals")]
    [SerializeField] private GameObject commonVisual;
    [SerializeField] private GameObject rareVisual;
    [SerializeField] private GameObject epicVisual;
    [SerializeField] private GameObject legendaryVisual;
    [SerializeField] private GameObject emptyCartVisual;
    [SerializeField] private GameObject itemCartVisual;
    [SerializeField] private GameObject expensiveItemCartVisual;
    [Header("Cart Collect Events")]
    [SerializeField] private GameEvent p1CollectEmptyCartEvent;
    [SerializeField] private GameEvent p1CollectNormalItemCartEvent;
    [SerializeField] private GameEvent p1CollectExpensiveItemCartEvent;
    [SerializeField] private GameEvent p2CollectEmptyCartEvent;
    [SerializeField] private GameEvent p2CollectNormalItemCartEvent;
    [SerializeField] private GameEvent p2CollectExpensiveItemCartEvent;

    private NavMeshAgent agent;
    private Transform targetItem;
    private Transform targetExit;
    private bool itemIsBonus = false;

    private GameObject runningVFX;
    private GameObject hittingVFX;
    [SerializeField] private bool carryingItem = false;
    [SerializeField] private bool carryingNormalItem = false;
    [SerializeField] private bool carryingExpensiveItem = false;

    [SerializeField] GameObjectPool targetPool;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = baseSpeed;
    }
    private void Start()
    {
        FindRandomTargetItem();

        runningVFX = this.transform.GetChild(0).GetChild(0).gameObject;
        runningVFX.SetActive(false);
        hittingVFX = this.transform.GetChild(0).GetChild(1).gameObject;
        hittingVFX.SetActive(false);

        emptyCartVisual.SetActive(false);
        itemCartVisual.SetActive(false);
        commonVisual.SetActive(false);
        rareVisual.SetActive(false);
        epicVisual.SetActive(false);
        legendaryVisual.SetActive(false);
    }

    private void Update()
    {
        switch (currentState)
        {
            case AIState.Seeking:
                SeekTarget();
                break;

            case AIState.Collecting:
                CollectItem();
                break;

            case AIState.Escaping:
                Escape();
                break;

            case AIState.Wandering:
                Wander();
                break;
        }
    }

    private void SeekTarget()
    {
        if (targetItem != null)
        {
            var itemManager = targetItem.GetComponent<ChainedCartManager>();
            if (itemManager != null && !itemManager.isAvailable)
            {
                // If the item is no longer available, reset target and wander
                targetItem = null;
                currentState = AIState.Wandering;
                //Debug.Log("Target item is no longer available, switching to wandering.");
                return;
            }

            agent.SetDestination(targetItem.position);

            if (Vector3.Distance(transform.position, targetItem.position) <= collectRange)
            {
                currentState = AIState.Collecting;
            }
        }
        else
        {
            currentState = AIState.Wandering;
        }
    }

    private void CollectItem()
    {
        if (targetItem != null)
        {
            var itemManager = targetItem.GetComponent<ChainedCartManager>();
            if (itemManager != null)
            {
                itemManager.CollectByAI(); // Mark as collected by AI
                if(itemManager.HasGroceryItem() && itemManager.isCarryingNormalGroceryItem())
                {
                    carryingItem = true;
                    carryingNormalItem = true;
                    carryingExpensiveItem = false;
                    itemCartVisual.SetActive(true);
                }
                else if(itemManager.HasGroceryItem() && itemManager.isCarryingExpensiveGroceryItem())
                {
                    carryingItem = true;
                    carryingNormalItem = false;
                    carryingExpensiveItem = true;
                    expensiveItemCartVisual.SetActive(true);
                }
                else
                {
                    carryingItem = false;
                    emptyCartVisual.SetActive(true);
                }
            }

            Destroy(targetItem.gameObject); // Assume item is collected
            targetItem = null;

            FindNearestExit();
            agent.speed = baseSpeed * escapeSpeedMultiplier;
            currentState = AIState.Escaping;
        }
    }

    private void Escape()
    {
        if (targetExit != null)
        {
            runningVFX.SetActive(true);
            agent.SetDestination(targetExit.position);

            if (Vector3.Distance(transform.position, targetExit.position) <= collectRange)
            {
                // Exit reached, destroy AI or mark as "exited"
                ResetState();
                targetPool.ReturnObject(gameObject);
            }
        }
        else
        {
            runningVFX.SetActive(false);
            currentState = AIState.Wandering;
        }
    }

    private void Wander()
    {
        if (!agent.hasPath)
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRange;
            randomDirection += transform.position;

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(randomDirection, out navHit, wanderRange, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (currentState == AIState.Wandering && other.CompareTag("Item"))
        {
            targetItem = other.transform;
            currentState = AIState.Seeking;
            Debug.Log("New available item in range");
        }
    }
    private void FindRandomTargetItem()
    {
        var items = GameObject.FindGameObjectsWithTag("Item");
        if (items.Length > 0)
        {
            targetItem = items[Random.Range(0, items.Length)].transform;
            currentState = AIState.Seeking;
        }
        else
        {
            currentState = AIState.Wandering;
        }
    }

    private void FindNearestExit()
    {
        var exits = GameObject.FindGameObjectsWithTag("Exit");
        float shortestDistance = Mathf.Infinity;

        foreach (var exit in exits)
        {
            float distance = Vector3.Distance(transform.position, exit.transform.position);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                targetExit = exit.transform;
            }
        }
    }

    public void OnKnockOut(int playerIndex)
    {
        hittingVFX.SetActive(true);
        itemCartVisual.SetActive(false);
        expensiveItemCartVisual.SetActive(false);
        emptyCartVisual.SetActive(false);
        runningVFX.SetActive(false);
        if (currentState == AIState.Escaping)
        {
            if (carryingItem && carryingNormalItem)
            {
                if (playerIndex == 1) p1CollectNormalItemCartEvent.Raise();
                else if (playerIndex == 2) p2CollectNormalItemCartEvent.Raise();
            }
            else if (carryingItem && carryingExpensiveItem)
            {
                if (playerIndex == 1) p1CollectExpensiveItemCartEvent.Raise();
                else if (playerIndex == 2) p2CollectExpensiveItemCartEvent.Raise();
            }
            else
            {
                if (playerIndex == 1) p1CollectEmptyCartEvent.Raise();
                else if (playerIndex == 2) p2CollectEmptyCartEvent.Raise();
            }
        }        
    }
    public void ResetState()
    {
        currentState = AIState.Wandering;
        agent.enabled = true;
        agent.speed = baseSpeed;
        targetItem = null;
        targetExit = null;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(this.transform.position, collectRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(this.transform.position, wanderRange);
    }
}
