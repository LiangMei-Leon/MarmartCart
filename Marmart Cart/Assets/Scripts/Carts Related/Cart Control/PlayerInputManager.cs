using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class PlayerInputManager : MonoBehaviour
{
    [Header("Cart References")]
    [SerializeField] private GameObject player1; // Drag from scene
    [SerializeField] private GameObject player2; // Drag from scene

    [SerializeField] private bool enableDebugMode = true; // expose to Inspector

    public void SetupPlayers()
    {
        var gamepads = Gamepad.all;

        if (gamepads.Count >= 2)
        {
            player1.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithDevice(gamepads[0]);
            player2.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithDevice(gamepads[1]);
        }
        else if (gamepads.Count == 1)
        {
            player1.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithDevice(gamepads[0]);

            if (enableDebugMode)
            {
                player2.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithKeyboard();
            }
            else
            {
                Debug.LogWarning("Only one gamepad detected. Player 2 inactive.");
            }
        }
        else if (enableDebugMode)
        {
            player1.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithKeyboard();
            player2.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithKeyboard(); // Optional
        }
        else
        {
            Debug.LogError("No gamepads found, and debug mode is off.");
        }
    }
}
