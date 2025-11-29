using UnityEngine;
using UnityEngine.SceneManagement;

public class Debuggers : MonoBehaviour
{

    [Header("Restart Game")]
    [SerializeField] private bool enableRestartHotkey = true;
    [SerializeField] private KeyCode restartKey = KeyCode.F9;

    [Header("Player Roots (auto-assigned)")]
    [SerializeField] private Transform p1Root;
    [SerializeField] private Transform p2Root;

    [Header("Player Rigidbodies (auto-assigned)")]
    [SerializeField] private Rigidbody p1Rigidbody;
    [SerializeField] private Rigidbody p2Rigidbody;

    [Header("Player CartControl (auto-assigned)")]
    [SerializeField] private CartControlScript p1CartControl;
    [SerializeField] private CartControlScript p2CartControl;

    [Header("Teleport Targets (set in Inspector)")]
    [SerializeField] private Transform p1TeleportPoint;
    [SerializeField] private Transform p2TeleportPoint;

    [Header("Debug Hotkeys")]
    [SerializeField] private KeyCode p1TeleportKey = KeyCode.Alpha1; // 1
    [SerializeField] private KeyCode p2TeleportKey = KeyCode.Alpha2; // 2
    [SerializeField] private KeyCode p1AllowFlipKey = KeyCode.Alpha3; // 3
    [SerializeField] private KeyCode p2AllowFlipKey = KeyCode.Alpha4; // 4

    void Start()
    {
        // Let the scene finish spawning everything first
        Invoke(nameof(RegisterPlayers), 2f);
    }

    private void RegisterPlayers()
    {
        GameObject p1 = GameObject.FindGameObjectWithTag("Player1");
        GameObject p2 = GameObject.FindGameObjectWithTag("Player2");

        if (p1 == null || p2 == null)
        {
            Debug.LogWarning("[Debuggers] Could not find Player1 or Player2 by tag.");
            return;
        }

        p1Root = p1.transform;
        p2Root = p2.transform;

        p1Rigidbody = p1.GetComponentInChildren<Rigidbody>();
        p2Rigidbody = p2.GetComponentInChildren<Rigidbody>();

        p1CartControl = p1.GetComponentInChildren<CartControlScript>();
        p2CartControl = p2.GetComponentInChildren<CartControlScript>();

        if (p1Rigidbody == null || p2Rigidbody == null)
            Debug.LogWarning("[Debuggers] Missing Rigidbody on players.");

        if (p1CartControl == null || p2CartControl == null)
            Debug.LogWarning("[Debuggers] Missing CartControlScript on players.");

        Debug.Log("[Debuggers] Player refs registered.");
    }

    void Update()
    {
        RestartDebug();
        TeleportDebug();
        FlipDebug();
    }
    // ----------------- RESTART GAME -----------------

    void RestartDebug()
    {
        if (!enableRestartHotkey) return;

        if (Input.GetKeyDown(restartKey))
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }

    // ----------------- TELEPORT PLAYERS -----------------

    void TeleportDebug()
    {
        // Teleport Player 1
        if (Input.GetKeyDown(p1TeleportKey))
        {
            if (p1Root != null && p1TeleportPoint != null)
            {
                p1Root.position = p1TeleportPoint.position;
                p1Root.rotation = p1TeleportPoint.rotation;

                if (p1Rigidbody != null)
                {
                    p1Rigidbody.linearVelocity = Vector3.zero;
                    p1Rigidbody.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                Debug.LogWarning("[Debuggers] P1 teleport refs missing.");
            }
        }

        // Teleport Player 2
        if (Input.GetKeyDown(p2TeleportKey))
        {
            if (p2Root != null && p2TeleportPoint != null)
            {
                p2Root.position = p2TeleportPoint.position;
                p2Root.rotation = p2TeleportPoint.rotation;

                if (p2Rigidbody != null)
                {
                    p2Rigidbody.linearVelocity = Vector3.zero;
                    p2Rigidbody.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                Debug.LogWarning("[Debuggers] P2 teleport refs missing.");
            }
        }
    }

    // ----------------- ALLOW FLIP -----------------

    void FlipDebug()
    {
        // Allow P1 flip
        if (Input.GetKeyDown(p1AllowFlipKey))
        {
            if (p1CartControl != null)
            {
                p1CartControl.AllowFlip();
                Debug.Log("[Debuggers] P1 allowed to flip.");
            }
            else
            {
                Debug.LogWarning("[Debuggers] p1CartControl not assigned.");
            }
        }

        // Allow P2 flip
        if (Input.GetKeyDown(p2AllowFlipKey))
        {
            if (p2CartControl != null)
            {
                p2CartControl.AllowFlip();
                Debug.Log("[Debuggers] P2 allowed to flip.");
            }
            else
            {
                Debug.LogWarning("[Debuggers] p2CartControl not assigned.");
            }
        }
    }
}