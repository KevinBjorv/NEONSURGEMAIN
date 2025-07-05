using UnityEngine;
using Cinemachine;
using System.Collections;
using UnityEngine.Rendering.PostProcessing;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using System.Collections.Generic;
using System;

public class StartMenuManager : MonoBehaviour
{
    [Header("Playtest Mode")]
    [Tooltip("If enabled, the game will skip the start menu and jump straight into play mode.")]
    public bool playtestMode = false;

    [Header("References to Player & Components")]
    public GameObject playerObject; // The Player

    [Header("Optional Cinemachine Setup")]
    public CinemachineVirtualCamera vCamStart;
    public CinemachineVirtualCamera vCamGame;
    [Tooltip("How long the smooth blend or transition lasts.")]
    public float transitionDuration = 1.5f;
    [Tooltip("AudioSource for swoosh/whoosh sound.")]
    public AudioSource swooshAudio;

    [Header("UI References")]
    [Tooltip("The main Canvas that holds all Start Menu elements (including panels). Don't disable this canvas!")]
    public Canvas startMenuCanvas;
    [Tooltip("This is the actual Start Menu panel (child of startMenuCanvas) that you want to disable/enable.")]
    public GameObject startMenuPanel;

    public GameObject backgroundPanel;
    public GameObject storePanel;
    public GameObject settingsPanel;
    public GameObject exitConfirmationPanel;
    public GameObject aboutPanel;

    [Header("Achievements Panel")]
    public GameObject achievementsPanel;
    public Transform achievementsContent;
    public AchievementItemUI itemPrefab;

    [Header("Store Paging System")]
    [Tooltip("List of store pages in a standardized order. Each entry is a separate store page.")]
    public GameObject[] storePages;
    [Tooltip("TMP element to display current page and total pages (e.g., 'Page 1/3').")]
    public TextMeshProUGUI storePageIndicator;
    private int currentStorePage = 0;

    [Header("Tutorial Paging System")]
    [Tooltip("The HOW TO PLAY panel that contains tutorial pages.")]
    public GameObject tutorialPanel;
    [Tooltip("List of HOW TO PLAY pages in a standardized order. Each entry is a separate tutorial page.")]
    public GameObject[] tutorialPages;
    [Tooltip("TMP element to display current tutorial page and total pages (e.g., 'Page 1/3').")]
    public TextMeshProUGUI tutorialPageIndicator;
    private int currentTutorialPage = 0;

    [Header("In-Game UI Manager")]
    [Tooltip("Parent GameObject of all in-game UI (minimap, stamina bar, etc.).")]
    public GameObject UIManager;

    [Header("Initial Square Spawn Settings")]
    [Tooltip("How many squares to spawn initially at game start.")]
    public int initialSquaresAmount = 10;
    [Tooltip("Minimum distance from player for the initial wave (instead of the normal noSpawnRadius).")]
    public float initialNoSpawnRadius = 2f;
    [Tooltip("Maximum distance from player for the initial wave (like an initial spawnRadius).")]
    public float initialSpawnRadius = 70f;
    public float initialSpawnRadiusMobile = 50f;
    [Tooltip("If true, sets 'initialSpawnRadius' to the SpawnManager's normal spawnRadius in Awake().")]
    public bool setSpawnRadiusToSpawnManager = false;

    [Header("Gradual Despawn Settings")]
    [Tooltip("How many entities to check for despawning each frame during the initial cleanup.")]
    public int entitiesToCheckPerFrame = 10;
    [Tooltip("Short delay after transition before starting the gradual despawn (seconds).")]
    public float initialDespawnDelay = 0.2f;

    [Header("Despawn Toggle")]
    [Tooltip("If true, enables despawning after the camera zoom-in animation is done.")]
    public bool enableDespawningAfterZoom = true;

    // Reference to the PauseMenuManager
    [Header("Pause Menu Manager Reference")]
    public PauseMenuManager pauseMenuManager;

    // POST-PROCESSING VIGNETTE
    [Header("Vignette Post-Processing (Built-in)")]
    public PostProcessVolume postProcessVolume;
    [Tooltip("Starting vignette intensity before the zoom begins (e.g., 0.4).")]
    public float initialVignetteIntensity = 0.4f;
    [Tooltip("Ending vignette intensity by the time zoom animation is done (e.g., 0.0).")]
    public float finalVignetteIntensity = 0.0f;
    [Tooltip("Final color for the vignette when the zoom is finished (e.g., black).")]
    public Color finalVignetteColor = Color.black;

    private Vignette vignette;

    // Components to disable/enable on the player
    private PlayerMovement playerMovement;
    private PlayerStamina playerStamina;
    private PlayerHealth playerHealth;
    private WeaponManager weaponManager;
    private GrenadeThrow grenadeThrow;
    private RocketCompanionManager rocketCompanionManager;

    // NEW: XP UI references and connection to SaveDataManager
    [Header("XP Display")]
    [Tooltip("Slider showing XP progress between current and next level.")]
    public Slider xpSlider;
    [Tooltip("Text displayed on the left side of the XP slider showing the current level.")]
    public TextMeshProUGUI currentLevelText;
    [Tooltip("Text displayed on the right side of the XP slider showing the next level.")]
    public TextMeshProUGUI nextLevelText;
    [Tooltip("Text displayed in the middle of the XP slider showing current XP / XP needed for next level.")]
    public TextMeshProUGUI xpProgressText;

    // NEW: Currency Display
    [Header("Currency Display")]
    [Tooltip("Text that displays the player's current currency (formatted with a $ prefix).")]
    public TextMeshProUGUI currencyDisplayTMP;

    // NEW: High Score Display
    [Header("High Score Display")]
    [Tooltip("Text element displaying the high score from SaveDataManager's persistent data.")]
    public TextMeshProUGUI highScoreTMP;

    [Header("Daily Reward UI")]
    [Tooltip("Button used to claim the daily reward.")]
    public Button dailyRewardButton;
    [Tooltip("Optional: Text on the button to show status (e.g., Claim, Cooldown timer).")]
    public TextMeshProUGUI dailyRewardButtonText; // Optional text on the button
    [Tooltip("Optional: GameObject (e.g., an icon) to show when reward is available.")]
    public GameObject dailyRewardNotification; // Optional notification icon

    public TextMeshProUGUI lastScoreTMP;

    private SaveDataManager sdm;
    private DailyRewardManager dailyRewardManager;

    // --- Removed SafeZone Camera Reference ---
    // [Header("SafeZone Camera")]
    // [Tooltip("Reference to the camera that renders safe zones. This will be enabled only after the game is fully loaded.")]
    // public Camera safeZoneCamera;
    // --- End Removed Section ---

    private bool isAnimating = false;

    [Header("Store Level Lock (Page 4)")]
    [Tooltip("Player must be at least this level to unlock page 4.")]
    public int storePage4RequiredLevel = 5;

    [Tooltip("Show this panel over page 4 when locked.")]
    public GameObject storePage4LockedPanel;

    [Header("References")]
    public GameObject difficultyManager;
    public GameObject deathScreenManager;

    public PhoneUIManager phoneUIManager;
    public EmissionPulseManager emissionPulseManager;

    private void Awake()
    {
        dailyRewardManager = DailyRewardManager.Instance;
        if (dailyRewardManager == null)
        {
            Debug.LogError("[StartMenuManager] DailyRewardManager instance not found!");
        }

        if (dailyRewardButton != null)
        {
            // Add listener for the button click
            dailyRewardButton.onClick.AddListener(OnDailyRewardClaim);
            // Set the initial state of the button UI
            UpdateDailyRewardUI();
        }
        else
        {
            Debug.LogWarning("[StartMenuManager] Daily Reward Button not assigned in the inspector.");
        }

        // Ensure ColorBlindFilter is attached to the main camera so that it applies in the main menu as well.
        if (Camera.main != null && Camera.main.GetComponent<ColorBlindFilter>() == null)
        {
            Camera.main.gameObject.AddComponent<ColorBlindFilter>();
        }

        // Get SaveDataManager instance
        sdm = SaveDataManager.Instance;

        // Update XP UI based on saved data
        UpdateXPUI();

        // Update currency display using SaveDataManager's persistent data
        UpdateCurrencyDisplay();

        // Update score displays using SaveDataManager's persistent data
        UpdateHighScoreDisplay();
        UpdateLastScoreDisplay();

        // Grab references to player's scripts
        if (playerObject)
        {
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            playerStamina = playerObject.GetComponent<PlayerStamina>();
            playerHealth = playerObject.GetComponent<PlayerHealth>();
            weaponManager = playerObject.GetComponent<WeaponManager>();
            grenadeThrow = playerObject.GetComponent<GrenadeThrow>();
            rocketCompanionManager = playerObject.GetComponent<RocketCompanionManager>();
        }

        // Disable them initially (freeze player)
        DisablePlayerComponents();

        // Hide sub-panels
        if (storePanel) storePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (exitConfirmationPanel) exitConfirmationPanel.SetActive(false);
        if (tutorialPanel) tutorialPanel.SetActive(false);
        if (achievementsPanel) achievementsPanel.SetActive(false);
        if (aboutPanel) aboutPanel.SetActive(false);

        // Cinemachine priorities
        if (vCamStart) vCamStart.Priority = 20; // higher
        if (vCamGame) vCamGame.Priority = 10;   // lower

        // Ensure the in-game UI is off at start
        if (UIManager) UIManager.SetActive(false);

        // Mark game as not started if using a GameManager on the player
        var gm = playerObject.GetComponent<GameManager>();
        if (gm) gm.gameStarted = false;

        // Spawn initial squares using normal logic, with custom radius range
        if (SpawnManager.Instance != null && playerObject != null && vCamStart != null)
        {
            // 1. Get Lens Settings directly from the Start Menu Virtual Camera (vCamStart)
            var lens = vCamStart.m_Lens; // Get the LensSettings struct
            float orthoSize = lens.OrthographicSize;

            // 2. Determine Aspect Ratio
            // Use the aspect set on the virtual camera's lens if it's valid (non-zero),
            // otherwise fall back to the current screen aspect ratio.
            float aspect = (float)Screen.width / Screen.height;
            if (aspect <= 0.01f) // Check if screen aspect is valid
            {
                if (Camera.main != null)
                {
                    aspect = Camera.main.aspect;
                    Debug.LogWarning($"Screen aspect was invalid, using Camera.main.aspect: {aspect}");
                }
                else
                {
                    Debug.LogError("Cannot get aspect ratio from Screen or Camera.main!");
                    aspect = 16f / 9f; // Last resort fallback
                    Debug.LogWarning($"Falling back to aspect ratio: {aspect}");
                }
            }

            // 3. Calculate World Bounds based on vCamStart's settings
            float initialViewHeight = orthoSize * 2f;
            float initialViewWidth = initialViewHeight * aspect;

            // 4. Determine the center for the bounds check
            // Assuming the start menu camera view should be centered around the player's starting position
            Vector3 spawnCenter = playerObject.transform.position;

            // 5. Apply mobile radius adjustment if needed (adjusts generation circle, not bounds)
            if (phoneUIManager.isMobile) initialSpawnRadius = initialSpawnRadiusMobile;

            // Optional: Log calculated values for debugging
            Debug.Log($"--- Initial Spawn Calculation ---");
            Debug.Log($"vCamStart Ortho Size: {orthoSize}");
            Debug.Log($"Aspect Ratio Used: {aspect} (Raw Lens Aspect: {lens.Aspect}, Screen: {(float)Screen.width / Screen.height})");
            Debug.Log($"Calculated View Width: {initialViewWidth}");
            Debug.Log($"Calculated View Height: {initialViewHeight}");
            Debug.Log($"Spawn/View Center Used: {spawnCenter}");
            Debug.Log($"Player Position at Calc: {playerObject.transform.position}"); // Compare with spawnCenter
            Debug.Log($"Screen Res at Calc: {Screen.width}x{Screen.height}");
            Debug.Log($"---------------------------------");

            // 6. Call SpawnManager with calculated center and bounds
            SpawnManager.Instance.SpawnInitialSquares(
                spawnCenter,         // The center point of the view rectangle
                initialViewWidth,    // The calculated width of the view
                initialViewHeight,   // The calculated height of the view
                initialSquaresAmount,// How many to spawn
                initialNoSpawnRadius, // Min distance from player for generation
                initialSpawnRadius   // Max distance from player for generation
            );
        }
        else
        {
            // Be more specific in the error
            if (SpawnManager.Instance == null) Debug.LogError("SpawnManager instance not found for initial spawn!");
            if (playerObject == null) Debug.LogError("Player Object not assigned in StartMenuManager!");
            if (vCamStart == null) Debug.LogError("vCamStart (Start Menu Virtual Camera) not assigned in StartMenuManager!");
        }

        if (pauseMenuManager != null) // Ensure pause menu can't be opened during the transition
        {
            pauseMenuManager.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("PauseMenuManager reference not assigned in StartMenuManager Inspector!");
        }

        if (deathScreenManager != null)
        {
            deathScreenManager.gameObject.SetActive(false);
        }

        // Setup Vignette from PostProcessVolume
        if (postProcessVolume != null && postProcessVolume.profile.TryGetSettings(out Vignette v))
        {
            vignette = v;
            vignette.intensity.overrideState = true;
            vignette.color.overrideState = true;
            vignette.intensity.value = initialVignetteIntensity;
        }

        // Initialize store paging display
        UpdateStorePageDisplay();

        // MUSIC: Start or skip to game based on playtestMode
        if (BackgroundMusicManager.Instance != null && !playtestMode)
        {
            BackgroundMusicManager.Instance.PlayMainMenuThemeDesktop();
        }

        if (playtestMode)
        {
            if (startMenuPanel) startMenuPanel.SetActive(false);
            if (backgroundPanel) backgroundPanel.SetActive(false);

            if (BackgroundMusicManager.Instance != null)
            {
                BackgroundMusicManager.Instance.PlayMainThemeFromMenu();
            }

            StartCoroutine(TransitionToGameView());
        }

        // --- Removed SafeZone Camera Disable Logic ---
        // // NEW: Disable SafeZone Camera until the game is fully loaded
        // if (safeZoneCamera != null)
        // {
        //     safeZoneCamera.gameObject.SetActive(false);
        // }
        // --- End Removed Section ---
    }

    private IEnumerator Start()
    {
        // Warmup shaders to avoid performance issues when game is started
#if !UNITY_EDITOR
        // Wait a short time to allow initial loading to complete
        yield return new WaitForSeconds(0.1f);
        Shader.WarmupAllShaders();
#endif
        yield break;
    }

    /// <summary>
    /// Updates the XP slider and associated text labels based on the current SaveData.
    /// </summary>
    ///

    private void UpdateLastScoreDisplay()
    {
        if (lastScoreTMP != null && sdm != null && sdm.persistentData != null)
        {
            lastScoreTMP.text = sdm.persistentData.lastScore.ToString();
        }
    }

    private void OnDailyRewardClaim()
    {
        if (dailyRewardManager == null) return;

        int rewardAmount = dailyRewardManager.ClaimReward();

        if (rewardAmount > 0)
        {
            // Success!
            Debug.Log($"Successfully claimed {rewardAmount} currency!");

            // Update the main currency display on the menu
            UpdateCurrencyDisplay();

            // Optional: Show feedback to the player (e.g., play sound, temporary message)
            // FeedbackManager.Instance.ShowMessage($"+{rewardAmount} Daily Bonus!");

        }
        else
        {
            // Failed (likely already claimed or on cooldown)
            Debug.Log("Daily reward not available yet.");
            // Optional: Show feedback
            // FeedbackManager.Instance.ShowMessage("Reward available tomorrow!");
        }

        // Always update the button's state after attempting a claim
        UpdateDailyRewardUI();
    }

    /// <summary>
    /// Updates the visual state of the daily reward button (enabled/disabled, text).
    /// </summary>
    private void UpdateDailyRewardUI()
    {
        if (dailyRewardManager == null || dailyRewardButton == null) return;

        bool canClaim = dailyRewardManager.CanClaimReward();

        // Enable/disable the button
        dailyRewardButton.interactable = canClaim;

        // Update button text (optional)
        if (dailyRewardButtonText != null)
        {
            if (canClaim)
            {
                dailyRewardButtonText.text = $"Claim {dailyRewardManager.dailyRewardAmount}!"; // Or just "Claim Reward"
            }
            else
            {
                // Show cooldown timer
                dailyRewardButtonText.text = dailyRewardManager.GetTimeRemainingString();
            }
        }

        // Show/hide notification icon (optional)
        if (dailyRewardNotification != null)
        {
            dailyRewardNotification.SetActive(canClaim);
        }
    }

    private void UpdateXPUI()
    {
        if (sdm == null || sdm.persistentData == null)
        {
            Debug.LogWarning("[StartMenuManager] SaveDataManager or its persistentData is null. Cannot update XP UI.");
            // Optionally, set default states for UI elements if sdm is null
            if (xpSlider != null) xpSlider.value = 0;
            if (currentLevelText != null) currentLevelText.text = "1";
            if (nextLevelText != null) nextLevelText.text = "2";
            if (xpProgressText != null) xpProgressText.text = "0/100 XP";
            return;
        }

        int currentLevel = sdm.persistentData.playerLevel;
        int nextLevel = currentLevel + 1;
        long currentXP = sdm.persistentData.currentXP; // Use long to match SaveData
        long xpNeeded = sdm.persistentData.xpNeededForNextLevel; // Use long to match SaveData

        // ---- NEW CODE FOR SLIDER UPDATE ----
        if (xpSlider != null)
        {
            if (xpNeeded > 0)
            {
                // Calculate progress as a float between 0 and 1
                // Ensure currentXP for the slider display doesn't exceed xpNeeded.
                // The SaveDataManager handles the actual logic for level ups.
                float progress = Mathf.Clamp01((float)currentXP / xpNeeded);
                xpSlider.value = progress;
            }
            else
            {
                // Handle cases where xpNeeded might be 0 (e.g., max level or an error in calculation)
                // If at max level and currentXP is also 0 or positive (meaning at or beyond the threshold for the "last" level), fill the bar.
                // If currentXP is somehow negative (which shouldn't happen), empty it.
                xpSlider.value = (currentXP >= 0) ? 1f : 0f;
                // For max level, you might want xpProgressText to show "MAX" or similar.
            }
        }
        // ---- END OF NEW CODE ----

        // Existing code for text updates
        if (currentLevelText != null)
        {
            currentLevelText.text = currentLevel.ToString();
        }

        if (nextLevelText != null)
        {
            nextLevelText.text = nextLevel.ToString();
        }

        if (xpProgressText != null)
        {
            // 1) Clone an invariant NFI so we can swap comma -> space
            var nfi = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo
                                .InvariantCulture
                                .NumberFormat
                                .Clone();
            nfi.NumberGroupSeparator = " "; // Use space as a separator
            nfi.NumberGroupSizes = new[] { 3 }; // Group digits by 3

            // 2) Format both values with "N0" (no decimals) + our space separator
            string curXpFormatted = currentXP.ToString("N0", nfi);
            string xpNeededFormatted = xpNeeded.ToString("N0", nfi);

            // Handle max level display in xpProgressText
            if (xpNeeded <= 0 && currentLevel > 1) // Assuming level 1 is not max
            {
                xpProgressText.text = "MAX LEVEL";
                if (nextLevelText != null) nextLevelText.text = ""; // Hide next level text or set to "-"
            }
            else
            {
                xpProgressText.text = $"{curXpFormatted}/{xpNeededFormatted} XP";
            }
        }
    }

    /// <summary>
    /// Updates the currency display text with a "$" prefix using the saved permanent total.
    /// </summary>
    private void UpdateCurrencyDisplay()
    {
        if (currencyDisplayTMP != null && sdm != null && sdm.persistentData != null)
        {
            currencyDisplayTMP.text = "$" + sdm.persistentData.totalCurrency.ToString("N0"); 
        }
    }

    /// <summary>
    /// Updates the high score display text using the saved high score from SaveDataManager's persistent data.
    /// </summary>
    private void UpdateHighScoreDisplay()
    {
        if (highScoreTMP != null && sdm != null && sdm.persistentData != null)
        {
            highScoreTMP.text = sdm.persistentData.highScore.ToString();
        }
    }

    // -----------------------------
    // BUTTON METHODS
    // -----------------------------
    public void OnStartButton()
    {
        if (isAnimating) return;
        isAnimating = true;

        if (swooshAudio) swooshAudio.Play();

        if (startMenuPanel) startMenuPanel.SetActive(false);
        if (backgroundPanel) backgroundPanel.SetActive(false);

        if (BackgroundMusicManager.Instance != null)
        {
            BackgroundMusicManager.Instance.CrossFadeToMainTheme(transitionDuration);
        }

        StartCoroutine(TransitionToGameView());
    }
    public void OnAboutButton()
    {
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(true); // Show the About panel
        }
        if (startMenuPanel != null)
        {
            startMenuPanel.SetActive(false); // Hide the main menu panel
        }
    }

    void ShowAchievementsPanel()
    {
        // Clear previous
        foreach (Transform c in achievementsContent) Destroy(c.gameObject);

        foreach (var def in AchievementManager.Instance.allAchievements)
        {
            var ui = Instantiate(itemPrefab, achievementsContent);
            ui.Init(def);
        }

        achievementsPanel.SetActive(true);
        startMenuPanel.SetActive(false);
    }

    public void OnAchievementsButton()
    {

        if (achievementsPanel == null) return;

        bool opening = !achievementsPanel.activeSelf;
        achievementsPanel.SetActive(opening);

        // Mirror the Store/Settings logic: hide the main menu when opening,
        // show it again when closing.
        if (startMenuPanel != null)
            startMenuPanel.SetActive(!opening);

        // If you're populating from code, do it here:
        if (opening)
            PopulateAchievementsGrid();
    }

    /// <summary>
    /// Called by a “Return” button on the Achievements panel.
    /// </summary>
    public void OnAchievementsReturn()
    {
        if (achievementsPanel != null)
            achievementsPanel.SetActive(false);
        if (startMenuPanel != null)
            startMenuPanel.SetActive(true);
    }

    /// <summary>
    /// Optional: clear & re‑fill the grid of AchievementItemUI prefabs.
    /// </summary>
    private void PopulateAchievementsGrid()
    {
        if (achievementsContent == null || itemPrefab == null)
            return;

        // clear old
        foreach (Transform child in achievementsContent)
            Destroy(child.gameObject);

        // instantiate one tile per definition
        foreach (var def in AchievementManager.Instance.allAchievements)
        {
            var tile = Instantiate(itemPrefab, achievementsContent);
            tile.Init(def);
        }
    }

    public void OnExitButton()
    {
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(true);
        }
    }

    public void OnExitConfirmYes()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnExitConfirmNo()
    {
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(false);
        }
    }

    // --- Modified Settings Button ---
    public void OnSettingsButton()
    {
        if (!settingsPanel) return;
        // Instead of toggling manually, call the settings manager’s method.
        SettingsManager sm = settingsPanel.GetComponent<SettingsManager>();
        if (sm != null)
        {
            sm.OpenSettingsFromStartMenu();
        }
    }

    public void OnStoreButton()
    {
        if (!storePanel) return;
        bool isActive = storePanel.activeSelf;
        storePanel.SetActive(!isActive);

        if (storePanel.activeSelf)
        {
            if (startMenuPanel) startMenuPanel.SetActive(false);
        }
        else
        {
            if (startMenuPanel) startMenuPanel.SetActive(true);
        }
    }

    public void OnReturnToStartMenu()
    {
        if (storePanel) storePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (tutorialPanel) tutorialPanel.SetActive(false);
        if (achievementsPanel) achievementsPanel.SetActive(false);
        if (aboutPanel) aboutPanel.SetActive(false);

        if (startMenuPanel) startMenuPanel.SetActive(true);

        UpdateDailyRewardUI();
        UpdateCurrencyDisplay();
    }

    // -----------------------------
    // TUTORIAL PAGING METHODS
    // -----------------------------
    public void OnHowToPlayButton()
    {
        if (tutorialPanel)
        {
            tutorialPanel.SetActive(true);
        }
        if (startMenuPanel)
        {
            startMenuPanel.SetActive(false);
        }
        currentTutorialPage = 0;
        UpdateTutorialPageDisplay();
    }

    public void OnTutorialReturn()
    {
        if (tutorialPanel)
        {
            tutorialPanel.SetActive(false);
        }
        if (startMenuPanel)
        {
            startMenuPanel.SetActive(true);
        }
    }

    public void OnNextTutorialPage()
    {
        if (tutorialPages == null || tutorialPages.Length == 0) return;
        if (currentTutorialPage < tutorialPages.Length - 1)
        {
            currentTutorialPage++;
            UpdateTutorialPageDisplay();
        }
    }

    public void OnPreviousTutorialPage()
    {
        if (tutorialPages == null || tutorialPages.Length == 0) return;
        if (currentTutorialPage > 0)
        {
            currentTutorialPage--;
            UpdateTutorialPageDisplay();
        }
    }

    private void UpdateTutorialPageDisplay()
    {
        if (tutorialPages == null || tutorialPages.Length == 0)
        {
            if (tutorialPageIndicator) tutorialPageIndicator.text = "No Pages";
            return;
        }

        for (int i = 0; i < tutorialPages.Length; i++)
        {
            tutorialPages[i].SetActive(false);
        }

        tutorialPages[currentTutorialPage].SetActive(true);

        if (tutorialPageIndicator)
        {
            tutorialPageIndicator.text = $"Page {currentTutorialPage + 1}/{tutorialPages.Length}";
        }
    }

    // -----------------------------
    // STORE PAGING METHODS
    // -----------------------------
    public void OnNextStorePage()
    {
        if (storePages == null || storePages.Length == 0) return;
        if (currentStorePage < storePages.Length - 1)
        {
            currentStorePage++;
            UpdateStorePageDisplay();
        }
    }

    public void OnPreviousStorePage()
    {
        if (storePages == null || storePages.Length == 0) return;
        if (currentStorePage > 0)
        {
            currentStorePage--;
            UpdateStorePageDisplay();
        }
    }

    private void UpdateStorePageDisplay()
    {
        if (storePages == null || storePages.Length == 0)
        {
            if (storePageIndicator != null) storePageIndicator.text = "No Pages";
            Debug.LogError("[StartMenuManager] Store Pages array is not initialized or is empty!");
            return;
        }

        // 1) Hide all pages first
        for (int i = 0; i < storePages.Length; i++)
        {
            if (storePages[i] != null)
            {
                storePages[i].SetActive(false);
            }
        }

        // Ensure currentStorePage is within valid bounds
        if (currentStorePage < 0 || currentStorePage >= storePages.Length || storePages[currentStorePage] == null)
        {
            Debug.LogError($"[StartMenuManager] currentStorePage ({currentStorePage}) is out of bounds or the page GameObject is null.");
            if (storePageIndicator != null) storePageIndicator.text = "Page Error";
            return;
        }

        // 2) Activate the current page
        storePages[currentStorePage].SetActive(true);

        // 3) Handle locking specifically for the page that has the lock mechanism
        //    (Assuming the last page is the one with storePage4RequiredLevel and storePage4LockedPanel)
        bool isThisTheLockablePage = (currentStorePage == storePages.Length - 1); // Or a more specific check if not always the last page

        if (isThisTheLockablePage && storePage4LockedPanel != null)
        {
            int playerLevel = SaveDataManager.Instance.persistentData.playerLevel;
            bool isPageCurrentlyUnlocked = playerLevel >= storePage4RequiredLevel;

            storePage4LockedPanel.SetActive(!isPageCurrentlyUnlocked);

            // If the page IS LOCKED, then (and only then) iterate through its buttons
            // and make them non-interactable.
            if (!isPageCurrentlyUnlocked)
            {
                // Page is LOCKED, so disable all its buttons.
                // Use GetComponentsInChildren<Button>(true) to include buttons on inactive child GameObjects if necessary,
                // though if storePages[currentStorePage] itself was just activated, (false) should be fine.
                foreach (var btn in storePages[currentStorePage].GetComponentsInChildren<Button>())
                {
                    btn.interactable = false;
                }
            }
            // ELSE (Page is UNLOCKED):
            // DO NOT touch btn.interactable here.
            // StoreManager is responsible for setting individual button interactability
            // based on whether an item is maxed out or not. If we set btn.interactable = true here,
            // we would override StoreManager's logic for already maxed-out items.
        }
        else if (storePage4LockedPanel != null)
        {
            // If this isn't the lockable page, ensure the lock panel is hidden
            // (This is a safeguard in case it's a shared panel or state wasn't reset)
            storePage4LockedPanel.SetActive(false);
        }

        // 4) Update the page indicator text
        if (storePageIndicator != null)
        {
            storePageIndicator.text = $"Page {currentStorePage + 1}/{storePages.Length}";
        }
    }

    // -----------------------------
    // CAMERA TRANSITION
    // -----------------------------
    private IEnumerator TransitionToGameView()
    {
        if (vCamStart && vCamGame)
        {
            vCamStart.Priority = 10;
            vCamGame.Priority = 20;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);

            if (vignette != null)
            {
                float newIntensity = Mathf.Lerp(initialVignetteIntensity, finalVignetteIntensity, t);
                vignette.intensity.value = newIntensity;
            }

            yield return null;
        }

        if (UIManager) UIManager.SetActive(true);
        EnablePlayerComponents();

        var gm = playerObject.GetComponent<GameManager>();
        if (gm) gm.gameStarted = true;

        if (vignette != null)
        {
            vignette.color.value = finalVignetteColor;
        }

        if (pauseMenuManager != null)
        {
            pauseMenuManager.gameObject.SetActive(true);
        }

        if (deathScreenManager != null)
        {
            deathScreenManager.gameObject.SetActive(true);
        }

        // --- Removed SafeZone Camera Enable Logic ---
        // // NEW: Enable the SafeZone camera after the game is fully loaded
        // if (safeZoneCamera != null)
        // {
        //     safeZoneCamera.gameObject.SetActive(true);
        // }
        // --- End Removed Section ---

        StartCoroutine(GradualDespawnInitialEntities());

        isAnimating = false;
    }

    private IEnumerator GradualDespawnInitialEntities()
    {
        // Ensure SpawnManager exists and we actually want to despawn
        if (SpawnManager.Instance == null || !enableDespawningAfterZoom)
        {
            // If not despawning, make sure the regular loop is also disabled
            if (SpawnManager.Instance != null) SpawnManager.Instance.enableDespawning = false;
            yield break; // Exit if no spawn manager or despawning is disabled
        }

        // Optional short delay to let things settle after transition
        if (initialDespawnDelay > 0)
        {
            yield return new WaitForSeconds(initialDespawnDelay);
        }

        Debug.Log("[StartMenuManager] Starting gradual initial despawn...");

        // Get required info (make sure playerTransform is assigned in SpawnManager)
        Transform player = SpawnManager.Instance.playerTransform;
        float despawnRad = SpawnManager.Instance.despawnRadius; // Use the regular despawn radius
        float despawnRadSqr = despawnRad * despawnRad; // Use squared distance for efficiency

        if (player == null)
        {
            Debug.LogError("[StartMenuManager] Player Transform not assigned in SpawnManager for gradual despawn!");
            yield break;
        }

        // Get a COPY of the list to avoid issues if SpawnManager modifies its list during this process
        List<GameObject> entitiesToProcess = new List<GameObject>(SpawnManager.Instance.GetActiveEntities());
        int checkCounter = 0;

        // Iterate backwards is safer when removing items indirectly (DespawnEntity removes from original list)
        for (int i = entitiesToProcess.Count - 1; i >= 0; i--)
        {
            GameObject entity = entitiesToProcess[i];

            // Check if entity still exists (might have been destroyed by other means)
            if (entity == null) continue;

            // Check distance (using squared magnitude is faster than Vector3.Distance)
            float distSqr = (player.position - entity.transform.position).sqrMagnitude;

            if (distSqr > despawnRadSqr)
            {
                // Entity is outside the radius, despawn it using SpawnManager's method
                // This will handle pooling correctly
                SpawnManager.Instance.DespawnEntity(entity);
            }

            checkCounter++;

            // If we've checked enough entities for this frame, yield execution
            if (checkCounter >= entitiesToCheckPerFrame)
            {
                checkCounter = 0;
                yield return null; // Wait until next frame
            }
        }

        Debug.Log("[StartMenuManager] Gradual initial despawn complete.");

        // NOW, enable the regular ongoing despawning loop in SpawnManager
        SpawnManager.Instance.enableDespawning = true;
    }

    private void DisablePlayerComponents()
    {
        // Player Scripts
        if (playerMovement) playerMovement.enabled = false;
        if (playerStamina) playerStamina.enabled = false;
        if (playerHealth) playerHealth.enabled = false;
        if (weaponManager) weaponManager.enabled = false;
        if (grenadeThrow) grenadeThrow.enabled = false;
        if (rocketCompanionManager) rocketCompanionManager.enabled = false;

        // Other Scripts
        if (difficultyManager) difficultyManager.SetActive(false);
    }

    private void EnablePlayerComponents()
    {
        // Player Scripts
        if (playerMovement) playerMovement.enabled = true;
        if (playerStamina) playerStamina.enabled = true;
        if (playerHealth) playerHealth.enabled = true;
        if (weaponManager) weaponManager.enabled = true;
        if (grenadeThrow) grenadeThrow.enabled = true;
        if (rocketCompanionManager) rocketCompanionManager.enabled = true;

        // Other Scripts
        if (difficultyManager) difficultyManager.SetActive(true);
    }

    public void ActivateUniqueSquareAbility(UniqueSquareType uniqueSquareType, float duration = 0f)
    {
        Debug.Log($"[SpawnManager] ActivateUniqueSquareAbility: {uniqueSquareType} for {duration}s");
    }
}