using UnityEngine;

public class PlayerIndictor : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private bool isForPlayer1 = true;

    public Transform player1;        // The player this indicator follows (e.g. Player 1)
    public Transform player2;        // The target player (e.g. Player 2)
    public float distanceFromPlayer = 2f; // How far away from Player A it should appear

    private void Start()
    {
        player1 = GameObject.FindGameObjectWithTag("Player1").transform;

        player2 = GameObject.FindGameObjectWithTag("Player2").transform;
    }

    void Update()
    {
        if (player1 == null || player2 == null) return;

        if (isForPlayer1)
        {
            Vector3 direction = (player2.position - player1.position).normalized;
            Vector3 offset = direction * distanceFromPlayer;
            transform.position = player1.position + offset;

            // Optional: face the direction of movement or look at player B
            transform.forward = direction;
        }
        else
        {
            Vector3 direction = (player1.position - player2.position).normalized;
            Vector3 offset = direction * distanceFromPlayer;
            transform.position = player2.position + offset;

            // Optional: face the direction of movement or look at player B
            transform.forward = direction;
        }
    }
}
