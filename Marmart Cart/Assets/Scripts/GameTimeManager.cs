using UnityEngine;
using TMPro;

public class GameTimeManager : MonoBehaviour
{
    [Header("Game Time Settings")]
    [SerializeField] private float totalGameDuration = 180f; // Total game time in seconds
    private float elapsedTime = 0f;
    private bool gameEnded = false;


    private void Update()
    {
        if (gameEnded) return;

        // Increment time
        elapsedTime += Time.deltaTime;

        // Check for game end
        if (elapsedTime >= totalGameDuration)
        {
            EndGame();
        }
    }


    private void EndGame()
    {
        gameEnded = true;
        Debug.Log("Game Over!");
    }
}