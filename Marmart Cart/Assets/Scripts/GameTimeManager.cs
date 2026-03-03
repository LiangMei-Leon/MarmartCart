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
    [SerializeField] private TextMeshProUGUI timerTextP1; // TMP for time display p1
    [SerializeField] private TextMeshProUGUI timerTextP2; // TMP for time display p2
    [SerializeField] private TextMeshProUGUI timerTextP3; // TMP for time display p3
    [SerializeField] private TextMeshProUGUI timerTextP4; // TMP for time display p4
    [SerializeField] private GameObject finalScoreScreen; // Final score screen object
    //[SerializeField] private TextMeshProUGUI finalHitCountText; // TMP for hit count display
    //[SerializeField] private TextMeshProUGUI finalScoreText; // TMP for final score display
    //[SerializeField] private TextMeshProUGUI normalItemText; // TMP for normal item count
    //[SerializeField] private TextMeshProUGUI bonusItemText; // TMP for bonus item count
    //[SerializeField] private TextMeshProUGUI scoreBreakdownText; // TMP for score breakdown
    [SerializeField] private TextMeshProUGUI scoreBreakdownTextP1; // TMP for score breakdown
    [SerializeField] private TextMeshProUGUI scoreBreakdownTextP2; // TMP for score breakdown
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private GameObject p1WinResult;
    [SerializeField] private GameObject p2WinResult;

    //private int normalItemCountP1 = 0;
    //private int bonusItemCountP1 = 0;
    //private int finalScoreP1 = 0;
    //private int normalItemCountP2 = 0;
    //private int bonusItemCountP2 = 0;
    //private int finalScoreP2 = 0;

    // obselete, points now calculated in CashScoreManager
    //[Header("Points System")]
    //[SerializeField] private int pointsPerHit = 5;
    //[SerializeField] private int pointsPerNormalItem = 20;
    //[SerializeField] private int pointsPerBonusItem = 30;
    //[SerializeField] private MartConditionManager martConditionManager;

    [Header("Player Carts")]
    //Player 1
    [SerializeField] private SnakeCartManager snakeCartManagerP1;
    private int cartCountP1 = 0;
    private int itemCartsP1 = 0;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP1Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP1Text;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP1TextFor4pMode;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP1TextFor4pMode;
    [SerializeField] private TextMeshProUGUI finalRefToCurrentTotalCartCountP1Text;
    [SerializeField] private TextMeshProUGUI finalRefToCurrentItemCartCountP1Text;

    //Player 2
    [SerializeField] private SnakeCartManager snakeCartManagerP2;
    private int cartCountP2 = 0;
    private int itemCartsP2 = 0;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP2Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP2Text;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP2TextFor4pMode;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP2TextFor4pMode;
    [SerializeField] private TextMeshProUGUI finalRefToCurrentTotalCartCountP2Text;
    [SerializeField] private TextMeshProUGUI finalRefToCurrentItemCartCountP2Text;

    //Player 3
    [SerializeField] private SnakeCartManager snakeCartManagerP3;
    private int cartCountP3 = 0;
    private int itemCartsP3 = 0;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP3Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP3Text;

    //Player 4
    [SerializeField] private SnakeCartManager snakeCartManagerP4;
    private int cartCountP4 = 0;
    private int itemCartsP4 = 0;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP4Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP4Text;

    private bool isAnimatingHitCount = false; // Flag for hit count text animation
    private bool isAnimatingCartCount = false; // Flag for cart count text animation

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera cinemachineCameraP1;
    [SerializeField] private CinemachineCamera cinemachineCameraP2;
    [SerializeField] private CinemachineCamera cinemachineCameraP3;
    [SerializeField] private CinemachineCamera cinemachineCameraP4;
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
        //Switch text reference for player 1&2 depending on which game mode they are in (player 3&4 would only be in 4 player mode)
        if (GMode.Instance.PlayerCount() == 2)
        {
            // p1
            finalRefToCurrentTotalCartCountP1Text = currentTotalCartCountP1Text;
            finalRefToCurrentItemCartCountP1Text = currentItemCartCountP1Text;
            // p2
            finalRefToCurrentTotalCartCountP2Text = currentTotalCartCountP2Text;
            finalRefToCurrentItemCartCountP2Text = currentItemCartCountP2Text;
        }
        else if (GMode.Instance.PlayerCount() == 4)
        {
            // p1
            finalRefToCurrentTotalCartCountP1Text = currentTotalCartCountP1TextFor4pMode;
            finalRefToCurrentItemCartCountP1Text = currentItemCartCountP1TextFor4pMode;
            // p2
            finalRefToCurrentTotalCartCountP2Text = currentTotalCartCountP2TextFor4pMode;
            finalRefToCurrentItemCartCountP2Text = currentItemCartCountP2TextFor4pMode;
        }

        finalRefToCurrentTotalCartCountP1Text.text = "0";
        finalRefToCurrentItemCartCountP1Text.text = "0";
        finalRefToCurrentTotalCartCountP2Text.text = "0";
        finalRefToCurrentItemCartCountP2Text.text = "0";
        currentTotalCartCountP3Text.text = "0";
        currentItemCartCountP3Text.text = "0";
        currentTotalCartCountP4Text.text = "0";
        currentItemCartCountP4Text.text = "0";

        // Initialize camera zoom
        if (cinemachineCameraP1 != null)
        {
            cinemachineCameraP1.Lens.OrthographicSize = defaultOrthographicSize;
        }
        if (cinemachineCameraP2 != null)
        {
            cinemachineCameraP2.Lens.OrthographicSize = defaultOrthographicSize;
        }
        if (cinemachineCameraP3 != null)
        {
            cinemachineCameraP3.Lens.OrthographicSize = defaultOrthographicSize;
        }
        if (cinemachineCameraP4 != null)
        {
            cinemachineCameraP4.Lens.OrthographicSize = defaultOrthographicSize;
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
        int newTotalP1 = snakeCartManagerP1.GetSnakeBodyLength() - 1;
        int newItemCartsP1 = snakeCartManagerP1.GetCurrentNumOfCartsWithItem();

        if (newTotalP1 != cartCountP1 || itemCartsP1 != newItemCartsP1)
        {
            cartCountP1 = newTotalP1;
            itemCartsP1 = newItemCartsP1;

            finalRefToCurrentTotalCartCountP1Text.text = cartCountP1.ToString();
            finalRefToCurrentItemCartCountP1Text.text = itemCartsP1.ToString();
            //StartCoroutine(AnimateText(currentTotalCartCountP1Text, false));
            StartCoroutine(AnimateText(finalRefToCurrentItemCartCountP1Text, false));
            UpdateCameraZoomP1();
        }

        //Update Player2 Cart Count display and adjust camera
        int newTotalP2 = snakeCartManagerP2.GetSnakeBodyLength() - 1;
        int newItemCartsP2 = snakeCartManagerP2.GetCurrentNumOfCartsWithItem();

        if (newTotalP2 != cartCountP2 || itemCartsP2 != newItemCartsP2)
        {
            cartCountP2 = newTotalP2;
            itemCartsP2 = newItemCartsP2;

            finalRefToCurrentTotalCartCountP2Text.text = cartCountP2.ToString();
            finalRefToCurrentItemCartCountP2Text.text = itemCartsP2.ToString();
            //StartCoroutine(AnimateText(currentTotalCartCountP2Text, false));
            StartCoroutine(AnimateText(finalRefToCurrentItemCartCountP2Text, false));
            UpdateCameraZoomP2();
        }

        //Update Player1 Cart Count display and adjust camera
        int newTotalP3 = snakeCartManagerP3.GetSnakeBodyLength() - 1;
        int newItemCartsP3 = snakeCartManagerP3.GetCurrentNumOfCartsWithItem();

        if (newTotalP3 != cartCountP3 || itemCartsP3 != newItemCartsP3)
        {
            cartCountP3 = newTotalP3;
            itemCartsP3 = newItemCartsP3;

            currentTotalCartCountP3Text.text = cartCountP3.ToString();
            currentItemCartCountP3Text.text = itemCartsP3.ToString();
            //StartCoroutine(AnimateText(currentTotalCartCountP3Text, false));
            StartCoroutine(AnimateText(currentItemCartCountP3Text, false));
            UpdateCameraZoomP3();
        }

        //Update Player4 Cart Count display and adjust camera
        int newTotalP4 = snakeCartManagerP4.GetSnakeBodyLength() - 1;
        int newItemCartsP4 = snakeCartManagerP4.GetCurrentNumOfCartsWithItem();

        if (newTotalP4 != cartCountP4 || itemCartsP4 != newItemCartsP4)
        {
            cartCountP4 = newTotalP4;
            itemCartsP4 = newItemCartsP4;

            currentTotalCartCountP4Text.text = cartCountP4.ToString();
            currentItemCartCountP4Text.text = itemCartsP4.ToString();
            //StartCoroutine(AnimateText(currentTotalCartCountP1Text, false));
            StartCoroutine(AnimateText(currentItemCartCountP4Text, false));
            UpdateCameraZoomP4();
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
        timerTextP1.text = $"{minutes:D2}:{seconds:D2}";
        timerTextP2.text = $"{minutes:D2}:{seconds:D2}";
        timerTextP3.text = $"{minutes:D2}:{seconds:D2}";
        timerTextP4.text = $"{minutes:D2}:{seconds:D2}";
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
        //CalculateFinalScore();

        // Show final score screen
        //scoreBreakdownTextP1.text = cashScoreManager.p1TotalScore.ToString();
        //scoreBreakdownTextP2.text = cashScoreManager.p2TotalScore.ToString();
        //finalScoreScreen.SetActive(true);
        //if(cashScoreManager.p1TotalScore > cashScoreManager.p2TotalScore)
        //{
        //    p1WinResult.SetActive(true);
        //    p2WinResult.SetActive(false);
        //}
        //else if(cashScoreManager.p2TotalScore > cashScoreManager.p1TotalScore)
        //{
        //    p1WinResult.SetActive(false);
        //    p2WinResult.SetActive(true);
        //}
        //DisplayScoreBreakdown();
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

    //public void IncreaseHitCount(int playerIndex)
    //{
    //    if(playerIndex == 1)
    //    {
    //        hitCountP1++;
    //        hitCountP1Text.text = hitCountP1.ToString();
    //        StartCoroutine(AnimateText(hitCountP1Text, true));
    //    }
    //    else if(playerIndex == 2)
    //    {
    //        hitCountP2++;
    //        hitCountP2Text.text = hitCountP2.ToString();
    //        StartCoroutine(AnimateText(hitCountP2Text, true));
    //    }
    //}

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
    private void UpdateCameraZoomP3()
    {
        if (cinemachineCameraP3 == null) return;

        // Calculate new orthographic size based on cart count
        float newOrthographicSize = defaultOrthographicSize + (cartCountP3 / cartsPerZoomIncrement) * orthographicSizeIncrement;
        newOrthographicSize = Mathf.Clamp(newOrthographicSize, defaultOrthographicSize, maxOrthographicSize);

        // Apply the new orthographic size
        cinemachineCameraP3.Lens.OrthographicSize = newOrthographicSize;
    }
    private void UpdateCameraZoomP4()
    {
        if (cinemachineCameraP4 == null) return;

        // Calculate new orthographic size based on cart count
        float newOrthographicSize = defaultOrthographicSize + (cartCountP4 / cartsPerZoomIncrement) * orthographicSizeIncrement;
        newOrthographicSize = Mathf.Clamp(newOrthographicSize, defaultOrthographicSize, maxOrthographicSize);

        // Apply the new orthographic size
        cinemachineCameraP4.Lens.OrthographicSize = newOrthographicSize;
    }
    //private void CalculateFinalScore()
    //{
    //    normalItemCountP1 = 0;
    //    bonusItemCountP1 = 0;
    //    normalItemCountP2 = 0;
    //    bonusItemCountP2 = 0;
    //    // Count items in the snake body player 1
    //    foreach (var cart in snakeCartManagerP1.GetSnakeBody())
    //    {
    //        var cartManager = cart.GetComponent<ChainedCartManager>();
    //        if (cartManager != null)
    //        {
    //            if (cartManager.isBonusCart)
    //                bonusItemCountP1++;
    //            else
    //                normalItemCountP1++;
    //        }
    //    }

    //    // Count items in the snake body player 2
    //    foreach (var cart in snakeCartManagerP2.GetSnakeBody())
    //    {
    //        var cartManager = cart.GetComponent<ChainedCartManager>();
    //        if (cartManager != null)
    //        {
    //            if (cartManager.isBonusCart)
    //                bonusItemCountP2++;
    //            else
    //                normalItemCountP2++;
    //        }
    //    }

    //    finalScoreP1 = (hitCountP1 * pointsPerHit) + (normalItemCountP1 * pointsPerNormalItem) + (bonusItemCountP1 * pointsPerBonusItem);
    //    finalScoreP2 = (hitCountP2 * pointsPerHit) + (normalItemCountP2 * pointsPerNormalItem) + (bonusItemCountP2 * pointsPerBonusItem);
    //}

    //private void DisplayScoreBreakdown()
    //{
    //    //         hitCountText.text = $"Hits: {hitCount}";
    //    //         normalItemText.text = $"Normal Items: {normalItemCount}";
    //    //         bonusItemText.text = $"Bonus Items: {bonusItemCount}";
    //    //         finalScoreText.text = $"Final Score: {finalScore}";
    //    scoreBreakdownTextP1.text =
    //        $"Score Breakdown:\n" +
    //        $"{hitCountP1} x {pointsPerHit} (Hits) = {hitCountP1 * pointsPerHit}\n" +
    //        $"{normalItemCountP1} x {pointsPerNormalItem} (Normal Items) = {normalItemCountP1 * pointsPerNormalItem}\n" +
    //        $"{bonusItemCountP1} x {pointsPerBonusItem} (Bonus Items) = {bonusItemCountP1 * pointsPerBonusItem}\n" +
    //        $"Player1 Score: {finalScoreP1}";
    //    scoreBreakdownTextP2.text =
    //        $"Score Breakdown:\n" +
    //        $"{hitCountP2} x {pointsPerHit} (Hits) = {hitCountP2 * pointsPerHit}\n" +
    //        $"{normalItemCountP2} x {pointsPerNormalItem} (Normal Items) = {normalItemCountP2 * pointsPerNormalItem}\n" +
    //        $"{bonusItemCountP2} x {pointsPerBonusItem} (Bonus Items) = {bonusItemCountP2 * pointsPerBonusItem}\n" +
    //        $"Player2 Score: {finalScoreP2}";

    //    scoreBreakdownText.text =
    //        $"Total Score: {finalScoreP1 + finalScoreP2 + Mathf.RoundToInt(martConditionManager.percent * 10000)}\n\n" + $"Mart Condition Score: {Mathf.RoundToInt(martConditionManager.percent * 10000)}" +
    //        "Thanks For Protecting the Mart!";
    //}

    public float GetCurrentGameTime()
    {
        return elapsedTime;
    }

    public int GetCurrentGameStage()
    {
        if(elapsedTime >= 0f && elapsedTime <= 60f)
            return 0;
        if (elapsedTime > 60f && elapsedTime <= 120f)
            return 1;
        else /*if (elapsedTime > 120f && elapsedTime <= 180f)*/
            return 2;
    }
}