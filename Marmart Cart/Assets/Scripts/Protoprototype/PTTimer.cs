using UnityEngine;
using TMPro;
public class PTTimer : MonoBehaviour
{
    public TextMeshProUGUI timerText; // UI element to display the timer
    private float score = 0f;
    public TextMeshProUGUI scoreText;
    public PTObjectCollect objectCollect; // Reference to the PTObjectCollect script

    private float timer = 180f;
    private bool timerActive = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !timerActive)
        {
            timerActive = true; // Start the timer
        }
    }

    private void Update()
    {
        if (timerActive)
        {
            // Decrease timer
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                timer = 0;
                timerActive = false;
                PauseGame(); // Pause the game when the timer reaches 0
            }
            // Display the timer in "minutes:seconds" format
            int minutes = Mathf.FloorToInt(timer / 60f);
            int seconds = Mathf.FloorToInt(timer % 60f);
            timerText.text = $"Time: {minutes:0}:{seconds:00}";
        }
        else if (timerActive && objectCollect.AllCollected())
        {
            timerActive = false; // Stop the timer
        }
    }
    private void PauseGame()
    {
        Time.timeScale = 0f; // Pause the game
        timerText.text = "Time: 0:00"; // Display timer as 0:00
    }
    public void AddScore(float amount)
    {
        score += amount;
        scoreText.text = "Score: " + score.ToString();
    }
}
