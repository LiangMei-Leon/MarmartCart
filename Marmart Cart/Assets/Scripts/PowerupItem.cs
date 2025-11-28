using UnityEngine;
public enum PowerupTier
{
    Normal,
    Gold
}
public class PowerupItem : MonoBehaviour
{
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private float selfCleanTime = 20f;
    [Header("Powerup Tier")]
    [SerializeField] private PowerupTier powerupTier = PowerupTier.Normal;  // NEW

    private void Start()
    {
        // Automatically destroy the powerup item after X seconds if not collected
        Destroy(this.gameObject, selfCleanTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player1"))
        {
            PowerupsManager p1PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p1PowerupManager == null)
            {
                // hit a chained cart instead of the leading cart
                return;
            }

            p1PowerupManager.RollRandomPowerup(powerupTier);   // NEW: pass tier
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }

        if (other.gameObject.CompareTag("Player2"))
        {
            PowerupsManager p2PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p2PowerupManager == null)
            {
                return;
            }

            p2PowerupManager.RollRandomPowerup(powerupTier);   // NEW: pass tier
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }
    }
}
