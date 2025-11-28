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

    [SerializeField] private SfxManager sfxManager;

    void Start()
    {
        Invoke(nameof(RegisterPlayer), 2f);
        
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
            int roll = Random.Range(0, 3); // 0,1,2
            switch (roll)
            {
                case 0: rolled = PowerupType.Scale; break;
                case 1: rolled = PowerupType.Tomato; break;
                default: rolled = PowerupType.Grabber; break;
            }
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

        Debug.Log($"Player {(isPlayer1 ? 1 : 2)} rolled {tier} powerup: {currentPowerup}");
    }

    public void ActivateStoredPowerup()
    {
        if (!powerupReady || currentPowerup == null || !cartControlScript.GetCanActivatePowerUp()) return;

        switch (currentPowerup)
        {
            case PowerupType.Boost:
                boostEvent.Raise();
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
                // TODO: Implement Grabber behavior
                Debug.Log($"Player {(isPlayer1 ? 1 : 2)} used GRABBER powerup (TODO)");
                // e.g. StartCoroutine(GrabberCoroutine());
                break;
        }

        cartControlScript.DisallowAim();
        cartControlScript.DisallowActivatePowerUp();
        currentPowerup = null;
        powerupReady = false;
    }

    private IEnumerator ScaleCartsTemporarily()
    {
        List<GameObject> scaledCarts = snakeCartManager.GetSnakeBody();

        foreach (var cart in scaledCarts)
            cart.transform.localScale = new Vector3(10f, 10f, 10f);

        snakeCartManager.needScaleup = true;

        yield return new WaitForSeconds(scaleDuration);

        snakeCartManager.needScaleup = false;
        cartPrefab.transform.localScale = new Vector3(5f, 5f, 5f); //hard reset the prefab scale to avoid issues with newly spawned carts

        foreach (var cart in scaledCarts)
            cart.transform.localScale = new Vector3(5f, 5f, 5f);
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
        GameObject projectile = Instantiate(tomatoPrefab, firePoint.position, Quaternion.identity);

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
    public PowerupType? GetCurrentPowerup()
    {
        return currentPowerup;
    }
}
