using UnityEngine;
using System; // Required for DateTime and TimeSpan

public class DailyRewardManager : MonoBehaviour
{
    public static DailyRewardManager Instance { get; private set; }

    [Header("Reward Settings")]
    [Tooltip("Amount of currency given as a daily reward.")]
    public int dailyRewardAmount = 100; // Example amount

    [Tooltip("Minimum hours required between claims.")]
    public double hoursBetweenClaims = 24.0; // 24 hours default

    private SaveDataManager sdm;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optional: Keep this manager alive across scenes if needed, though likely not necessary if only used in the menu.
        // DontDestroyOnLoad(gameObject);

        // Get reference to SaveDataManager
        sdm = SaveDataManager.Instance;
        if (sdm == null)
        {
            Debug.LogError("[DailyRewardManager] SaveDataManager instance not found!");
        }
    }

    /// <summary>
    /// Checks if the daily reward can be claimed based on the last claimed time.
    /// </summary>
    /// <returns>True if the reward is available, false otherwise.</returns>
    public bool CanClaimReward()
    {
        if (sdm == null || sdm.persistentData == null)
        {
            Debug.LogError("[DailyRewardManager] Cannot check claim status - SaveDataManager not ready.");
            return false;
        }

        long lastClaimTicks = sdm.persistentData.lastRewardClaimTicksUtc;

        // If never claimed before (ticks is 0 or less), it's available.
        if (lastClaimTicks <= 0)
        {
            return true;
        }

        // Convert saved ticks back to a DateTime object (ensure UTC)
        DateTime lastClaimTime = new DateTime(lastClaimTicks, DateTimeKind.Utc);

        // Get current time in UTC
        DateTime currentTimeUtc = DateTime.UtcNow;

        // Calculate the time difference
        TimeSpan timeSinceLastClaim = currentTimeUtc - lastClaimTime;

        // Check if enough time has passed (e.g., 24 hours)
        return timeSinceLastClaim.TotalHours >= hoursBetweenClaims;
    }

    /// <summary>
    /// Attempts to claim the daily reward. Adds currency and updates the last claim time if successful.
    /// </summary>
    /// <returns>The amount of currency rewarded if successful, otherwise 0.</returns>
    public int ClaimReward()
    {
        if (sdm == null || sdm.persistentData == null)
        {
            Debug.LogError("[DailyRewardManager] Cannot claim reward - SaveDataManager not ready.");
            return 0;
        }

        if (CanClaimReward())
        {
            // Grant the reward
            sdm.persistentData.totalCurrency += dailyRewardAmount;

            // Update the last claimed time to now (UTC)
            sdm.persistentData.lastRewardClaimTicksUtc = DateTime.UtcNow.Ticks;

            // Save the game data
            sdm.SaveGame();

            Debug.Log($"[DailyRewardManager] Reward of {dailyRewardAmount} claimed!");
            return dailyRewardAmount;
        }
        else
        {
            Debug.Log("[DailyRewardManager] Reward already claimed or cooldown active.");
            return 0; // Indicate failure or cooldown
        }
    }

    /// <summary>
    /// Gets a formatted string indicating the time remaining until the next claim is available.
    /// Returns "Available Now!" if ready.
    /// </summary>
    public string GetTimeRemainingString()
    {
        if (CanClaimReward())
        {
            return "Available Now!";
        }

        if (sdm == null || sdm.persistentData == null || sdm.persistentData.lastRewardClaimTicksUtc <= 0)
        {
            return "Status Unknown"; // Should ideally be available if ticks <= 0, but safety check
        }

        DateTime lastClaimTime = new DateTime(sdm.persistentData.lastRewardClaimTicksUtc, DateTimeKind.Utc);
        DateTime nextClaimTime = lastClaimTime.AddHours(hoursBetweenClaims);
        TimeSpan timeRemaining = nextClaimTime - DateTime.UtcNow;

        if (timeRemaining.TotalSeconds <= 0)
        {
            return "Available Now!"; // Should be caught by CanClaimReward, but double-check
        }

        // Format the remaining time
        return $"Come back in: {timeRemaining.Hours:D2}h {timeRemaining.Minutes:D2}m {timeRemaining.Seconds:D2}s";

    }
}