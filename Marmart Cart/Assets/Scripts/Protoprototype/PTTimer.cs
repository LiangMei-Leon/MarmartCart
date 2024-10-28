using UnityEngine;
using TMPro;
public class PTTimer : MonoBehaviour
{
    public TextMeshProUGUI timerText; // UI element to display the timer
    public PTObjectCollect objectCollect; // Reference to the PTObjectCollect script

    private float timer = 0f;
    private bool timerActive = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !timerActive)
        {
            timerActive = true; // Start the timer
            timer = 0f; // Reset timer
        }
    }

    private void Update()
    {
        if (timerActive && !objectCollect.AllCollected())
        {
            timer += Time.deltaTime; // Increment timer
            timerText.text = "Time: " + timer.ToString("F2"); // Display timer with 2 decimal places
        }
        else if (timerActive && objectCollect.AllCollected())
        {
            timerActive = false; // Stop the timer
        }
    }
}
