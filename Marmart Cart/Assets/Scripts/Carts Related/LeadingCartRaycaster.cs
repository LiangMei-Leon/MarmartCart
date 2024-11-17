using UnityEngine;

public class LeadingCartRaycaster : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] LayerMask layerMask;
    [SerializeField] float distance;
    [field: SerializeField]
    public Vector3 hitDirection { get; private set; }

    [Header("Others")]
    [SerializeField] private float detachCooldown = 5f; // Cooldown duration in seconds
    [SerializeField] private float cooldownTimer = 0f; // Tracks the cooldown timer

    [Header("Events")]
    [SerializeField] GameEvent disableDetachEvent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        // Update the cooldown timer
        
        cooldownTimer -= Time.deltaTime;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, distance, layerMask))
        {
            // Debug.Log(hit.transform.gameObject.name);
            if(hit.transform.gameObject.GetComponent<ChainedCartManager>() != null)
            {
                ChainedCartManager cartInfo = hit.transform.gameObject.GetComponent<ChainedCartManager>();
                if (cartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                {
                    hitDirection = -1 * hit.normal;
                    cartInfo.OnDetach();
                }
            }

            if(hit.transform.gameObject.CompareTag("Obstacles"))
            {
                Debug.Log("Raised");
                disableDetachEvent.Raise();
            }
        }
    }

    public void TemporarilyDisableDetaching()
    {
        Debug.Log("Attempt to reset timer");
        cooldownTimer = detachCooldown;
    }

    void OnDrawGizmos()
    {
        // Draw our friend ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * distance);
    }
}
