using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the Canvas (or root GameObject) that contains the pause menu UI.")]
    public GameObject pauseMenuCanvas;
    [Tooltip("Assign the Exit Confirmation Panel GameObject (separate from the pause menu).")]
    public GameObject exitConfirmationPanel;
    [Tooltip("Reference to the StartMenuManager so we can re-initialize the start menu.")]
    public StartMenuManager startMenuManager;

    [Header("Shared Settings Panel")]
    [Tooltip("Reference to the shared Settings Panel GameObject.")]
    public GameObject settingsPanel;

    public PlayerMovement playerMovement;
    public WeaponManager weaponManager;

    /// <summary>
    /// Tracks whether the game is currently paused.
    /// </summary>
    private bool isPaused = false;

    private void Start()
    {
        // Ensure the pause menu and exit confirmation are hidden at game start
        if (pauseMenuCanvas)
        {
            pauseMenuCanvas.SetActive(false);
        }
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Listen for the Esc key to toggle pause/unpause.
        // If the settings panel is active, ignore the Esc key.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
                return;

            if (!isPaused)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }
    }

    /// <summary>
    /// Called when the user presses the Esc key or a Pause button (if you have one).
    /// Activates the pause UI and stops time.
    /// </summary>
    private void PauseGame()
    {
        playerMovement.enabled = false;
        weaponManager.enabled = false;

        isPaused = true;
        Time.timeScale = 0f; // Freeze the game
        if (pauseMenuCanvas)
        {
            pauseMenuCanvas.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the pause menu and resumes normal gameplay.
    /// </summary>
    public void ResumeGame()
    {
        playerMovement.enabled = true;
        weaponManager.enabled = true;
        isPaused = false;
        Time.timeScale = 1f; // Unfreeze the game

        if (pauseMenuCanvas)
        {
            pauseMenuCanvas.SetActive(false);
        }
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(false);
        }

        // Reapply the current music volume after resume
        SettingsManager settingsManager = FindObjectOfType<SettingsManager>();
        if (settingsManager != null)
        {
            settingsManager.ReapplyMusicVolume();
        }
    }


    /// <summary>
    /// Called by the "Return to Start Menu" button (or from the exit confirmation 'Yes').
    /// Deactivates the pause menu, unfreezes time, and re-initializes the 
    /// StartMenuManager & SpawnManager so it behaves as though the game was just opened.
    /// </summary>
    public void ReturnToStartMenu()
    {
        // Make sure the game is unpaused before returning
        isPaused = false;
        Time.timeScale = 1f;

        // Hide all pause-related UI panels
        if (pauseMenuCanvas)
        {
            pauseMenuCanvas.SetActive(false);
        }
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(false);
        }

        // Re-initialize the start menu in the same way as the first time.
        if (startMenuManager != null)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            startMenuManager.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("PauseMenuManager: No reference to StartMenuManager assigned!");
        }
    }

    /// <summary>
    /// Called by the "Continue" button on the pause menu UI.
    /// Same as ResumeGame(), but separated for clarity if you want a direct button event.
    /// </summary>
    public void OnContinueButton()
    {
        ResumeGame();
    }

    /// <summary>
    /// Called when the player presses the "Give Up" button in the pause menu.
    /// This hides the pause menu elements and shows the exit confirmation panel.
    /// </summary>
    public void OnGiveUpButton()
    {
        // Hide pause menu UI
        if (pauseMenuCanvas)
        {
            pauseMenuCanvas.SetActive(false);
        }
        // Show exit confirmation panel
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Called by the "No" button on the exit confirmation panel.
    /// This returns the player to the pause menu.
    /// </summary>
    public void OnExitConfirmationNo()
    {
        // Hide the exit confirmation panel and re-show the pause menu
        if (exitConfirmationPanel)
        {
            exitConfirmationPanel.SetActive(false);
        }
        if (pauseMenuCanvas)
        {
            pauseMenuCanvas.SetActive(true);
        }
    }

    /// <summary>
    /// Called by the "Yes" button on the exit confirmation panel.
    /// This routes the player to the start menu.
    /// </summary>
    public void OnExitConfirmationYes()
    {
        ReturnToStartMenu();
    }

    /// <summary>
    /// Called when the Settings button in the pause menu is pressed.
    /// Opens the shared Settings Panel (using the pause menu integration).
    /// </summary>
    public void OnSettingsButton()
    {
        if (settingsPanel == null) return;
        // Get the SettingsManager component from the shared panel
        SettingsManager sm = settingsPanel.GetComponent<SettingsManager>();
        if (sm != null)
        {
            // Open settings with pause menu as the caller.
            sm.OpenSettingsFromPauseMenu();
        }
    }
}
