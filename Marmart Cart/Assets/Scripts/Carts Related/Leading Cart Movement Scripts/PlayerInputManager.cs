using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class PlayerInputManager : MonoBehaviour
{
    public enum PlayerMode { TwoPlayers = 2, FourPlayers = 4 }

    [Header("Mode")]
    [SerializeField] private PlayerMode mode = PlayerMode.TwoPlayers;

    [Header("Cart References")]
    [SerializeField] private GameObject player1;
    [SerializeField] private GameObject player2;
    [SerializeField] private GameObject player3;
    [SerializeField] private GameObject player4;

    [Header("Fallback")]
    [SerializeField] private bool enableKeyboardInput = true;

    // If CartControlScript is always at child(0)->child(3), keep this.
    // Otherwise, replace this with GetComponentInChildren<CartControlScript>() or serialize refs directly.
    private CartControlScript GetCartControl(GameObject playerRoot)
    {
        if (!playerRoot) return null;
        return playerRoot.transform.GetChild(0).GetChild(0).GetComponent<CartControlScript>();
    }
    private void Start()
    {
        SetupPlayers();
    }
    public void SetupPlayers()
    {
        int neededPlayers = (int)mode;

        GameObject[] players = new GameObject[] { player1, player2, player3, player4 };

        // Enable only the players we need
        for (int i = 0; i < players.Length; i++)
        {
            if (!players[i]) continue;
            players[i].SetActive(i < neededPlayers);
        }

        var gamepads = Gamepad.all;
        int padCount = gamepads.Count;

        for (int i = 0; i < neededPlayers; i++)
        {
            var p = players[i];
            if (!p)
            {
                Debug.LogError($"Player {i + 1} reference is missing.");
                continue;
            }

            var cart = GetCartControl(p);
            if (!cart)
            {
                Debug.LogError($"CartControlScript not found for Player {i + 1}.");
                continue;
            }

            // Assign gamepad if available for this slot, else keyboard if allowed
            if (i < padCount)
            {
                cart.InitializeWithDevice(gamepads[i]);
            }
            else if (enableKeyboardInput)
            {
                cart.InitializeWithKeyboard(); // shared keyboard input (all keyboard players share it)
            }
            else
            {
                Debug.LogWarning($"Not enough gamepads for Player {i + 1}, and keyboard is disabled. Disabling that player.");
                p.SetActive(false);
            }
        }
    }
}
