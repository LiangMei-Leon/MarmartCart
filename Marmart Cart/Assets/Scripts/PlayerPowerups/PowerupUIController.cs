using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerupUIController : MonoBehaviour
{
    [SerializeField] private PowerupsManager powerupManager;
    [SerializeField] private TextMeshProUGUI powerupText;

    void Update()
    {
        if (powerupManager == null || powerupText == null)
        {
            return;
        }

        string displayName = "NO POWERUPS";

        var current = powerupManager.GetCurrentPowerup();

        if (current != null)
        {
            switch (current)
            {
                case PowerupsManager.PowerupType.Boost:
                displayName = "ULTIMATE CHARGE";
                break;
                case PowerupsManager.PowerupType.Scale:
                displayName = "GIANT CARTS";
                break;
                case PowerupsManager.PowerupType.Projectile:
                displayName = "DEADLY STONE";
                break;
                default:
                displayName = "NO POWERUPS";
                break;
            }
        }

        powerupText.text = displayName;
    }
}