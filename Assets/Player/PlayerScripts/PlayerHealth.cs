using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Rendering.PostProcessing; 
using UnityEngine.Audio;
using UnityEngine.UI;
using System.Linq;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("Health value under which low health effects trigger.")]
    public float lowHealthThreshold = 30f;

    [Header("Health Bar UI")]
    [Tooltip("Reference to the UI Image that represents the health bar.")]
    public Image healthBarImage;

    [Header("Passive Regeneration")]
    [Tooltip("Maximum percentage of max health regenerated per second (e.g. 0.05 = 5%).")]
    public float maxPassiveRegenPercent = 0.05f;
    [Tooltip("Time (in seconds) until the regeneration rate reaches the maximum.")]
    public float secondsUntilMaxRegenAchieved = 5f;
    [Tooltip("Delay in seconds after taking damage before regeneration begins.")]
    public float regenDelay = 3f;
    private float lastDamageTime;
    private bool isRegenerating = false;
    [Header("Debug Settings")]
    [Tooltip("Current regen percentage (for debugging).")]
    public float currentRegenPercentage;

    [Header("Feedback Settings")]
    public UnityEvent OnTakeDamage;
    public UnityEvent OnHeal;
    public UnityEvent OnDeath;

    [Header("Invincibility Settings")]
    [Tooltip("Can be enabled by upgrades.")]
    public bool enableInvincibility = false;
    public float invincibilityDuration = 0f;
    private bool isInvincible = false;
    [HideInInspector] public bool shieldBurstActive = false; // Controlled by shield burst ability


    [Header("Damage Flash Settings")]
    [Tooltip("Color the player flashes when taking damage.")]
    public Color damageFlashColor = Color.red;
    [Tooltip("How long the flash lasts.")]
    public float damageFlashDuration = 0.2f;
    [Tooltip("Assign the player's Renderer to flash its emission color.")]
    public Renderer playerRenderer;
    private Color defaultEmissionColor;
    private Coroutine damageFlashCoroutine;

    [Header("Vignette Settings")]
    [Tooltip("Reference to the Post Process Volume.")]
    public PostProcessVolume postProcessVolume;
    // Uses lowHealthThreshold from Health Settings
    [Tooltip("Target vignette intensity when health is low.")]
    public float lowHealthVignetteIntensity = 0.5f;
    [Tooltip("Vignette color when health is low.")]
    public Color lowHealthVignetteColor = Color.red;
    private Vignette vignette;
    private float defaultVignetteIntensity;
    private Color defaultVignetteColor;

    // --- SNAPSHOT CONTROL ---
    [Header("Audio Mixer Snapshots")]
    [Tooltip("Drag the 'Normal Snapshot' asset here from your Project window.")]
    public AudioMixerSnapshot normalHealthSnapshot; // Assign "Normal Snapshot" here
    [Tooltip("Drag the 'LowHealth Snapshot' asset here from your Project window.")]
    public AudioMixerSnapshot lowHealthSnapshot;  // Assign "LowHealth Snapshot" here
    [Tooltip("Time in seconds to transition between snapshots.")]
    public float snapshotTransitionTime = 0.75f;
    private bool isLowHealthStateActive = false; // Tracks the currently active snapshot state for health


    [Header("Heartbeat SFX")]
    [Tooltip("Assign an AudioSource for the heartbeat sound.")]
    public AudioSource heartbeatAudioSource;

    // ── Low‐Health Survival Achievement ──
    private const string lowHealthAchvID = "7";     // your new achievement ID
    [Tooltip("survive x seconds after dipping low to get the achievement")]
    public const float survivalDelay = 10f;        

    private bool lowHealthTimerRunning = false;
    private float lowHealthTimer = 0f;
    private int lowHealthThresholdValue = -1;    // current tier’s threshold


    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // --- Initial Setup ---
        currentHealth = maxHealth; // Start at max health
        lastDamageTime = -regenDelay; // Allow regen immediately if starting full health

        // --- Capture Defaults ---
        // Renderer Emission Color
        if (playerRenderer?.material != null)
        {
            defaultEmissionColor = playerRenderer.material.GetColor("_EmissionColor");
        }
        else { Debug.LogWarning("PlayerHealth: playerRenderer or its material not set.", this); }

        // Vignette Settings
        if (postProcessVolume?.profile != null && postProcessVolume.profile.TryGetSettings(out vignette))
        {
            defaultVignetteIntensity = vignette.intensity.value;
            defaultVignetteColor = vignette.color.value;
        }
        else { Debug.LogWarning("PlayerHealth: Vignette setup failed (Volume/Profile/Setting missing).", this); }

        // --- Ensure Correct Starting State ---
        UpdateHealthBarDisplay();
        // Force snapshot check/transition on start AFTER snapshots assigned
        // Deferring slightly using Invoke ensures Awake/Start completes elsewhere if needed
        Invoke(nameof(InitializeAudioState), 0.05f);
        //CheckAndTransitionSnapshots(true); // Call directly if Invoke isn't needed
        UpdateVignette(); // Set initial vignette state
        UpdateHeartbeatState(); // Set initial heartbeat state
    }

    // Used to initialize snapshot state slightly after Start
    private void InitializeAudioState()
    {
        CheckAndTransitionSnapshots(true); // Force initial snapshot check
    }


    private void Update()
    {
        // --- Passive Regen Logic ---
        HandlePassiveRegen();

        // --- Update Visual/Audio Feedback based on State ---
        // Snapshot transitions are now handled only when health state CHANGES
        UpdateVignette(); // Lerp vignette smoothly every frame
        UpdateHeartbeatState(); // Check heartbeat state every frame
        UpdateLowHealthTimer();
    }

    // --- Health Modification Methods ---

    public void TakeDamage(float damage)
    {
        if (damage <= 0 || currentHealth <= 0) return; // Ignore damage if dead
        if (isInvincible || shieldBurstActive) return; // Ignore if invincible

        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.killsWithoutDamage = 0;
            Debug.Log("Killswithout damage set to 0 in theory, reality: " + WeaponManager.Instance.killsWithoutDamage);
        }

        float healthBeforeDamage = currentHealth;
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Clamp health
        lastDamageTime = Time.time; // Update time since last damage

        OnTakeDamage?.Invoke(); // Fire event
        UpdateHealthBarDisplay(); // Update UI
        CheckAndTransitionSnapshots(); // Check if audio state needs to change


        // Stop regeneration if it was active
        if (isRegenerating)
        {
            StopCoroutine("PassiveRegenCoroutine"); // Stop regen coroutine
            isRegenerating = false;
        }

        // Trigger damage flash effect
        if (playerRenderer != null)
        {
            if (damageFlashCoroutine != null) StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        bool justDied = currentHealth <= 0;

        if (!justDied)
            CheckLowHealthAchievement();

        if (justDied)
            HandleDeath();
    }

    public void Heal(float amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth) return; // Ignore invalid heals

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Clamp to max

        OnHeal?.Invoke(); // Fire event
        UpdateHealthBarDisplay(); // Update UI
        CheckAndTransitionSnapshots(); // Check if audio state needs to change
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth; // Restore health
        lastDamageTime = -regenDelay; // Allow immediate regen possibility
        OnHeal?.Invoke(); // Fire event
        UpdateHealthBarDisplay(); // Update UI
        CheckAndTransitionSnapshots(true); // Force transition back to normal audio state
        // Ensure visual/audio state matches full health immediately
        UpdateVignette();
        UpdateHeartbeatState();
        if (isRegenerating)
        { // Stop regen if it happened to be running
            StopCoroutine("PassiveRegenCoroutine");
            isRegenerating = false;
        }
        Debug.Log("Player health has been reset.");
    }

    private void HandleDeath()
    {
        Debug.Log("Player has died.");
        OnDeath?.Invoke(); // Fire event
        CheckAndTransitionSnapshots(true); // Force transition back to normal audio state on death
        // Stop heartbeat explicitly if it was running
        if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying)
        {
            heartbeatAudioSource.Stop();
        }
        // Stop regen explicitly
        if (isRegenerating)
        {
            StopCoroutine("PassiveRegenCoroutine");
            isRegenerating = false;
        }
        // Add other death logic: disable controls, show game over UI, etc.
    }
    private void CheckLowHealthAchievement()
    {
        // Grab definition & entry
        var def = AchievementManager.Instance.allAchievements.FirstOrDefault(a => a.id == lowHealthAchvID);
        if (def == null) return;

        var entry = SaveDataManager.Instance.GetAchievement(lowHealthAchvID);

        // Which tier are we going for next?
        int nextTier = Mathf.Clamp(entry.currentTier + 1, 0, def.tiers.Length - 1);
        int threshold = def.tiers[nextTier].targetValue;

        // Only start once per‐reach
        if (!lowHealthTimerRunning && currentHealth <= threshold)
        {
            lowHealthThresholdValue = threshold;
            lowHealthTimerRunning = true;
            lowHealthTimer = 0f;
        }
    }

    /// <summary>
    /// Run each frame once the low‐health timer is active.
    /// </summary>
    private void UpdateLowHealthTimer()
    {
        if (!lowHealthTimerRunning) return;

        // If we died, cancel
        if (currentHealth <= 0)
        {
            lowHealthTimerRunning = false;
            return;
        }

        // Count up
        lowHealthTimer += Time.unscaledDeltaTime;
        if (lowHealthTimer >= survivalDelay)
        {
            Debug.Log($"[LowHealth] Survived {survivalDelay}s at ≤{lowHealthThresholdValue}! Reporting achievement.");
            AchievementManager.Instance.ReportProgress(lowHealthAchvID, lowHealthThresholdValue);
            SaveDataManager.Instance.SaveGame();
            lowHealthTimerRunning = false;
        }
    }

    // --- State Management and Feedback ---

    /// <summary>
    /// Checks if the current health requires a snapshot change and transitions if needed.
    /// </summary>
    /// <param name="forceNormal">If true, forces transition to NormalHealthSnapshot regardless of current health.</param>
    private void CheckAndTransitionSnapshots(bool forceNormal = false)
    {
        // Ensure snapshots are assigned in the Inspector
        if (normalHealthSnapshot == null || lowHealthSnapshot == null)
        {
            Debug.LogError("PlayerHealth: Snapshots ('Normal Snapshot' and 'LowHealth Snapshot') must be assigned in the Inspector!", this);
            return;
        }

        // Determine the target state based on health, unless forced to normal
        bool shouldBeLowHealth = (!forceNormal && currentHealth <= lowHealthThreshold && currentHealth > 0);

        // Transition TO Low Health state if needed
        if (shouldBeLowHealth && !isLowHealthStateActive)
        {
            // Debug.Log("Transitioning to LowHealth Snapshot");
            lowHealthSnapshot.TransitionTo(snapshotTransitionTime);
            isLowHealthStateActive = true;
        }
        // Transition TO Normal Health state if needed (or forced)
        else if ((!shouldBeLowHealth || forceNormal) && isLowHealthStateActive)
        {
            // Debug.Log("Transitioning to NormalHealth Snapshot");
            normalHealthSnapshot.TransitionTo(snapshotTransitionTime);
            isLowHealthStateActive = false;
        }
        // If state already matches target (or forced normal and already normal), do nothing.
        // Added check for initial forceNormal call when already normal
        else if (forceNormal && !isLowHealthStateActive)
        {
            // If forcing normal state and already there, ensure transition (can be instant if needed)
            // normalHealthSnapshot.TransitionTo(0f); // Use 0 for instant transition if desired on reset/start
        }
    }

    // Updates the vignette effect smoothly based on current health
    private void UpdateVignette()
    {
        if (vignette == null) return;

        float targetIntensity = defaultVignetteIntensity;
        Color targetColor = defaultVignetteColor;

        // Determine if low health effect should be active
        bool applyLowHealthEffect = (currentHealth <= lowHealthThreshold && currentHealth > 0);
        if (applyLowHealthEffect)
        {
            targetIntensity = lowHealthVignetteIntensity;
            targetColor = lowHealthVignetteColor;
        }

        // Lerp towards the target values using unscaled time for smooth UI feedback
        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, targetIntensity, Time.unscaledDeltaTime * 5f);
        vignette.color.value = Color.Lerp(vignette.color.value, targetColor, Time.unscaledDeltaTime * 5f);
    }

    // Manages the heartbeat audio source based on current health
    private void UpdateHeartbeatState()
    {
        if (heartbeatAudioSource == null) return;

        bool shouldPlay = (currentHealth <= lowHealthThreshold && currentHealth > 0); // Play only if low health and alive

        if (shouldPlay && !heartbeatAudioSource.isPlaying)
        {
            // Start playing if needed
            heartbeatAudioSource.loop = true; // Ensure looping is enabled
            heartbeatAudioSource.Play();
        }
        else if (!shouldPlay && heartbeatAudioSource.isPlaying)
        {
            // Stop playing if not needed
            heartbeatAudioSource.Stop();
        }
    }

    // Updates the health bar fill amount and color
    private void UpdateHealthBarDisplay()
    {
        if (healthBarImage != null)
        {
            healthBarImage.fillAmount = GetHealthPercent(); // Update fill
            UpdateHealthBarColor(); // Update color
        }
    }

    // Sets the health bar color based on health percentage
    private void UpdateHealthBarColor()
    {
        if (healthBarImage == null) return;
        float healthPercent = GetHealthPercent();
        // Determine color based on health percentage ranges
        Color targetColor = (healthPercent > 0.7f) ? Color.green : (healthPercent > 0.3f) ? Color.yellow : Color.red;
        healthBarImage.color = targetColor; // Set the color directly
    }


    // --- Other Methods & Coroutines (Regen, Invincibility, Flash) ---

    // Gets health percentage (0 to 1)
    public float GetHealthPercent()
    {
        return (maxHealth <= 0) ? 0 : currentHealth / maxHealth;
    }

    // Starts the regen coroutine if conditions are met
    private void HandlePassiveRegen()
    {
        // Only start regen if not already regenerating, delay has passed, not dead, and not at max health
        if (!isRegenerating && Time.time - lastDamageTime >= regenDelay && currentHealth < maxHealth && currentHealth > 0)
        {
            StartCoroutine("PassiveRegenCoroutine");
        }
    }

    // Activates invincibility coroutine
    public void ActivateInvincibility(float duration)
    {
        if (!isInvincible && duration > 0) StartCoroutine(InvincibilityCoroutine(duration));
    }

    // Coroutine for temporary invincibility
    private IEnumerator InvincibilityCoroutine(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }

    // Coroutine for the damage flash visual effect
    private IEnumerator DamageFlashRoutine()
    {
        if (playerRenderer?.material != null)
        {
            Color originalEmission = defaultEmissionColor;
            try
            {
                playerRenderer.material.SetColor("_EmissionColor", damageFlashColor);
                // Use unscaled time if flash should work even during pause/slowmo
                yield return new WaitForSecondsRealtime(damageFlashDuration);
            }
            finally
            {
                // Ensure color resets even if coroutine is interrupted
                if (playerRenderer?.material != null)
                {
                    playerRenderer.material.SetColor("_EmissionColor", originalEmission);
                }
            }
        }
        damageFlashCoroutine = null; // Allow flash again
    }

    // Coroutine for passive health regeneration
    private IEnumerator PassiveRegenCoroutine()
    {
        isRegenerating = true;
        float regenStartTime = Time.time;
        while (Time.time - lastDamageTime >= regenDelay && currentHealth < maxHealth && currentHealth > 0)
        {
            // Calculate regen rate (example: Ease In Quad curve)
            float timeSinceRegenStart = Time.time - regenStartTime;
            float normalizedTime = Mathf.Clamp01(timeSinceRegenStart / secondsUntilMaxRegenAchieved);
            float curveFactor = normalizedTime * normalizedTime;
            currentRegenPercentage = Mathf.Lerp(0, maxPassiveRegenPercent, curveFactor);

            // Calculate and apply heal amount for this frame
            float regenAmount = currentRegenPercentage * maxHealth * Time.deltaTime;
            if (regenAmount > 0.001f)
            { // Apply only if significant
                Heal(regenAmount); // Use Heal method (clamps, fires event, updates UI)
                if (currentHealth >= maxHealth) break; // Exit if Heal reached max health
            }
            yield return null; // Wait for next frame
        }
        isRegenerating = false; // Reset flag when loop ends
        currentRegenPercentage = 0f; // Reset debug display
    }
}