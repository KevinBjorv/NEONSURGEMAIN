using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Core UI References")]
    public TextMeshProUGUI scoreText;
    public Slider staminaBar;
    public Slider healthBar;
    public TextMeshProUGUI timerText;
    public GameObject leaderboardPanel;
    public Transform leaderboardContent;
    public TextMeshProUGUI leaderboardEntryPrefab;

    [Header("Score Pop Settings")]
    [Tooltip("Multiplier for how much the score text should grow when the score increases.")]
    public float scorePopScaleIncrease = 1.2f;
    [Tooltip("Duration of the pop effect animation in seconds.")]
    public float scorePopDuration = 0.3f;

    [Header("Milestone Settings")]
    public int[] milestones = { 50, 100, 250, 500, 1000 };
    private Dictionary<int, float> milestoneTimes = new Dictionary<int, float>();

    public float elapsedTime = 0f;
    private int previousScore = 0;
    private Vector3 originalScoreScale;
    private Coroutine popCoroutine;


    // ── Run‑Time “Survive X Seconds” Achievement ──
    private const string timeSurvivalAchvID = "8";
    private HashSet<int> _timeTiersTriggered;

    bool isMobile = Application.isMobilePlatform;

    /// <summary>
    /// For storing who is in each ability slot (icon + optional slider)
    /// </summary>
    [System.Serializable]
    public class AbilitySlotUI
    {
        public Image abilityImage;
        public Slider durationSlider;

        [HideInInspector] public UniqueSquareType assignedType;
        [HideInInspector] public float remainingTime;
        [HideInInspector] public float totalTime;
        [HideInInspector] public bool isActive;
    }

    [Header("Ability Slots")]
    public AbilitySlotUI primarySlot;
    public AbilitySlotUI secondarySlot;
    public AbilitySlotUI tertiarySlot;

    /// <summary>
    /// A single-slot queue: if all 3 slots are full and a new ability is activated,
    /// we store it here (if empty) or ignore it (if already occupied).
    /// </summary>
    private (UniqueSquareType uniqueType, float duration)? queuedAbility = null;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Store original scale of the score text
        if (scoreText != null)
            originalScoreScale = scoreText.transform.localScale;

        _timeTiersTriggered = new HashSet<int>();
    }

    private void Start()
    {
        leaderboardPanel.SetActive(false);
        ClearSlot(primarySlot);
        ClearSlot(secondarySlot);
        ClearSlot(tertiarySlot);
        if(isMobile) placeElementsForMobile();

    }

    private void placeElementsForMobile()
    {

    }

    private void Update()
    {
        // 1) Timer update using unscaledDeltaTime * Time.timeScale so that it reflects the current timeScale
        elapsedTime += Time.unscaledDeltaTime * Time.timeScale;
        if (timerText != null)
        {
            timerText.text = elapsedTime.ToString("F1") + "s";
        }

        CheckTimeSurvivalAchievement();

        // 2) Health/Stamina
        if (PlayerHealth.Instance != null && healthBar != null)
        {
            healthBar.value = PlayerHealth.Instance.GetHealthPercent();
        }
        if (PlayerStamina.Instance != null && staminaBar != null)
        {
            staminaBar.value = PlayerStamina.Instance.GetStaminaPercent();
        }

        // 3) Score with pop effect
        if (ScoreManager.Instance != null && scoreText != null)
        {
            int currentScore = ScoreManager.Instance.GetScore();
            scoreText.text = "Score: " + currentScore.ToString();

            if (currentScore > previousScore)
            {
                // Trigger pop effect only if score increased
                if (popCoroutine != null)
                {
                    StopCoroutine(popCoroutine);
                    scoreText.transform.localScale = originalScoreScale;
                }
                popCoroutine = StartCoroutine(ScorePopEffect());
            }
            previousScore = currentScore;
        }

        // 4) Update each slot (ability slots use unscaledDeltaTime for real-time countdown)
        UpdateSlotDuration(primarySlot);
        UpdateSlotDuration(secondarySlot);
        UpdateSlotDuration(tertiarySlot);
    }

    private void CheckTimeSurvivalAchievement()
    {
        // 1) Grab the achievement definition
        var def = AchievementManager.Instance.allAchievements
                      .FirstOrDefault(a => a.id == timeSurvivalAchvID);
        if (def == null) return;

        // 2) Grab the persistent entry (so we know which tiers are already done)
        var entry = SaveDataManager.Instance.GetAchievement(timeSurvivalAchvID);

        // 3) For each tier *after* the one already unlocked...
        for (int tier = entry.currentTier + 1; tier < def.tiers.Length; tier++)
        {
            int neededSeconds = def.tiers[tier].targetValue;

            // if we haven't already triggered this tier this run
            if (elapsedTime >= neededSeconds && !_timeTiersTriggered.Contains(tier))
            {
                _timeTiersTriggered.Add(tier);

                // 4) report it (passing the full threshold so tier completes immediately)
                AchievementManager.Instance.ReportProgress(timeSurvivalAchvID, neededSeconds);
                SaveDataManager.Instance.SaveGame();
            }
        }
    }

    private IEnumerator ScorePopEffect()
    {
        Vector3 targetScale = originalScoreScale * scorePopScaleIncrease;
        float halfDuration = scorePopDuration / 2f;
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            scoreText.transform.localScale = Vector3.Lerp(originalScoreScale, targetScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            scoreText.transform.localScale = Vector3.Lerp(targetScale, originalScoreScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        scoreText.transform.localScale = originalScoreScale;
        popCoroutine = null;
    }

    #region Ability Management

    public void OnAbilityActivated(UniqueSquareType uniqueType, float duration)
    {
        if (uniqueType == null)
        {
            Debug.LogWarning("OnAbilityActivated called with null UniqueSquareType.");
            return;
        }

        if (SlotHasType(primarySlot, uniqueType) ||
            SlotHasType(secondarySlot, uniqueType) ||
            SlotHasType(tertiarySlot, uniqueType))
        {
            Debug.Log($"Ability '{uniqueType.squareName}' is already displayed. Ignoring duplicate.");
            return;
        }

        if (!primarySlot.isActive) { AssignSlot(primarySlot, uniqueType, duration); }
        else if (!secondarySlot.isActive) { AssignSlot(secondarySlot, uniqueType, duration); }
        else if (!tertiarySlot.isActive) { AssignSlot(tertiarySlot, uniqueType, duration); }
        else
        {
            if (!queuedAbility.HasValue)
            {
                queuedAbility = (uniqueType, duration);
                Debug.Log($"All slots are full. Queuing '{uniqueType.squareName}'.");
            }
            else
            {
                Debug.LogWarning($"All slots + queue are full. Ignoring '{uniqueType.squareName}'.");
            }
        }
    }

    public void OnAbilityDeactivated(UniqueSquareType uniqueType)
    {
        if (uniqueType == null) return;
        if (SlotHasType(primarySlot, uniqueType)) ClearSlot(primarySlot);
        if (SlotHasType(secondarySlot, uniqueType)) ClearSlot(secondarySlot);
        if (SlotHasType(tertiarySlot, uniqueType)) ClearSlot(tertiarySlot);
    }

    private void UpdateSlotDuration(AbilitySlotUI slot)
    {
        if (!slot.isActive) return;

        // If totalTime is <= 0, it's a permanent ability (duration 0) or improperly set.
        // Timing logic only applies if totalTime > 0 (set for positive or negative durations).
        if (slot.totalTime <= 0f) return;

        // --- Countdown Logic ---
        slot.remainingTime -= Time.unscaledDeltaTime;

        if (slot.remainingTime <= 0f)
        {
            // Important: Use the type stored in the slot for deactivation
            UniqueSquareType typeToDeactivate = slot.assignedType;
            // Clear the slot *before* potentially trying to reactivate from queue
            ClearSlot(slot);
            // Note: Directly calling OnAbilityDeactivated here could cause recursion
            // if the deactivation itself triggers another activation immediately.
            // Clearing the slot is sufficient as the timer ran out.
            // We might still need OnAbilityDeactivated for *external* calls.

            // --- Check Queue ---
            // Check if we can display the queued ability now that a slot is free
            // Moved this check here to immediately fill the slot if possible
            CheckAndAssignQueuedAbility();

        }
        // --- Slider Update Logic ---
        // Only update the slider's value if it exists AND is currently active
        else if (slot.durationSlider != null && slot.durationSlider.gameObject.activeSelf)
        {
            // Update slider based on remaining time
            slot.durationSlider.value = slot.remainingTime / slot.totalTime;
        }
    }

    private void AssignSlot(AbilitySlotUI slot, UniqueSquareType uniqueType, float duration)
    {
        bool showSlider = duration > 0f; // Only show slider for positive durations
        bool isTimed = duration != 0f;    // Is it timed at all (positive or negative)?

        slot.isActive = true;
        slot.assignedType = uniqueType;

        // Assign Icon
        if (slot.abilityImage != null && uniqueType.abilityIcon != null)
        {
            slot.abilityImage.sprite = uniqueType.abilityIcon;
            slot.abilityImage.enabled = true;
            slot.abilityImage.preserveAspect = true;
        }
        else if (slot.abilityImage != null) // Ensure image is hidden if icon is null
        {
            slot.abilityImage.enabled = false;
            slot.abilityImage.sprite = null;
        }


        // Handle Timing based on duration sign
        if (isTimed)
        {
            // Use absolute value for actual timing
            slot.totalTime = Mathf.Abs(duration);
            slot.remainingTime = Mathf.Abs(duration);
        }
        else // duration == 0f -> Permanent, requires manual deactivation
        {
            slot.totalTime = 0f;
            slot.remainingTime = 0f;
        }

        // Handle Slider Visibility
        if (slot.durationSlider != null)
        {
            slot.durationSlider.gameObject.SetActive(showSlider);
            if (showSlider) // Only set value if slider is active
            {
                slot.durationSlider.value = 1f;
            }
        }

        // Updated Debug Log for clarity
        string durationType = duration > 0f ? "Timed with Slider" : (duration < 0f ? "Timed without Slider" : "Permanent");
        Debug.Log($"UIManager: Assigned '{uniqueType.squareName}' to slot '{slot.abilityImage?.name}'. Type: {durationType} (duration={duration}, actualTime={Mathf.Abs(duration)}).");
    }



    // Add this new method to UIManager class
    private void CheckAndAssignQueuedAbility()
    {
        if (queuedAbility.HasValue)
        {
            // Check if any slot is now free
            if (!primarySlot.isActive || !secondarySlot.isActive || !tertiarySlot.isActive)
            {
                // Dequeue and attempt to activate
                var (uType, dur) = queuedAbility.Value;
                queuedAbility = null; // Clear the queue
                Debug.Log($"UIManager: Dequeuing and attempting to activate '{uType.squareName}'");
                OnAbilityActivated(uType, dur); // Re-call OnAbilityActivated to find the free slot
            }
        }
    }

    // Modify the Update method to remove the queue check - it's now handled when slots free up.
    

    // Modify ClearSlot slightly to also trigger a queue check
    private void ClearSlot(AbilitySlotUI slot)
    {
        // Keep existing code:
        slot.isActive = false;
        slot.assignedType = null;
        slot.totalTime = 0f;
        slot.remainingTime = 0f;
        if (slot.abilityImage != null)
        {
            slot.abilityImage.enabled = false;
            slot.abilityImage.sprite = null;
        }
        if (slot.durationSlider != null)
        {
            slot.durationSlider.gameObject.SetActive(false);
        }

        // Add this line:
        // Check queue immediately after clearing a slot externally (e.g. via OnAbilityDeactivated)
        // This covers cases where deactivation doesn't happen via timer expiry.
        CheckAndAssignQueuedAbility();
    }



    private bool SlotHasType(AbilitySlotUI slot, UniqueSquareType type)
    {
        return slot.isActive && slot.assignedType == type;
    }
    public void ShowAbilityForTime(UniqueSquareType type, float duration)
    {
        OnAbilityActivated(type, duration);
        StartCoroutine(AbilityTimeout(type, duration));
    }

    private IEnumerator AbilityTimeout(UniqueSquareType type, float duration)
    {
        yield return new WaitForSeconds(duration);
        OnAbilityDeactivated(type);
    }

    #endregion

    #region Leaderboard & Milestones

    public void RecordMilestone(int milestone, float timeReached)
    {
        if (!milestoneTimes.ContainsKey(milestone))
        {
            milestoneTimes[milestone] = timeReached;
            UpdateLeaderboardEntries();
            Debug.Log($"UIManager: Milestone {milestone} recorded at {timeReached:F1} seconds.");
        }
    }

    private void UpdateLeaderboardEntries()
    {
        foreach (Transform child in leaderboardContent)
        {
            Destroy(child.gameObject);
        }
        foreach (var kvp in milestoneTimes)
        {
            var entry = Instantiate(leaderboardEntryPrefab, leaderboardContent);
            entry.text = $"Reached {kvp.Key} points at {kvp.Value:F1}s";
        }
    }

    public float GetCurrentTime()
    {
        return elapsedTime;
    }

    #endregion
}
