using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public class PowerupsManager : MonoBehaviour
{
    public bool isPlayer1 = true;

    public enum PowerupType { Boost, Scale, Projectile }
    private PowerupType? currentPowerup = null;

    private bool powerupReady = false;

    [Header("References")]
    [SerializeField] private SnakeCartManager snakeCartManager;
    [SerializeField] private CartControlScript cartControlScript;
    [SerializeField] private GameEvent boostEvent;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Scaling Powerup")]
    [SerializeField] private float scaleDuration = 10f;

    void Start()
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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RollRandomPowerup();
        }
    }
    public void RollRandomPowerup()
    {

        int roll = Random.Range(0, 3);
        currentPowerup = (PowerupType)roll;
        powerupReady = true;

        cartControlScript.AllowActivatePowerUp();

        if (roll == 2)
        {
            cartControlScript.AllowAim();
        }
        Debug.LogError($"Player {(isPlayer1 ? 1 : 2)} rolled powerup: {currentPowerup}");
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

            case PowerupType.Projectile:
                FireProjectile();
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

        foreach (var cart in scaledCarts)
            cart.transform.localScale = new Vector3(5f, 5f, 5f);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || firePoint == null) return;

        Debug.Log($"Player {(isPlayer1 ? 1 : 2)} firing projectile from {firePoint.position}");
        // Instantiate the projectile
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

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
