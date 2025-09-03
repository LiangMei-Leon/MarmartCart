using UnityEngine;

public class PowerupItem : MonoBehaviour
{
    private void Start()
    {
        // Automatically destroy the powerup item after 20 seconds if not collected
        Destroy(this.gameObject, 20f);
    }
    void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Player1"))
        {
            PowerupsManager p1PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            p1PowerupManager.RollRandomPowerup();
            Destroy(this.gameObject);
        }

        if(other.gameObject.CompareTag("Player2"))
        {
            PowerupsManager p2PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            p2PowerupManager.RollRandomPowerup();
            Destroy(this.gameObject);
        }
    }
}
