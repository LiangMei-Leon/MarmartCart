using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Tomato Splash")]
    [SerializeField] private Image tomatoSplashEffect;
    [SerializeField] private float splashFadeDelay = 2f;      // wait before fade
    [SerializeField] private float splashFadeDuration = 3f;   // fade time
    private Coroutine tomatoSplashRoutine;

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
        if(cartControlInput.IsSpeedingUp() || cartControlInput.IsCharing())
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

        if (Physics.Raycast(rayStartPosition, transform.forward, out hit, distance, layerMask))
        {
            // Debug.Log(hit.transform.gameObject.name);

            // Check if the hit object is a Chained Cart
            if (hit.transform.gameObject.GetComponent<ChainedCartManager>() != null)
            {
                GameObject hitObject = hit.transform.gameObject;
                ChainedCartManager hitCartInfo = hitObject.GetComponent<ChainedCartManager>();

                // If the other player is in pit, despite charging or not, destroy self
                if (hitCartInfo.isCollectedByPlayer && !hitCartInfo.CompareTag(this.tag))
                {
                    if (hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>() != null)
                    {
                        CartControlScript hitCartController = hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>();
                        if (hitCartController.GetIsInPit())
                        {
                            DetachSelfCompletely();
                            return;
                        }
                    }
                }
                // Case when the player is charging (a strong powerup ability), they can detach any cart it hits along the way, ignoring the norms
                if (cartControlInput.IsCharing())
                {
                    if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                    {
                        hitDirection = -1 * hit.normal;
                        hitCartInfo.OnDetach(hitDirection);
                        sfxManager.PlaySFX("Detach");
                    }
                }
                else
                {
                    // Case when the player is not charging, they would always lose all of their carts when they hit other player's chained cart or themselves as long as not in ghost mode cooldown
                    if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                    {
                        DetachSelfCompletely();
                    }
                }

            }
            // If the hit object is the leading cart of the other player
            else if (hit.transform.gameObject.GetComponent<LeadingCartRaycaster>() != null)
            {
                // If the other player is in pit, despite charging or not, destroy self
                if (hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>() != null)
                {
                    CartControlScript hitCartController = hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>();
                    if (hitCartController.GetIsInPit())
                    {
                        DetachSelfCompletely();
                        return;
                    }
                }
                // If charging, destory all the carts of that player
                if (cartControlInput.IsCharing())
                {
                    if (hit.transform.parent.GetComponent<SnakeCartManager>().GetSnakeBody().Count >= 2)
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
                // If not charging, destory itself while that cart is not in ghost mode
                else
                {
                    if (cooldownTimer <= 0f && !hit.transform.gameObject.GetComponent<LeadingCartRaycaster>().getIfInGhostMode())
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

    public void TemporarilyDisableDetaching()
    {
        //Debug.Log("Attempt to reset timer");
        cooldownTimer = detachCooldown;
    }

    public void SetInGhostModeWithTime(float time)
    {
        cooldownTimer = time;
    }
    public void DetachSelfCompletely()
    {
        cooldownTimer = 4f;
        cartControlInput.DisallowActivatePowerUp();

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

            cartControlInput.AllowFlip();
            cartControlInput.DisallowActivatePowerUp();

            if (cartControlInput.IsCharing())
            {
                Destroy(collision.gameObject);
            }
        }

        if (collision.gameObject.CompareTag("Walls"))
        {
            sfxManager.PlaySFX("CrashWalls");
            cartControlInput.AllowFlip();
            cartControlInput.DisallowActivatePowerUp();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles") || collision.gameObject.CompareTag("Walls"))
        {
            if (cartControlInput.GetCanFlip())
            {
                cartControlInput.DisallowFlip();
                cartControlInput.AllowActivatePowerUp();
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles"))
        {
            if (cartControlInput.IsCharing())
                Destroy(collision.gameObject);
            cartControlInput.AllowFlip();
            cartControlInput.DisallowActivatePowerUp();
        }
        if (collision.gameObject.CompareTag("Walls"))
        {
            cartControlInput.AllowFlip();
            cartControlInput.DisallowActivatePowerUp();
        }
    }

    public bool getIfInGhostMode()
    {
        return cartInGhostMode;
    }

    public SnakeCartManager GetmySnakeCartManager()
    {
        return snakeCartManager;
    }
    public void TriggerTomatoSplash()
    {
        if (tomatoSplashEffect == null) return;

        // If an old splash is running, restart it
        if (tomatoSplashRoutine != null)
            StopCoroutine(tomatoSplashRoutine);

        tomatoSplashRoutine = StartCoroutine(TomatoSplashCoroutine());
    }

    private IEnumerator TomatoSplashCoroutine()
    {
        // Enable and set full alpha
        tomatoSplashEffect.gameObject.SetActive(true);

        Color c = tomatoSplashEffect.color;
        c.a = 1f;
        tomatoSplashEffect.color = c;

        // Hold full opacity for a delay
        yield return new WaitForSeconds(splashFadeDelay);

        // Fade out over splashFadeDuration
        float elapsed = 0f;
        while (elapsed < splashFadeDuration)
        {
            float t = elapsed / splashFadeDuration;
            c.a = Mathf.Lerp(1f, 0f, t);
            tomatoSplashEffect.color = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure alpha is fully 0 and turn off object
        c.a = 0f;
        tomatoSplashEffect.color = c;
        tomatoSplashEffect.gameObject.SetActive(false);

        tomatoSplashRoutine = null;
    }
}
