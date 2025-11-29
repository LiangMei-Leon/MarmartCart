using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public class PowerupsManager : MonoBehaviour
{
    public bool isPlayer1 = true;

    public enum PowerupType
    {
        Boost,
        Scale,
        BowlingBall,
        Tomato,
        Grabber
    }

    private PowerupTier currentTier = PowerupTier.Normal;
    private PowerupType? currentPowerup = null;
    private bool powerupReady = false;

    [Header("References")]
    [SerializeField] private SnakeCartManager snakeCartManager;
    [SerializeField] private CartControlScript cartControlScript;
    [SerializeField] private GameEvent boostEvent;
    [SerializeField] private GameObject bowlingBallPrefab;
    [SerializeField] private GameObject tomatoPrefab;
    [SerializeField] private Transform firePoint;

    [Header("Scaling Powerup")]
    [SerializeField] private GameObject cartPrefab;
    [SerializeField] private float scaleDuration = 10f;
    private int scaleBuffCount = 0;

    [SerializeField] private SfxManager sfxManager;
    [Header("Powerup Visuals")]
    [SerializeField] private GameObject visualBoost;
    [SerializeField] private GameObject visualBoostMore;
    [SerializeField] private GameObject visualScale;
    [SerializeField] private GameObject visualBowlingBall;
    [SerializeField] private GameObject visualTomato;
    [SerializeField] private GameObject visualGrabberRoot;
    [Header("Grabber Powerup")]
    [SerializeField] private GrabberAnimator grabberAnimatorInstance; // on GrabberRoot
    [SerializeField] private Transform grabberRoot;
    void Start()
    {
        Invoke(nameof(RegisterPlayer), 2f);

        // Make sure visuals start disabled
        UpdatePowerupVisuals();
    }
    public void RegisterPlayer()
    {
        if (snakeCartManager == null)
        {
            snakeCartManager = this.transform.parent.GetComponent<SnakeCartManager>();
        }
        if (cartControlScript == null)
        {
            cartControlScript = this.gameObject.GetComponentInChildren<CartControlScript>();
            cartControlScript.SetPowerupsManager(this);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            RollRandomPowerup(PowerupTier.Normal);
        }
    }
    // ------------------ ROLL LOGIC ------------------
    // Main API: tier-aware
    public void RollRandomPowerup(PowerupTier tier)
    {
        currentTier = tier;

        PowerupType rolled;

        if (tier == PowerupTier.Normal)
        {
            // Normal tier: Scale, Tomato, Grabber
            int roll = Random.Range(0, 2); // 0,1
            rolled = (roll == 0) ? PowerupType.Scale : (roll == 1) ? PowerupType.Tomato : PowerupType.Scale;
        }
        else
        {
            // Gold tier: Boost, Projectile
            int roll = Random.Range(0, 2); // 0,1
            rolled = (roll == 0) ? PowerupType.Boost : PowerupType.BowlingBall;
        }

        currentPowerup = rolled;
        powerupReady = true;

        cartControlScript.AllowActivatePowerUp();

        // Only Projectile needs aiming
        if (rolled == PowerupType.BowlingBall || rolled == PowerupType.Tomato || rolled == PowerupType.Grabber)
        {
            cartControlScript.AllowAim();
        }
        else
        {
            cartControlScript.DisallowAim();
        }
        UpdatePowerupVisuals();
        Debug.Log($"Player {(isPlayer1 ? 1 : 2)} rolled {tier} powerup: {currentPowerup}");
    }

    public void ActivateStoredPowerup()
    {
        if (!powerupReady || currentPowerup == null || !cartControlScript.GetCanActivatePowerUp()) return;

        switch (currentPowerup)
        {
            case PowerupType.Boost:
                boostEvent.Raise();
                visualBoostMore.SetActive(true);
                Invoke(nameof(DisableBoostMoreVisual), 1f);
                break;

            case PowerupType.Scale:
                StartCoroutine(ScaleCartsTemporarily());
                break;

            case PowerupType.BowlingBall:
                FireBowlingBall();
                break;

            case PowerupType.Tomato:
                FireTomato();
                break;

            case PowerupType.Grabber:
                //UseGrabber();
                break;
        }

        cartControlScript.DisallowAim();
        cartControlScript.DisallowActivatePowerUp();
        currentPowerup = null;
        powerupReady = false;
        UpdatePowerupVisuals();
    }
    // ------------------ VISUAL MANAGEMENT ------------------

    private void UpdatePowerupVisuals()
    {
        // If nothing equipped, hide all visuals
        if (!powerupReady || currentPowerup == null)
        {
            if (visualBoost) visualBoost.SetActive(false);
            if (visualScale) visualScale.SetActive(false);
            if (visualBowlingBall) visualBowlingBall.SetActive(false);
            if (visualTomato) visualTomato.SetActive(false);
            if (visualGrabberRoot) visualGrabberRoot.SetActive(false);
            return;
        }

        // Turn off everything first
        if (visualBoost) visualBoost.SetActive(false);
        if (visualScale) visualScale.SetActive(false);
        if (visualBowlingBall) visualBowlingBall.SetActive(false);
        if (visualTomato) visualTomato.SetActive(false);
        if (visualGrabberRoot) visualGrabberRoot.SetActive(false);

        // Now enable the one matching the current powerup
        switch (currentPowerup)
        {
            case PowerupType.Boost:
                if (visualBoost) visualBoost.SetActive(true);
                break;
            case PowerupType.Scale:
                if (visualScale) visualScale.SetActive(true);
                break;
            case PowerupType.BowlingBall:
                if (visualBowlingBall) visualBowlingBall.SetActive(true);
                break;
            case PowerupType.Tomato:
                if (visualTomato) visualTomato.SetActive(true);
                break;
            case PowerupType.Grabber:
                if (visualGrabberRoot) visualGrabberRoot.SetActive(true);
                break;
        }
    }
    // ------------------ GRABBER ------------------
    private void UseGrabber()
    {
        if (grabberAnimatorInstance == null || grabberRoot == null || firePoint == null)
        {
            Debug.LogWarning("PowerupsManager: Grabber used, but animator/root/firePoint missing.");
            return;
        }

        // Enable the grabber child
        grabberRoot.gameObject.SetActive(true);

        // Stick it at the firePoint (world position) but keep it parented
        grabberRoot.position = firePoint.position;

        // --- AIM ROTATION ---

        // AimDirection is in world space, already iso-adjusted
        Vector3 dir = cartControlScript.AimDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;          // fallback

        // Flatten to XZ
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        // Convert direction on XZ plane to a yaw angle in degrees
        // (0,0,1) => 0°, (1,0,0) => 90°, etc.
        float angleDeg = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;

        // If iso / model orientation feels off, tweak this in Inspector
        float yawOffset = 0f; // you can expose as [SerializeField] if you like

        float finalY = angleDeg + yawOffset;

        // Now force the grabber to "lie flat" and only rotate around Y
        grabberRoot.localEulerAngles = new Vector3(
            90f,    // X: horizontal
            finalY, // Y: aim
            180f    // Z: flipped or whatever matches your model
        );

        // Hook owner (safe even if done every time)
        var grabberBehavior = grabberRoot.GetComponentInChildren<GrabberBehavior>();
        if (grabberBehavior != null)
        {
            grabberBehavior.SetOwner(this, snakeCartManager);
        }

        // Play extend/retract
        grabberAnimatorInstance.PlayGrabber();
    }

    private IEnumerator ScaleCartsTemporarily()
    {
        scaleBuffCount++;

        // If this is the FIRST simultaneous scale buff → apply scale
        if (scaleBuffCount == 1)
        {
            List<GameObject> scaledCarts = snakeCartManager.GetSnakeBody();

            foreach (var cart in scaledCarts)
                cart.transform.localScale = new Vector3(10f, 10f, 10f);

            snakeCartManager.needScaleup = true;
        }

        yield return new WaitForSeconds(scaleDuration);

        scaleBuffCount--;

        // Only when all overlapping scale effects are finished → restore normal scale
        if (scaleBuffCount == 0)
        {
            snakeCartManager.needScaleup = false;
            cartPrefab.transform.localScale = new Vector3(5f, 5f, 5f);

            List<GameObject> scaledCarts = snakeCartManager.GetSnakeBody();
            foreach (var cart in scaledCarts)
                cart.transform.localScale = new Vector3(5f, 5f, 5f);
        }
    }

    private void FireBowlingBall()
    {
        if (bowlingBallPrefab == null || firePoint == null) return;

        //Debug.Log($"Player {(isPlayer1 ? 1 : 2)} firing projectile from {firePoint.position}");
        // Instantiate the projectile
        GameObject projectile = Instantiate(bowlingBallPrefab, firePoint.position, Quaternion.identity);

        // Get the direction from aim input or cart forward as fallback
        Vector3 fireDirection = cartControlScript.AimDirection;
        if (fireDirection == Vector3.zero)
            fireDirection = transform.forward;

        // Launch the projectile with player ownership info
        PowerupProjectile projectileScript = projectile.GetComponent<PowerupProjectile>();
        if (projectileScript != null)
        {
            projectileScript.Launch(fireDirection, isPlayer1);
        }
    }

    private void FireTomato()
    {
        if (tomatoPrefab == null || firePoint == null) return;

        //Debug.Log($"Player {(isPlayer1 ? 1 : 2)} firing projectile from {firePoint.position}");
        // Instantiate the projectile
        GameObject projectile = Instantiate(tomatoPrefab, firePoint.position, Quaternion.Euler(-90f, 0f, 0f));

        // Get the direction from aim input or cart forward as fallback
        Vector3 fireDirection = cartControlScript.AimDirection;
        if (fireDirection == Vector3.zero)
            fireDirection = transform.forward;

        // Launch the projectile with player ownership info
        PowerupProjectile projectileScript = projectile.GetComponent<PowerupProjectile>();
        if (projectileScript != null)
        {
            projectileScript.Launch(fireDirection, isPlayer1);
        }
    }
    private void DisableBoostMoreVisual()
    {
        visualBoostMore.SetActive(false);
    }
    public PowerupType? GetCurrentPowerup()
    {
        return currentPowerup;
    }
}
