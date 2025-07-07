using UnityEngine;

public class LeadingCartRaycaster : MonoBehaviour
{
    [SerializeField] CartControlScript cartControlInput;
    [SerializeField] SnakeCartManager snakeCartManager;

    [Header("Raycast Settings")]
    [SerializeField] LayerMask layerMask;
    [SerializeField] float distance;
    [SerializeField] private float raycastYOffset = 0.5f;
    [SerializeField] private float raycastZOffset = 0.5f;

    [field: SerializeField]
    public Vector3 hitDirection { get; private set; }

    [Header("Others")]
    [SerializeField] private float detachCooldown = 5f; // Cooldown duration in seconds
    [SerializeField] private float cooldownTimer = 0f; // Tracks the cooldown timer
    private bool cartInGhostMode = false;

    [Header("Events")]
    [SerializeField] GameEvent disableDetachEvent;

    [SerializeField] SfxManager sfxManager;
    [SerializeField] GameObject chargingVFX;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        snakeCartManager = this.transform.parent.GetComponent<SnakeCartManager>();
    }

    // Update is called once per frame
    void Update()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f)
        {
            if (!cartInGhostMode)
            {
                this.gameObject.GetComponent<CartMaterialManager>()?.SetCooldown(cooldownTimer);
                cartInGhostMode = true;
            }
        }
        else
        {
            cartInGhostMode = false;
        }
        if(cartControlInput.IsCharing())
        {
            chargingVFX.SetActive(true);
        }
        else
        {
            chargingVFX.SetActive(false);
        }
    }

    void FixedUpdate()
    {
        Vector3 rayStartPosition = transform.position + transform.forward * raycastZOffset + transform.up * raycastYOffset;
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
                        cartInfo.OnDetach(hitDirection);
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
                // Debug.Log(hit.transform.gameObject.name);
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
                            hitDirection = -1 * hit.normal;
                            hitCartInfo.OnDetach(hitDirection);
                            sfxManager.PlaySFX("Detach");
                        }
                    }
                    else
                    {
                        // If the player is not charging, detach itself from the begining if it hits the opponent's cart
                        if (!hitObject.CompareTag(this.gameObject.tag))
                        {
                            if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                            {
                                DetachSelfCompletely();
                            }
                        }
                        // If the player is not charging, detach carts if it hits its own cart
                        if (hitObject.CompareTag(this.gameObject.tag))
                        {
                            if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                            {
                                hitDirection = -1 * hit.normal;
                                hitCartInfo.OnDetach(hitDirection);
                                sfxManager.PlaySFX("Detach");
                            }
                        }
                    }
                    
                }
                // If the hit object is the leading cart of the other player
                else if (hit.transform.gameObject.GetComponent<LeadingCartRaycaster>() != null)
                {
                    // If charging, destory all the carts of that player
                    if (cartControlInput.IsCharing())
                    {
                        if(hit.transform.parent.GetComponent<SnakeCartManager>().GetSnakeBody().Count >= 2)
                        {
                            ChainedCartManager secondCartOnOtherPlayer = hit.transform.parent.GetComponent<SnakeCartManager>().GetSnakeBody()[1].GetComponent<ChainedCartManager>();
                            if (cooldownTimer <= 0f)
                            {
                                hitDirection = -1 * hit.normal;
                                secondCartOnOtherPlayer.OnDetach(hitDirection);
                                sfxManager.PlaySFX("Detach");
                            }
                        }
                    }
                    // If not charging, destory itself
                    else
                    {
                        if (cooldownTimer <= 0f)
                        {
                            DetachSelfCompletely();
                        }
                    }
                }
                // Check if the hit object is an obstacle, destroy them when charing
                if (hit.transform.gameObject.CompareTag("Obstacles") && cartControlInput.IsCharing())
                {
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
    private void DetachSelfCompletely()
    {
        cooldownTimer = 4f;
        cartControlInput.DisallowBoost();

        if (snakeCartManager.GetSnakeBody().Count >= 2)
        {
            snakeCartManager.GetSnakeBody()[1].GetComponent<ChainedCartManager>().OnDetach();
            sfxManager.PlaySFX("Detach");
        }

        LeadingCartBehaviour leadingCartBehaviour0 = this.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour1 = this.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour2 = this.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour3 = this.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

        leadingCartBehaviour0.SetSpeedToZero(2f);
        leadingCartBehaviour1.SetSpeedToZero(2f);
        leadingCartBehaviour2.SetSpeedToZero(2f);
        leadingCartBehaviour3.SetSpeedToZero(2f);
    }
    void OnDrawGizmos()
    {
        // Draw our friend ray
        Gizmos.color = Color.red;
        Vector3 rayStartPosition = transform.position + transform.forward * raycastZOffset + transform.up * raycastYOffset;
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
        if (collision.gameObject.CompareTag("Walls"))
        {
            Debug.Log("a");
            cartControlInput.AllowFlip();
        }
    }

    public bool getIfInGhostMode()
    {
        return cartInGhostMode;
    }
}
