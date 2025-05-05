using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class PlayerInputManager : MonoBehaviour
{
    [Header("Cart References")]
    [SerializeField] private GameObject player1; // Drag from scene
    [SerializeField] private GameObject player2; // Drag from scene

    public void PairGamepad1WithPlayer1()
    {
        var allGamepads = Gamepad.all;

        if (allGamepads.Count < 2)
        {
            Debug.LogError("Two gamepads are required for this test.");
            return;
        }

        player1.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithDevice(allGamepads[0]);
    }
    public void PairGamepad2WithPlayer2()
    {
        var allGamepads = Gamepad.all;

        if (allGamepads.Count < 2)
        {
            Debug.LogError("Two gamepads are required for this test.");
            return;
        }

        player2.transform.GetChild(0).GetChild(3).GetComponent<CartControlScript>().InitializeWithDevice(allGamepads[1]);
    }
}
