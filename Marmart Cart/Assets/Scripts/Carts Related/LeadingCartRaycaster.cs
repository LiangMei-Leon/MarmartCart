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
        if(GMode.Instance.IsCoop)
        {
            if (Physics.Raycast(rayStartPosition, transform.forward, out hit, distance, layerMask))
            {
                // Debug.Log(hit.transform.gameObject.name);
                if (hit.transform.gameObject.GetComponent<ChainedCartManager>() != null)
                {
                    ChainedCartManager cartInfo = hit.transform.gameObject.GetComponent<ChainedCartManager>();
                    if (cartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                    {
                        hitDirection = -1 * hit.normal;
                        cartInfo.OnDetach();
                        sfxManager.PlaySFX("Detach");
                    }
                }

                if (hit.transform.gameObject.CompareTag("Obstacles"))
                {
                    //Debug.Log("Raised");
                    disableDetachEvent.Raise();
                }
            }
        }
        else if (GMode.Instance.IsCompetitive)
        {
            if (Physics.Raycast(rayStartPosition, transform.forward, out hit, distance, layerMask))
            {
                // Check if the hit object is a Chained Cart
                if (hit.transform.gameObject.GetComponent<ChainedCartManager>() != null)
                {
                    GameObject hitObject = hit.transform.gameObject;
                    ChainedCartManager hitCartInfo = hitObject.GetComponent<ChainedCartManager>();
                    
                    if (cartControlInput.IsCharing())
                    {
                        // If the player is charging, we can detach any cart it hits along the way
                        if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                        {
                            //hitDirection = -1 * hit.normal;
                            hitCartInfo.OnDetach();
                            sfxManager.PlaySFX("Detach");
                        }
                    }
                    else
                    {
                        // If the player is not charging, we only detach if the cart has the same tag as the leading cart (Only detach if it's the same player)
                        if (hitObject.CompareTag(this.gameObject.tag))
                        {
                            if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                            {
                                //hitDirection = -1 * hit.normal;
                                hitCartInfo.OnDetach();
                                sfxManager.PlaySFX("Detach");
                            }
                        }
                    }
                    
                }
                // Check if the hit object is an obstacle
                if (hit.transform.gameObject.CompareTag("Obstacles"))
                {
                    //Debug.Log("Raised");
                    disableDetachEvent.Raise();
                    Destroy(hit.transform.gameObject);
                }
            }
        }
    }

    public void TemporarilyDisableDetaching()
    {
        //Debug.Log("Attempt to reset timer");
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
            if(GMode.Instance.IsCompetitive)
            {
                cartControlInput.AllowFlip();

                if(cartControlInput.IsCharing())
                {
                    Destroy(collision.gameObject);
                }
            }
        }

        if (collision.gameObject.CompareTag("Walls"))
        {
            sfxManager.PlaySFX("CrashWalls");
            cartControlInput.AllowFlip();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles"))
        {
            //sfxManager.PlaySFX("CrashWalls");
            //cartControlInput.AllowFlip();
            if (GMode.Instance.IsCompetitive && cartControlInput.IsCharing())
                Destroy(collision.gameObject);
        }
    }
}
