using UnityEngine;

public class LeadingCartRaycaster : MonoBehaviour
{
    [SerializeField] CartControlScript cartControlInput;

    [Header("Raycast Settings")]
    [SerializeField] LayerMask layerMask;
    [SerializeField] float distance;
    [SerializeField] private float raycastOffset = 0.5f;
    [field: SerializeField]
    public Vector3 hitDirection { get; private set; }

    [Header("Others")]
    [SerializeField] private float detachCooldown = 5f; // Cooldown duration in seconds
    [SerializeField] private float cooldownTimer = 0f; // Tracks the cooldown timer

    [Header("Events")]
    [SerializeField] GameEvent disableDetachEvent;

    [SerializeField] SfxManager sfxManager;
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
        Vector3 rayStartPosition = transform.position + transform.forward * raycastOffset;
        RaycastHit hit;
        if (Physics.Raycast(rayStartPosition, transform.forward, out hit, distance, layerMask))
        {
            // Debug.Log(hit.transform.gameObject.name);
            if(hit.transform.gameObject.GetComponent<ChainedCartManager>() != null)
            {
                ChainedCartManager cartInfo = hit.transform.gameObject.GetComponent<ChainedCartManager>();
                if (cartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                {
                    hitDirection = -1 * hit.normal;
                    cartInfo.OnDetach();
                    sfxManager.PlaySFX("Detach");
                }
            }

            if(hit.transform.gameObject.CompareTag("Obstacles"))
            {
                //Debug.Log("Raised");
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
        Vector3 rayStartPosition = transform.position + transform.forward * raycastOffset;
        Gizmos.DrawRay(rayStartPosition, transform.forward * distance);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Obstacles"))
        {
            sfxManager.PlaySFX("CrashWalls");
            //cartControlInput.AllowFlip();
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Walls"))
        {
            sfxManager.PlaySFX("CrashWalls");
            cartControlInput.AllowFlip();
        }
    }
}
