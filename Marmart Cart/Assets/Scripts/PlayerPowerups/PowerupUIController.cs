using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerupUIController : MonoBehaviour
{
    [SerializeField] private PowerupsManager powerupManager;
    [SerializeField] private TextMeshProUGUI powerupText;

    [Header("Powerup Icons")]
    [SerializeField] private GameObject noPowerupIcon;
    [SerializeField] private GameObject boostIcon;
    [SerializeField] private GameObject scaleIcon;
    [SerializeField] private GameObject projectileIcon;

    void Update()
    {
        if (powerupManager == null)
            return;

        var current = powerupManager.GetCurrentPowerup();

        // Hide all icons before showing the active one
        SetAllIconsActive(false);

        string displayName = "NO POWERUPS";

        if (current != null)
        {
            switch (current)
            {
                case PowerupsManager.PowerupType.Boost:
                    displayName = "UNSTOPPABLE CHARGE";
                    SetIconActive(boostIcon, true);
                    break;

                case PowerupsManager.PowerupType.Scale:
                    displayName = "GIANT CARTS";
                    SetIconActive(scaleIcon, true);
                    break;

                case PowerupsManager.PowerupType.Projectile:
                    displayName = "BOWLING STRIKE";
                    SetIconActive(projectileIcon, true);
                    break;

                default:
                    SetIconActive(noPowerupIcon, true);
                    break;
            }
        }
        else
        {
            // Explicit no-powerup state
            SetIconActive(noPowerupIcon, true);
        }

        if (powerupText != null)
            powerupText.text = displayName;
    }

    private void SetAllIconsActive(bool state)
    {
        if (noPowerupIcon != null) noPowerupIcon.gameObject.SetActive(state);
        if (boostIcon != null) boostIcon.gameObject.SetActive(state);
        if (scaleIcon != null) scaleIcon.gameObject.SetActive(state);
        if (projectileIcon != null) projectileIcon.gameObject.SetActive(state);
    }

    private void SetIconActive(GameObject icon, bool state)
    {
        if (icon != null)
            icon.gameObject.SetActive(state);
    }
}