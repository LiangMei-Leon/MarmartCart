using UnityEngine;
using UnityEngine.UI;

public class CartControlPromptManager : MonoBehaviour
{
    [SerializeField] CartControlScript cartController;

    [Header("Raycast Settings")]
    [SerializeField] GameObject flipPrompt;
    [Header("Speed-Up UI (Circular Fill)")]
    [Tooltip("Image with 'Filled' type set to Radial360 in the inspector.")]
    [SerializeField] private Image speedUpFillImage;

    [Tooltip("FillAmount value that represents 'full' (e.g. 0.35 if your ring is not a full circle).")]
    [Range(0f, 1f)]
    [SerializeField] private float desiredMaxFill = 0.35f;
    // Update is called once per frame
    void Update()
    {
        if(cartController.GetCanFlip())
        {
            flipPrompt.SetActive(true);
        }
        else
        {
            flipPrompt.SetActive(false);
        }

        // Speed-up circular bar
        if (speedUpFillImage != null)
        {
            // Map GetSpeedUpMeter() from 0-100 to 0-1
            float meter01 = Mathf.Clamp01(cartController.GetSpeedUpMeter()/100);

            // Map 0..1 → 0..desiredMaxFill
            float fill = meter01 * desiredMaxFill;

            speedUpFillImage.fillAmount = fill;
        }
    }
}
