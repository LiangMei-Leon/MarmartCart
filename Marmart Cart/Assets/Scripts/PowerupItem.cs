using UnityEngine;

public class PowerupItem : MonoBehaviour
{
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private float selfCleanTime = 20f;
    private void Start()
    {
        // Automatically destroy the powerup item after 30 seconds if not collected
        Destroy(this.gameObject, selfCleanTime);
    }
    void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player1"))
        {
            PowerupsManager p1PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p1PowerupManager == null)
            {
                //hit the chained cart instead of the leading cart, do nothing. TODO: improve this detection
                return;
            }
            p1PowerupManager.RollRandomPowerup();
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }

        if(other.gameObject.CompareTag("Player2"))
        {
            PowerupsManager p2PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p2PowerupManager == null)
            {
                //hit the chained cart instead of the leading cart, do nothing. TODO: improve this detection
                return;
            }
            p2PowerupManager.RollRandomPowerup();
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }
    }
}
