using UnityEngine;
using UnityEngine.UI;

public class CartControlPromptManager : MonoBehaviour
{
    [SerializeField] CartControlScript cartController;

    [Header("Raycast Settings")]
    [SerializeField] GameObject flipPrompt;
    [Header("UI References")]
    [SerializeField] private Slider speedUpSlider;
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

        // Update speed-up meter
        speedUpSlider.value = cartController.GetSpeedUpMeter();
    }
}
