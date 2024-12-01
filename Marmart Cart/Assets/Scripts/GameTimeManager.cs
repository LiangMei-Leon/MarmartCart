using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;

public class GameTimeManager : MonoBehaviour
{
    [Header("Game Time Settings")]
    [SerializeField] private float totalGameDuration = 180f; // Total game time in seconds
    private float elapsedTime = 0f;
    private bool gameEnded = false;
    private bool gamePaused = true;

    [Header("UI References")]
    [SerializeField] private GameObject titleScreen; // Title screen object
    [SerializeField] private TextMeshProUGUI timerText; // TMP for time display
    [SerializeField] private GameObject finalScoreScreen; // Final score screen object
//     [SerializeField] private TextMeshProUGUI finalHitCountText; // TMP for hit count display
//     [SerializeField] private TextMeshProUGUI finalScoreText; // TMP for final score display
//     [SerializeField] private TextMeshProUGUI normalItemText; // TMP for normal item count
//     [SerializeField] private TextMeshProUGUI bonusItemText; // TMP for bonus item count
    [SerializeField] private TextMeshProUGUI scoreBreakdownText; // TMP for score breakdown

    private int normalItemCount = 0;
    private int bonusItemCount = 0;
    private int finalScore = 0;

    [Header("Points System")]
    [SerializeField] private int pointsPerHit = 5;
    [SerializeField] private int pointsPerNormalItem = 20;
    [SerializeField] private int pointsPerBonusItem = 30;

    private int hitCount = 0;
    [SerializeField] private TextMeshProUGUI hitCountText; // TMP for hit count display

    private int cartCount = 0;
    [SerializeField] private TextMeshProUGUI cartCountText; // TMP for hit count display
    [SerializeField] private SnakeCartManager snakeCartManager;

    private bool isAnimatingHitCount = false; // Flag for hit count text animation
    private bool isAnimatingCartCount = false; // Flag for cart count text animation

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float defaultOrthographicSize = 14f;
    [SerializeField] private float orthographicSizeIncrement = 0.5f;
    [SerializeField] private int cartsPerZoomIncrement = 5;
    [SerializeField] private float maxOrthographicSize = 20f;

    [Header("Music Settings")]
    [SerializeField] private MusicManager musicManager;

    private void Start()
    {
        // Pause game and show title screen at the beginning
        PauseGame();
        titleScreen.SetActive(true);
        hitCountText.text = "0";
        cartCountText.text = "0";
        // Initialize camera
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.OrthographicSize = defaultOrthographicSize;
        }
    }

    private void Update()
    {
        if (gamePaused)
        {
            // Check for game start input while paused
            if (Input.GetButtonDown("Submit")) // "Submit" is mapped to "A" by default in the new Input System
            {
                StartGame();
            }
            return;
        }

        // Increment elapsed time
        elapsedTime += Time.deltaTime;

        // Update timer display
        UpdateTimerDisplay();

        int currentCartCount = snakeCartManager.GetSnakeBodyLength();
        if (currentCartCount != cartCount)
        {
            cartCount = currentCartCount;
            cartCountText.text = cartCount.ToString();
            StartCoroutine(AnimateText(cartCountText, false));
            UpdateCameraZoom();
        }

        // Check if game time has ended
        if (elapsedTime >= totalGameDuration)
        {
            EndGame();
        }
    }

    private void UpdateTimerDisplay()
    {
        float timeRemaining = Mathf.Max(0, totalGameDuration - elapsedTime);
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        timerText.text = $"{minutes:D2}:{seconds:D2}";
    }

    private void StartGame()
    {
        // Unpause the game and hide title screen
        gamePaused = false;
        titleScreen.SetActive(false);
        ResumeGame();
        musicManager.PlayMusic("BackgroundMusic");
    }

    private void EndGame()
    {
        gameEnded = true;
        musicManager.StopMusic();
        PauseGame();
        Debug.Log("Game Over!");

        // Calculate the final score
        CalculateFinalScore();

        // Show final score screen
        finalScoreScreen.SetActive(true);
        DisplayScoreBreakdown();
    }

    private void PauseGame()
    {
        Time.timeScale = 0f;
        gamePaused = true;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        gamePaused = false;
    }

    public void IncreaseHitCount()
    {
        hitCount++;
        hitCountText.text = hitCount.ToString();
        StartCoroutine(AnimateText(hitCountText, true));
    }

    private IEnumerator AnimateText(TextMeshProUGUI text, bool isHitCount)
    {
        // Determine which flag to check and update
        bool isAnimating = isHitCount ? isAnimatingHitCount : isAnimatingCartCount;
        if (isAnimating) yield break; // Exit if this text's animation is already in progress

        if (isHitCount)
            isAnimatingHitCount = true;
        else
            isAnimatingCartCount = true;

        float animationDuration = 0.3f; // Total animation time
        float scaleUpDuration = animationDuration / 2; // Time for scaling up
        float scaleDownDuration = animationDuration / 2; // Time for scaling down
        Vector3 originalScale = text.transform.localScale;

        // Scale up
        float elapsed = 0f;
        while (elapsed < scaleUpDuration)
        {
            float t = elapsed / scaleUpDuration;
            text.transform.localScale = Vector3.Lerp(originalScale, originalScale * 2f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Scale down
        elapsed = 0f;
        while (elapsed < scaleDownDuration)
        {
            float t = elapsed / scaleDownDuration;
            text.transform.localScale = Vector3.Lerp(originalScale * 2f, originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        text.transform.localScale = originalScale; // Ensure the scale resets

        // Reset the corresponding flag
        if (isHitCount)
            isAnimatingHitCount = false;
        else
            isAnimatingCartCount = false;
    }
    private void UpdateCameraZoom()
    {
        if (cinemachineCamera == null) return;

        // Calculate new orthographic size based on cart count
        float newOrthographicSize = defaultOrthographicSize + (cartCount / cartsPerZoomIncrement) * orthographicSizeIncrement;
        newOrthographicSize = Mathf.Clamp(newOrthographicSize, defaultOrthographicSize, maxOrthographicSize);

        // Apply the new orthographic size
        cinemachineCamera.Lens.OrthographicSize = newOrthographicSize;
    }
    private void CalculateFinalScore()
    {
        normalItemCount = 0;
        bonusItemCount = 0;

        // Count items in the snake body
        foreach (var cart in snakeCartManager.GetSnakeBody())
        {
            var cartManager = cart.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                if (cartManager.isBonusCart)
                    bonusItemCount++;
                else
                    normalItemCount++;
            }
        }

        finalScore = (hitCount * pointsPerHit) + (normalItemCount * pointsPerNormalItem) + (bonusItemCount * pointsPerBonusItem);
    }
    private void DisplayScoreBreakdown()
    {
//         hitCountText.text = $"Hits: {hitCount}";
//         normalItemText.text = $"Normal Items: {normalItemCount}";
//         bonusItemText.text = $"Bonus Items: {bonusItemCount}";
//         finalScoreText.text = $"Final Score: {finalScore}";

        scoreBreakdownText.text =
            $"Score Breakdown:\n" +
            $"{hitCount} x {pointsPerHit} (Hits) = {hitCount * pointsPerHit}\n" +
            $"{normalItemCount} x {pointsPerNormalItem} (Normal Items) = {normalItemCount * pointsPerNormalItem}\n" +
            $"{bonusItemCount} x {pointsPerBonusItem} (Bonus Items) = {bonusItemCount * pointsPerBonusItem}\n" +
            $"Total Score: {finalScore}";
    }
}