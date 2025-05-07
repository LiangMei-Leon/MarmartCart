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
    [SerializeField] private TextMeshProUGUI scoreBreakdownTextP1; // TMP for score breakdown
    [SerializeField] private TextMeshProUGUI scoreBreakdownTextP2; // TMP for score breakdown

    private int normalItemCountP1 = 0;
    private int bonusItemCountP1 = 0;
    private int finalScoreP1 = 0;
    private int normalItemCountP2 = 0;
    private int bonusItemCountP2 = 0;
    private int finalScoreP2 = 0;

    [Header("Points System")]
    [SerializeField] private int pointsPerHit = 5;
    [SerializeField] private int pointsPerNormalItem = 20;
    [SerializeField] private int pointsPerBonusItem = 30;


    [Header("Player Carts")]
    //Player 1
    [SerializeField] private SnakeCartManager snakeCartManagerP1;
    private int hitCountP1 = 0;
    [SerializeField] private TextMeshProUGUI hitCountP1Text; // TMP for hit count display
    private int cartCountP1 = 0;
    [SerializeField] private TextMeshProUGUI cartCountP1Text; // TMP for hit count display

    //Player 2
    [SerializeField] private SnakeCartManager snakeCartManagerP2;
    private int hitCountP2 = 0;
    [SerializeField] private TextMeshProUGUI hitCountP2Text; // TMP for hit count display
    private int cartCountP2 = 0;
    [SerializeField] private TextMeshProUGUI cartCountP2Text; // TMP for hit count display

    private bool isAnimatingHitCount = false; // Flag for hit count text animation
    private bool isAnimatingCartCount = false; // Flag for cart count text animation

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera cinemachineCameraP1;
    [SerializeField] private CinemachineCamera cinemachineCameraP2;
    [SerializeField] private float defaultOrthographicSize = 16f;
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
        hitCountP1Text.text = "0";
        cartCountP1Text.text = "0";
        hitCountP2Text.text = "0";
        cartCountP2Text.text = "0";
        // Initialize camera zoom
        if (cinemachineCameraP1 != null)
        {
            cinemachineCameraP1.Lens.OrthographicSize = defaultOrthographicSize;
        }
        if (cinemachineCameraP2 != null)
        {
            cinemachineCameraP2.Lens.OrthographicSize = defaultOrthographicSize;
        }
    }

    private void Update()
    {
        //Start the game from the title screen
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
        //Update Player1 Cart Count display and adjust camera
        int currentCartCountP1 = snakeCartManagerP1.GetSnakeBodyLength();
        if (currentCartCountP1 != cartCountP1)
        {
            cartCountP1 = currentCartCountP1;
            cartCountP1Text.text = cartCountP1.ToString();
            StartCoroutine(AnimateText(cartCountP1Text, false));
            UpdateCameraZoomP1();
        }
        //Update Player2 Cart Count display and adjust camera
        int currentCartCountP2 = snakeCartManagerP2.GetSnakeBodyLength();
        if (currentCartCountP2 != cartCountP2)
        {
            cartCountP2 = currentCartCountP2;
            cartCountP2Text.text = cartCountP2.ToString();
            StartCoroutine(AnimateText(cartCountP2Text, false));
            UpdateCameraZoomP2();
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
        //Debug.Log("Game Over!");

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

    public void IncreaseHitCount(int playerIndex)
    {
        if(playerIndex == 1)
        {
            hitCountP1++;
            hitCountP1Text.text = hitCountP1.ToString();
            StartCoroutine(AnimateText(hitCountP1Text, true));
        }
        else if(playerIndex == 2)
        {
            hitCountP2++;
            hitCountP2Text.text = hitCountP2.ToString();
            StartCoroutine(AnimateText(hitCountP2Text, true));
        }
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
    private void UpdateCameraZoomP1()
    {
        if (cinemachineCameraP1 == null) return;

        // Calculate new orthographic size based on cart count
        float newOrthographicSize = defaultOrthographicSize + (cartCountP1 / cartsPerZoomIncrement) * orthographicSizeIncrement;
        newOrthographicSize = Mathf.Clamp(newOrthographicSize, defaultOrthographicSize, maxOrthographicSize);

        // Apply the new orthographic size
        cinemachineCameraP1.Lens.OrthographicSize = newOrthographicSize;
    }
    private void UpdateCameraZoomP2()
    {
        if (cinemachineCameraP2 == null) return;

        // Calculate new orthographic size based on cart count
        float newOrthographicSize = defaultOrthographicSize + (cartCountP2 / cartsPerZoomIncrement) * orthographicSizeIncrement;
        newOrthographicSize = Mathf.Clamp(newOrthographicSize, defaultOrthographicSize, maxOrthographicSize);

        // Apply the new orthographic size
        cinemachineCameraP2.Lens.OrthographicSize = newOrthographicSize;
    }
    private void CalculateFinalScore()
    {
        normalItemCountP1 = 0;
        bonusItemCountP1 = 0;
        normalItemCountP2 = 0;
        bonusItemCountP2 = 0;
        // Count items in the snake body player 1
        foreach (var cart in snakeCartManagerP1.GetSnakeBody())
        {
            var cartManager = cart.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                if (cartManager.isBonusCart)
                    bonusItemCountP1++;
                else
                    normalItemCountP1++;
            }
        }

        // Count items in the snake body player 2
        foreach (var cart in snakeCartManagerP2.GetSnakeBody())
        {
            var cartManager = cart.GetComponent<ChainedCartManager>();
            if (cartManager != null)
            {
                if (cartManager.isBonusCart)
                    bonusItemCountP2++;
                else
                    normalItemCountP2++;
            }
        }

        finalScoreP1 = (hitCountP1 * pointsPerHit) + (normalItemCountP1 * pointsPerNormalItem) + (bonusItemCountP1 * pointsPerBonusItem);
        finalScoreP2 = (hitCountP2 * pointsPerHit) + (normalItemCountP2 * pointsPerNormalItem) + (bonusItemCountP2 * pointsPerBonusItem);
    }
    private void DisplayScoreBreakdown()
    {
        //         hitCountText.text = $"Hits: {hitCount}";
        //         normalItemText.text = $"Normal Items: {normalItemCount}";
        //         bonusItemText.text = $"Bonus Items: {bonusItemCount}";
        //         finalScoreText.text = $"Final Score: {finalScore}";
        scoreBreakdownTextP1.text =
            $"Score Breakdown:\n" +
            $"{hitCountP1} x {pointsPerHit} (Hits) = {hitCountP1 * pointsPerHit}\n" +
            $"{normalItemCountP1} x {pointsPerNormalItem} (Normal Items) = {normalItemCountP1 * pointsPerNormalItem}\n" +
            $"{bonusItemCountP1} x {pointsPerBonusItem} (Bonus Items) = {bonusItemCountP1 * pointsPerBonusItem}\n" +
            $"Player1 Score: {finalScoreP1}";
        scoreBreakdownTextP2.text =
            $"Score Breakdown:\n" +
            $"{hitCountP2} x {pointsPerHit} (Hits) = {hitCountP2 * pointsPerHit}\n" +
            $"{normalItemCountP2} x {pointsPerNormalItem} (Normal Items) = {normalItemCountP2 * pointsPerNormalItem}\n" +
            $"{bonusItemCountP2} x {pointsPerBonusItem} (Bonus Items) = {bonusItemCountP2 * pointsPerBonusItem}\n" +
            $"Player2 Score: {finalScoreP2}";

        scoreBreakdownText.text =
            $"Total Score: {finalScoreP1 + finalScoreP2}\n" +
            "Thanks For Shopping!";
    }
}