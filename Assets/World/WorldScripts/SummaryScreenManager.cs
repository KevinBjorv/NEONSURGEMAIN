using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using System.Collections;
using System.Globalization; // Needed for NumberFormatInfo

/// <summary>
/// Manages the summary screen UI, intro / pop animations,
/// and freezing / unfreezing the game.
/// </summary>
public class SummaryScreenManager : MonoBehaviour
{
    /*──────────────────────────────────────────── UI refs ──*/
    [Header("Main Parts")]
    [Tooltip("Root window that scales in (SummaryPanelWindow).")]
    public RectTransform panelWindow;
    [Tooltip("Full‑screen image used for the dark fade.")]
    public Image fadeOverlay;

    [Header("Texts and bars")]
    public TextMeshProUGUI totalScoreText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI newHighScoreText;
    public TextMeshProUGUI timeText;

    public Slider xpSlider;
    public TextMeshProUGUI currentLevelText;
    public TextMeshProUGUI nextLevelText;
    public TextMeshProUGUI xpValuesText;
    public TextMeshProUGUI enemiesKilledText;
    public TextMeshProUGUI currencyGainedText;

    [Header("Continue Button")]
    public Button continueButton;

    [Header("Intro Animation Settings")]
    public float introDuration = 0.35f;
    public float introStartScale = 0.8f;
    public float overlayTargetAlpha = 0.6f;

    [Header("Score Pop")]
    public float scorePopScale = 1.2f;
    public float scorePopDuration = 0.35f;

    [Header("High Score Pop")]
    public float newHighPopScale = 1.35f;
    public float newHighPopDuration = 0.4f;

    [Header("XP Animation")]
    public float xpFillDurationPerLevel = 0.4f;

    [Header("Audio")]
    public AudioSource levelingUpAudioSource;
    public AudioMixer audioMixer;
    public float musicVolumeLowerAmount = -10f;   // dB
    public float volumeTransitionDuration = 1f;

    [Header("Debug")]
    public bool debugForceNewHighScore = false;

    [Header("Misc")]
    public GameObject UIManager; // Reference to the GameObject holding UIManager script
    public UIManager UIManagerScript; // Reference to the UIManager script component
    public GameObject playerObject;

    /*──────────────────────────────────────────── private ──*/
    // Assuming Player components might be needed elsewhere or were part of original design
    // PlayerMovement playerMovement;
    // PlayerStamina playerStamina;
    // PlayerHealth playerHealth;
    // WeaponManager weaponManager;

    Coroutine scoreCoroutine, xpCoroutine, newHighCoroutine;
    Vector3 originalScoreScale;
    Vector2 continueOriginalSize;
    float originalMusicVol;
    bool isSummaryOpen;

    // float timerTextValue; // This seems redundant if UIManagerScript.elapsedTime is used

    /*─────────────── helpers ───────────────*/
    // Make sure NumberFormatInfo is initialized correctly
    static readonly NumberFormatInfo nfi;
    static SummaryScreenManager()
    {
        nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        nfi.NumberGroupSeparator = " "; // Or "," or your preferred separator
    }
    // Overload Pretty for different types as needed
    static string Pretty(int n) => n.ToString("N0", nfi);
    static string Pretty(long n) => n.ToString("N0", nfi); // Add overload for long
    static string Pretty(float n) => Mathf.RoundToInt(n).ToString("N0", nfi); // Example for float

    /*─────────────────────────────────────── Unity flow ──*/
    void Awake()
    {
        if (continueButton)
            continueButton.onClick.AddListener(OnContinue);

        if (newHighScoreText)
            newHighScoreText.gameObject.SetActive(false);

        if (totalScoreText)
            originalScoreScale = totalScoreText.transform.localScale;

        if (continueButton)
            continueOriginalSize = continueButton.GetComponent<RectTransform>().sizeDelta;

        // Ensure UIManagerScript reference is set if UIManager GameObject is assigned
        if (UIManager != null && UIManagerScript == null)
        {
            UIManagerScript = UIManager.GetComponent<UIManager>();
        }

        gameObject.SetActive(false); // hide until shown

        // Get Player components if needed (removed unused ones for clarity)
        // if (playerObject) { ... }
    }

    private void LateUpdate()
    {
        // Example: Disable weapon manager if summary is open
        // if (isSummaryOpen && weaponManager != null && weaponManager.enabled)
        //     weaponManager.enabled = false;
    }

    /*────────────────────────────────────── Show Summary ──*/
    public void ShowSummary()
    {
        // 1️⃣ Make sure we have this run’s numbers (from ScoreManager, etc.)
        // This should ideally happen *before* showing the summary
        SaveDataManager.Instance?.CollectAllData();

        // 2️⃣ Activate the summary screen object
        gameObject.SetActive(true);
        isSummaryOpen = true;

        // 3️⃣ Hide the regular game UI
        if (UIManager) UIManager.SetActive(false);

        // 4️⃣ Store original music volume for restoration later
        if (audioMixer)
            audioMixer.GetFloat("MusicVolume", out originalMusicVol);

        // 5️⃣ Start visual sequences
        StartCoroutine(IntroSequence()); // Fade in overlay, scale in window
        PopulateTextsAndStartCoroutines(); // Fill data and start animations

        // 6️⃣ Disable player controls / components
        // DisablePlayerComponents(); // You might need a function for this
        Time.timeScale = 0f; // Pause the game
    }

    /*──────── intro (overlay + window pop) ───────*/
    IEnumerator IntroSequence()
    {
        if (fadeOverlay) fadeOverlay.color = new Color(0, 0, 0, 0);
        if (panelWindow) panelWindow.localScale = Vector3.one * introStartScale;

        float t = 0f;
        while (t < introDuration)
        {
            // Use unscaledDeltaTime because Time.timeScale is 0
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / introDuration); // Ensure p doesn't exceed 1

            if (fadeOverlay)
                fadeOverlay.color = new Color(0, 0, 0, Mathf.Lerp(0, overlayTargetAlpha, p));
            if (panelWindow)
                panelWindow.localScale = Vector3.LerpUnclamped(
                    Vector3.one * introStartScale,
                    Vector3.one,
                    EaseOutBack(p)); // Use eased progress

            yield return null; // Wait for the next frame
        }

        // Ensure final state
        if (fadeOverlay) fadeOverlay.color = new Color(0, 0, 0, overlayTargetAlpha);
        if (panelWindow) panelWindow.localScale = Vector3.one;


        // Lower music volume after intro completes
        if (audioMixer)
            StartCoroutine(TransitionMusicVolume(originalMusicVol,
                                                 originalMusicVol + musicVolumeLowerAmount,
                                                 volumeTransitionDuration));
    }

    /*──────── populate UI then start animations ───────*/
    // --- THIS IS THE UPDATED FUNCTION ---
    void PopulateTextsAndStartCoroutines()
    {
        var sdm = SaveDataManager.Instance;
        if (sdm == null)
        {
            Debug.LogError("[SummaryScreen] SaveDataManager Instance is null!");
            return;
        }

        /* --- Get Session Numbers --- */
        int finalScore = sdm.finalScore;
        // Get high score from persistent data (as it might be updated only on continue)
        int currentHighScore = sdm.persistentData.highScore;
        // Use the reliable runtime source, fallback to sessionTime if UIManager isn't available
        float runTime = UIManagerScript != null ? UIManagerScript.elapsedTime : sdm.sessionTime;
        int enemies = sdm.enemiesKilled;
        int currency = sdm.currencyGainedThisSession;

        /* --- Get XP/Level State *BEFORE* This Session's Gain --- */
        int oldLevel = sdm.persistentData.playerLevel;
        long oldXPLong = sdm.persistentData.currentXP; // Get long
        long initialNeedXPLong = sdm.persistentData.xpNeededForNextLevel; // Get long

        // Safety check/recalculation for initial needed XP
        if (initialNeedXPLong <= 0L && oldLevel >= 1)
        {
            initialNeedXPLong = sdm.CalculateXpForNextLevel(oldLevel);
            Debug.LogWarning($"[SummaryScreen] Recalculated initialNeedXPLong for level {oldLevel} to {initialNeedXPLong}");
        }
        else if (oldLevel < 1) // Handle case where level might be uninitialized (shouldn't happen ideally)
        {
            oldLevel = 1; // Assume level 1 if invalid
            oldXPLong = 0L;
            initialNeedXPLong = sdm.CalculateXpForNextLevel(1);
            Debug.LogWarning($"[SummaryScreen] oldLevel was < 1, resetting start state display.");
        }
        // XP gained in this session (kept as float from CollectAllData)
        float gained = sdm.xpGainedThisSession;

        /* --- Set Static Texts --- */
        if (highScoreText) highScoreText.text = $"HIGHSCORE: {Pretty(currentHighScore)}";

        // Set Time Text reliably
        if (timeText)
        {
            if (UIManagerScript != null && UIManagerScript.timerText != null && !string.IsNullOrEmpty(UIManagerScript.timerText.text))
            {
                timeText.text = UIManagerScript.timerText.text; // Use already formatted text
            }
            else // Fallback formatting from float
            {
                int m = Mathf.FloorToInt(runTime / 60f);
                int s = Mathf.FloorToInt(runTime % 60f);
                timeText.text = $"{m:00}:{s:00}";
            }
        }

        if (enemiesKilledText) enemiesKilledText.text = Pretty(enemies);
        if (currencyGainedText) currencyGainedText.text = Pretty(currency);

        /* --- Simulate Final XP/Level State --- */
        int finalLevelTarget;
        long finalXpTargetLong;       // Receive long result
        long finalNeedXpTargetLong;   // Receive long result (XP needed for level AFTER finalLevelTarget)
        sdm.SimulateXpGain(gained, out finalLevelTarget, out finalXpTargetLong, out finalNeedXpTargetLong); // Call simulation
        // Log the results for debugging
        Debug.Log($"[SummaryScreen] Simulation Complete. finalLevelTarget={finalLevelTarget}, finalXpTarget(long)={finalXpTargetLong}, finalNeedXpTarget(long)={finalNeedXpTargetLong}. Passing (float) equivalents to AnimateXP.");


        /* --- Set Initial Level/XP UI --- */
        // Display the state *before* animation starts
        if (xpSlider)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = (float)initialNeedXPLong; // Use float for UI max value
            xpSlider.value = (float)oldXPLong;           // Use float for UI current value
        }
        if (currentLevelText) currentLevelText.text = oldLevel.ToString();
        if (nextLevelText) nextLevelText.text = (oldLevel + 1).ToString();
        // Use Pretty(long) for displaying the initial numbers
        if (xpValuesText) xpValuesText.text = $"{Pretty(oldXPLong)} / {Pretty(initialNeedXPLong)}";

        /* --- Start Score Pop Animation --- */
        if (totalScoreText) // Check if assigned
        {
            if (scoreCoroutine != null) StopCoroutine(scoreCoroutine);
            scoreCoroutine = StartCoroutine(AnimateScore(finalScore));
        }

        /* --- Handle "New High Score" Text --- */
        // Check if the score just achieved is higher than the previously saved high score
        if (finalScore > currentHighScore || debugForceNewHighScore)
        {
            if (newHighScoreText)
            {
                newHighScoreText.gameObject.SetActive(true);
                if (newHighCoroutine != null) StopCoroutine(newHighCoroutine);
                newHighCoroutine = StartCoroutine(AnimateNewHighScoreLoop());
            }
        }
        else // Ensure it's hidden if not a new high score
        {
            if (newHighScoreText) newHighScoreText.gameObject.SetActive(false);
        }


        /* --- Start XP Fill Animation (or set final state if no XP gained) --- */
        if (gained > 0.001f && xpSlider) // Use a small threshold for float comparison
        {
            if (xpCoroutine != null) StopCoroutine(xpCoroutine);
            // Convert longs to floats when PASSING to AnimateXP,
            // as AnimateXP uses floats internally for visual interpolation.
            xpCoroutine = StartCoroutine(AnimateXP(
                oldLevel,
                (float)oldXPLong,             // Pass initial XP as float
                (float)initialNeedXPLong,      // Pass initial NeedXP as float
                gained,                       // Pass gained XP as float
                finalLevelTarget,             // Pass target level as int
                (float)finalXpTargetLong,      // Pass target final XP as float
                (float)finalNeedXpTargetLong   // Pass target final NeedXP as float
            ));
        }
        else // If no XP was gained, set UI directly to the final (unchanged) state
        {
            Debug.Log("[SummaryScreen] No significant XP gained, setting final UI state directly.");
            if (currentLevelText) currentLevelText.text = finalLevelTarget.ToString(); // Should be same as oldLevel
            if (nextLevelText) nextLevelText.text = (finalLevelTarget + 1).ToString(); // Should be same as oldLevel+1

            if (xpSlider)
            {
                // Use the simulated final target values (which should match initial values)
                xpSlider.maxValue = (float)finalNeedXpTargetLong;
                xpSlider.value = (float)finalXpTargetLong;
            }
            if (xpValuesText)
            {
                // Use the Pretty(long) overload for displaying the values
                xpValuesText.text = $"{Pretty(finalXpTargetLong)} / {Pretty(finalNeedXpTargetLong)}";
            }
        }
    } // End PopulateTextsAndStartCoroutines


    /*──────── Music fade ───────*/
    IEnumerator TransitionMusicVolume(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float currentVol = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            if (!audioMixer.SetFloat("MusicVolume", currentVol))
            {
                Debug.LogWarning("[SummaryScreen] Failed to set MusicVolume on AudioMixer.");
                yield break; // Stop if mixer fails
            }
            yield return null;
        }
        if (!audioMixer.SetFloat("MusicVolume", to)) // Ensure final value
        {
            Debug.LogWarning("[SummaryScreen] Failed to set final MusicVolume on AudioMixer.");
        }
    }

    /*──────── Score pop ───────*/
    IEnumerator AnimateScore(int finalScore)
    {
        if (totalScoreText == null) yield break; // Exit if text not assigned

        float t = 0f;
        Vector3 baseScale = originalScoreScale; // Use the stored original scale

        while (t < scorePopDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / scorePopDuration); // Use clamped progress

            // Animate score text value
            int cur = Mathf.RoundToInt(Mathf.Lerp(0, finalScore, p));
            totalScoreText.text = Pretty(cur);

            // Animate scale (pop out and back)
            float scaleFactor = 1f;
            float halfDur = scorePopDuration / 2f;
            if (t <= halfDur)
            {
                scaleFactor = Mathf.Lerp(1f, scorePopScale, t / halfDur); // Scale up
            }
            else
            {
                scaleFactor = Mathf.Lerp(scorePopScale, 1f, (t - halfDur) / halfDur); // Scale down
            }
            totalScoreText.transform.localScale = baseScale * scaleFactor;

            yield return null;
        }
        // Ensure final state
        totalScoreText.text = Pretty(finalScore);
        totalScoreText.transform.localScale = baseScale;

        // This logic related to continue button size seems misplaced here,
        // perhaps intended for after score AND XP animation?
        // if (continueButton)
        //     continueButton.GetComponent<RectTransform>().sizeDelta = continueOriginalSize;
    }

    /*──────── High score pop loop ───────*/
    IEnumerator AnimateNewHighScoreLoop()
    {
        if (newHighScoreText == null) yield break;

        Vector3 baseScale = newHighScoreText.transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one; // Safety check

        // Loop indefinitely while the text object is active
        while (newHighScoreText.gameObject.activeInHierarchy)
        {
            // Animate one pop cycle
            float t = 0f;
            while (t < newHighPopDuration)
            {
                if (!newHighScoreText.gameObject.activeInHierarchy) yield break; // Exit if deactivated mid-animation

                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / newHighPopDuration);
                float scaleFactor = 1f;
                float halfDur = newHighPopDuration / 2f;

                if (t <= halfDur)
                {
                    scaleFactor = Mathf.Lerp(1f, newHighPopScale, t / halfDur); // Scale up
                }
                else
                {
                    scaleFactor = Mathf.Lerp(newHighPopScale, 1f, (t - halfDur) / halfDur); // Scale down
                }
                newHighScoreText.transform.localScale = baseScale * scaleFactor;
                yield return null; // Wait for next frame
            }
            // Ensure scale resets at end of cycle before potential pause
            newHighScoreText.transform.localScale = baseScale;

            // Optional: Add a small pause between loops
            // yield return new WaitForSecondsRealtime(0.5f);
            yield return null; // Yield at least one frame before restarting loop
        }
        // Ensure scale is reset if loop exits
        newHighScoreText.transform.localScale = baseScale;
    }

    /*──────── XP fill + level‑up (receives float targets) ───────*/
    IEnumerator AnimateXP(int startLvl, float startXP, float initialNeedXP, float gained,
                          int finalLevelTarget, float finalXpTarget, float finalNeedXpTarget)
    {
        if (xpSlider == null)
        {
            Debug.LogError("[AnimateXP] xpSlider is not assigned!");
            yield break;
        }

        int lvl = startLvl;         // Visual level during animation
        float curXP = startXP;      // Visual XP during animation (float)
        float left = gained;        // Remaining XP to animate (float)
        float needXP = initialNeedXP; // Visual requirement for current level (float)

        // --- Animation loop ---
        while (left > 0.001f) // Loop while there's XP left to animate
        {
            // Calculate XP for this chunk
            float spaceInLevel = Mathf.Max(0f, needXP - curXP); // How much XP fits in the current level bar
            float toGain = Mathf.Min(left, spaceInLevel); // Gain amount is limited by space or remaining XP
            float targetXP = curXP + toGain; // Visual target for this animation chunk

            // Calculate duration
            float dur = 0f;
            if (needXP > 0.001f) // Avoid division by zero
            {
                dur = xpFillDurationPerLevel * (toGain / needXP);
                // Optional: Add a minimum duration? e.g., Mathf.Max(0.05f, dur);
            }

            // Animate this chunk
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = (dur > 0) ? Mathf.Clamp01(t / dur) : 1f;
                float val = Mathf.Lerp(curXP, targetXP, p);

                xpSlider.value = val; // Update slider visually

                if (xpValuesText) // Update text visually (use Pretty(long) for consistency)
                    xpValuesText.text = $"{Pretty((long)Mathf.Round(val))} / {Pretty((long)Mathf.Round(needXP))}";

                yield return null;
            }

            // Snap to the end of the chunk state visually
            curXP = targetXP;
            xpSlider.value = curXP;
            if (xpValuesText) xpValuesText.text = $"{Pretty((long)Mathf.Round(curXP))} / {Pretty((long)Mathf.Round(needXP))}";

            left -= toGain; // Decrease remaining XP

            // --- Visual Level up check ---
            // Check if the level bar is full (or very close) AND there's still XP left to process for the next level
            if (curXP >= needXP - 0.001f && left > 0.001f)
            {
                lvl++; // Increment visual level
                if (currentLevelText) currentLevelText.text = lvl.ToString();
                if (nextLevelText) nextLevelText.text = (lvl + 1).ToString();
                if (levelingUpAudioSource) levelingUpAudioSource.Play();

                // Get requirement for the NEW visual level
                curXP = 0f; // Reset visual XP for the new level bar
                // Get the next requirement by asking SaveDataManager (returns long, cast to float)
                needXP = (float)SaveDataManager.Instance.CalculateXpForNextLevel(lvl);

                // Update slider max/value for the new level bar
                xpSlider.maxValue = needXP;
                xpSlider.value = 0f;

                // Update text visually for the new level bar state
                if (xpValuesText)
                    xpValuesText.text = $"{Pretty(0L)} / {Pretty((long)Mathf.Round(needXP))}"; // Show 0 / NewNeed

                yield return new WaitForSecondsRealtime(0.15f); // Pause for effect
            }
        } // --- End Animation loop ---


        // --- Force Final State Display ---
        // AFTER the loop finishes, forcefully set the UI to the PRE-CALCULATED final values
        Debug.Log($"[AnimateXP] Animation loop finished. Snapping UI to pre-calculated final state: Level={finalLevelTarget}, XP(float)={finalXpTarget}, NeedForNext(float)={finalNeedXpTarget}");

        if (currentLevelText) currentLevelText.text = finalLevelTarget.ToString();
        if (nextLevelText) nextLevelText.text = (finalLevelTarget + 1).ToString();

        if (xpSlider)
        {
            // Final Max value should be the requirement for the level *after* the final level achieved
            xpSlider.maxValue = finalNeedXpTarget;
            // Final Value is the remaining XP within the final level achieved
            xpSlider.value = finalXpTarget;
        }
        if (xpValuesText) // Display final remaining XP and the requirement for the next level
        {
            // Use Pretty(long) for consistency, rounding the float targets
            xpValuesText.text = $"{Pretty((long)Mathf.Round(finalXpTarget))} / {Pretty((long)Mathf.Round(finalNeedXpTarget))}";
        }

    } // End AnimateXP

    /*──────── Continue button ───────*/
    public void OnContinue() => StartCoroutine(HandleOnContinue());

    IEnumerator HandleOnContinue()
    {
        if (continueButton != null && continueButton.interactable)
        {
            // Disable the button immediately to prevent further clicks
            continueButton.interactable = false;
            Debug.Log("[HandleOnContinue] Continue button clicked and disabled.");
        }
        else
        {
            // If button reference is null or already disabled, log a warning and exit
            Debug.LogWarning("[HandleOnContinue] Continue action triggered, but button was null or already disabled. Exiting.");
            yield break; // Stop the coroutine here
        }

        // Restore music volume first? Or after save? User preference.
        if (audioMixer)
        {
            // Optionally start fade back to original volume
            // StartCoroutine(TransitionMusicVolume(originalMusicVol + musicVolumeLowerAmount, originalMusicVol, volumeTransitionDuration));
            // Or just set it back instantly:
            audioMixer.SetFloat("MusicVolume", originalMusicVol);
        }


        // --- IMPORTANT: Merge session results & save ---
        SaveDataManager.Instance?.ApplySessionResultsAndSave();
        // ---

        // Unpause time before loading scene
        Time.timeScale = 1f;

        // Reload the current scene (or load a different scene like a main menu/hub)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Or use scene name/index

        // Optional: Wait a frame before potentially trying to reactivate old UI
        // yield return null;
        // if (UIManager) UIManager.SetActive(true); // Usually not needed if reloading scene

        // Coroutine technically ends here due to scene load
        yield break;
    }

    /*──────── Helpers ───────*/
    void DisablePlayerComponents()
    {
        // Implement logic to disable relevant player scripts (Movement, Shooting, etc.)
        // Example:
        // if (playerObject == null) return;
        // var scriptsToDisable = playerObject.GetComponents<MonoBehaviour>(); // Or specify types
        // foreach (var script in scriptsToDisable)
        // {
        //     // Add conditions to avoid disabling essential scripts
        //     if (script != this && !(script is Camera)) // Example condition
        //     {
        //         script.enabled = false;
        //     }
        // }
        // Or disable specific components directly:
        // if (playerMovement) playerMovement.enabled = false;
        // if (weaponManager) weaponManager.enabled = false;
        Debug.LogWarning("DisablePlayerComponents() called but not fully implemented.");

    }

    // Easing function for intro animation
    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        // Clamp input x to 0-1 range before applying easing
        x = Mathf.Clamp01(x);
        return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
    }
}