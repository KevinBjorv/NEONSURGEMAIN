using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UpgradeEntry
{
    public string upgradeName;
    public int tier;
}

[Serializable]
public class AchievementEntry
{
    public string id;
    public int currentTier; // 0-based index (-1 = not started)
    public int currentValue; // Progress towards the next tier
}

/// <summary>
/// Data to be saved permanently across sessions.
/// </summary>
[Serializable]
public class SaveData
{
    // ---- Permanent Gameplay Progress ----
    public int highScore;
    public int lastScore;
    public int totalCurrency;

    public int playerLevel;
    public long currentXP;
    public long xpNeededForNextLevel; // Represents the XP needed to go from current level to the next

    // Store purchased upgrades as a list of UpgradeEntry objects.
    public List<UpgradeEntry> purchasedUpgradesList = new List<UpgradeEntry>();

    // ---- Saved Settings ----
    public bool showFps;
    public bool vsyncOn;
    public float masterVolume;
    public float musicVolume;
    public float sfxVolume;
    public bool bloomOn;
    public int resolutionIndex;
    public int fullscreenModeIndex;
    public int fpsIndex;
    public int colorblindModeIndex;
    public bool screenShakeOn; 

    public List<AchievementEntry> achievements = new List<AchievementEntry>();

    public long lastRewardClaimTicksUtc = 0; // When daily reward was last claimed
}

/// <summary>
/// Central manager that handles both ephemeral "session" data and permanent "SaveData".
/// </summary>
public class SaveDataManager : MonoBehaviour
{
    public static SaveDataManager Instance { get; private set; }

    // -------------------------
    // Session (temporary) values:
    public int enemiesKilled;       // Shown on summary only
    public int finalScore;          // Shown on summary, tested vs high score
    public float sessionTime;       // Shown on summary
    public float xpGainedThisSession;   // Used for summary & added to permanent XP
    public int currencyGainedThisSession; // Derived from finalScore via the universal income multiplier

    // -------------------------
    // Permanent data that persists across sessions
    [Header("Permanent Save Data")]
    public SaveData persistentData;
    public bool fileExisted { get; private set; } = false;

    // -------------------------
    // XP / Leveling Configuration
    [Header("Level/XP Configuration")]
    [Tooltip("Factor 'A' in the formula: TotalXP = A*L + B*(L^C)")]
    public float xpLinearFactorA = 100f; // Example value for A
    [Tooltip("Factor 'B' in the formula: TotalXP = A*L + B*(L^C)")]
    public float xpExponentialFactorB = 50f; // Example value for B
    [Tooltip("Exponent 'C' in the formula: TotalXP = A*L + B*(L^C)")]
    public float xpLevelExponentC = 2.0f; // Example value for C (quadratic)

    [Tooltip("How much XP is gained per point scored in a session.")]
    public float xpPerScorePoint = 2.5f;

    // -------------------------
    // Path to our save file (JSON).
    private string saveFilePath;


    private const string totalCurrencyAchvID = "5";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        saveFilePath = Application.dataPath + "/saveData.json";
#else
        saveFilePath = Application.persistentDataPath + "/saveData.json";
#endif
        LoadGame();
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (persistentData == null)
            persistentData = new SaveData();

        // Initialize level and XP if it's the first run or data is invalid
        if (persistentData.playerLevel < 1)
        {
            persistentData.playerLevel = 1;
            persistentData.currentXP = 0L;
            // Calculate XP needed to reach level 2 using the new formula
            persistentData.xpNeededForNextLevel = CalculateXpForNextLevel(persistentData.playerLevel);
        }
        // Ensure xpNeededForNextLevel is valid if loading existing data (in case formula changed)
        else if (persistentData.xpNeededForNextLevel <= 0L)
        {
            persistentData.xpNeededForNextLevel = CalculateXpForNextLevel(persistentData.playerLevel);
        }


        if (persistentData.purchasedUpgradesList == null)
        {
            persistentData.purchasedUpgradesList = new List<UpgradeEntry>();
        }

        if (persistentData.achievements == null)
        {
            persistentData.achievements = new List<AchievementEntry>();
        }
    }

    /// <summary>
    /// Calculates the *total* XP required to *reach* a specific target level.
    /// Formula: TotalXP = A * level + B * (level ^ C)
    /// </summary>
    /// <param name="level">The target level.</param>
    /// <returns>Total XP required to attain that level from level 0.</returns>
    public long CalculateTotalXpForLevel(int level) // Return long
    {
        if (level <= 0) return 0L; // Return long literal 0

        // Use double for intermediate calculations to preserve precision
        double linearPart = (double)xpLinearFactorA * level;
        double exponentialPart = (double)xpExponentialFactorB * Math.Pow(level, xpLevelExponentC);

        // Round the final result and cast to long
        return (long)Math.Round(linearPart + exponentialPart);
    }

    /// <summary>
    /// Calculates the amount of XP needed to advance from the currentLevel to the next level.
    /// This is the *difference* between the total XP needed for the next level and the total XP needed for the current level.
    /// </summary>
    /// <param name="currentLevel">The player's current level.</param>
    /// <returns>XP required to reach currentLevel + 1.</returns>
    public long CalculateXpForNextLevel(int currentLevel) // Return long
    {
        if (currentLevel < 1) currentLevel = 1;

        int nextLevel = currentLevel + 1;
        long totalXpForNextLevel = CalculateTotalXpForLevel(nextLevel);     // Gets long
        long totalXpForCurrentLevel = CalculateTotalXpForLevel(currentLevel); // Gets long

        long difference = totalXpForNextLevel - totalXpForCurrentLevel;

        // Ensure minimum XP needed is at least 1 (long)
        return Math.Max(1L, difference); // Use long literal 1
    }


    /// <summary>
    /// Clears out the current session stats (call this at the start of a new run).
    /// </summary>
    public void ResetSessionData()
    {
        enemiesKilled = 0;
        finalScore = 0;
        sessionTime = 0f;
        xpGainedThisSession = 0f;
        currencyGainedThisSession = 0;
    }


    /// <summary>
    /// Called at the end of a session to gather final stats.
    /// </summary>
    public void CollectAllData()
    {
        if (ScoreManager.Instance != null)
        {
            finalScore = ScoreManager.Instance.GetScore();
        }
        else
        {
            finalScore = 0; // Default if ScoreManager isn't available
        }

        // Compute XP gained from finalScore
        xpGainedThisSession = finalScore * xpPerScorePoint;

        // Compute currency gained using the universal income multiplier.
        if (CurrencyManager.Instance != null)
            currencyGainedThisSession = CurrencyManager.Instance.CalculateCurrencyFromScore(finalScore);
        else
            currencyGainedThisSession = finalScore; // Fallback if CurrencyManager isn't available
    }

    /// <summary>
    /// Merges session data into permanent data then writes the data to disk.
    /// Handles level ups using the new XP formula.
    /// </summary>
    public void ApplySessionResultsAndSave()
    {
        persistentData.lastScore = finalScore;
        if (finalScore > persistentData.highScore)
        {
            persistentData.highScore = finalScore;
        }

        persistentData.totalCurrency += currencyGainedThisSession;
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.ReportProgress(totalCurrencyAchvID, currencyGainedThisSession);
        }

        long xpGainedLong = (long)Mathf.Round(xpGainedThisSession);
        if (xpGainedLong < 0) xpGainedLong = 0; // Ensure non-negative gain

        persistentData.currentXP += xpGainedLong; // Add long

        int safetyBreak = 0;
        int maxLevelUps = 1000;

        while (persistentData.currentXP >= persistentData.xpNeededForNextLevel && persistentData.xpNeededForNextLevel > 0L && safetyBreak < maxLevelUps)
        {
            Debug.Log($"[SaveData] PROCESSING LEVEL UP: Level {persistentData.playerLevel} -> {persistentData.playerLevel + 1}. Subtracting {persistentData.xpNeededForNextLevel} XP. Current XP before sub: {persistentData.currentXP}");
            persistentData.currentXP -= persistentData.xpNeededForNextLevel; // Subtract long
            persistentData.playerLevel++;
            long newNeedXP = CalculateXpForNextLevel(persistentData.playerLevel); // Gets long
            persistentData.xpNeededForNextLevel = newNeedXP;
            Debug.Log($"[SaveData] LEVEL UP COMPLETE: New Level={persistentData.playerLevel}, New NeedXP={newNeedXP}, Remaining CurrentXP={persistentData.currentXP}");

            if (newNeedXP <= 0L)
            {
                Debug.LogError($"[SaveData Apply] Calculated needed XP is {newNeedXP} for level {persistentData.playerLevel}. Aborting level up loop.");
                break;
            }
            safetyBreak++;
        }
        if (safetyBreak >= maxLevelUps)
        {
            Debug.LogError($"[SaveData Apply] Level up loop hit safety break limit ({maxLevelUps}). Check XP formula or gain amount.");
        }
        // --- End Level Up Check ---

        Debug.Log($"[SaveData] >>> FINAL STATE PRE-SAVE CHECKPOINT <<< Level={persistentData.playerLevel}, CurrentXP={persistentData.currentXP}, NeededXP={persistentData.xpNeededForNextLevel}");
        SaveGame();
    }

    public void SaveGame()
    {
        try
        {
            string json = JsonUtility.ToJson(persistentData, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"[SaveDataManager] Game Saved to: {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveDataManager] Failed to save file: " + e);
        }
    }

    public void LoadGame()
    {
        if (!File.Exists(saveFilePath))
        {
            Debug.Log("[SaveDataManager] No save file found. A new one will be created on next save.");
            persistentData = new SaveData();
            fileExisted = false;
            // Initialization will happen in InitializeIfNeeded
            return;
        }

        try
        {
            string json = File.ReadAllText(saveFilePath);
            persistentData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("[SaveDataManager] Game Loaded.");
            fileExisted = true;
            // Post-load initialization/validation happens in InitializeIfNeeded
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveDataManager] Failed to load file: " + e + ". Creating new save data.");
            persistentData = new SaveData();
            fileExisted = false;
            // Initialization will happen in InitializeIfNeeded
        }
    }

    /// <summary>
    /// Returns the purchased tier for an upgrade. Returns 0 if not found.
    /// </summary>
    public int GetUpgradeTier(string upgradeName)
    {
        if (persistentData.purchasedUpgradesList != null)
        {
            foreach (UpgradeEntry entry in persistentData.purchasedUpgradesList)
            {
                if (entry.upgradeName == upgradeName)
                    return entry.tier;
            }
        }
        return 0; // Default to 0 if not purchased or list is null
    }

    /// <summary>
    /// Sets the tier for a given upgrade, adding it if it doesn't exist.
    /// </summary>
    public void SetUpgradeTier(string upgradeName, int tier)
    {
        if (persistentData.purchasedUpgradesList == null)
        {
            persistentData.purchasedUpgradesList = new List<UpgradeEntry>();
        }

        bool found = false;
        foreach (UpgradeEntry entry in persistentData.purchasedUpgradesList)
        {
            if (entry.upgradeName == upgradeName)
            {
                entry.tier = tier;
                found = true;
                break;
            }
        }

        if (!found)
        {
            UpgradeEntry newEntry = new UpgradeEntry { upgradeName = upgradeName, tier = tier };
            persistentData.purchasedUpgradesList.Add(newEntry);
        }
    }

    /// <summary>
    /// Saves all current game settings to the persistent data object.
    /// </summary>
    public void SaveSettingsToPermanentData(
        bool showFps,
        bool vsyncOn,
        float masterVolume,
        float musicVolume,
        float sfxVolume,
        bool bloomOn,
        int resolutionIndex,
        int fullscreenModeIndex,
        int fpsIndex,
        int colorblindModeIndex,
        bool screenShakeOn)
    {
        // Ensure data object exists
        if (persistentData == null) persistentData = new SaveData();

        persistentData.showFps = showFps;
        persistentData.vsyncOn = vsyncOn;
        persistentData.masterVolume = masterVolume;
        persistentData.musicVolume = musicVolume;
        persistentData.sfxVolume = sfxVolume;
        persistentData.bloomOn = bloomOn;
        persistentData.resolutionIndex = resolutionIndex;
        persistentData.fullscreenModeIndex = fullscreenModeIndex;
        persistentData.fpsIndex = fpsIndex;
        persistentData.colorblindModeIndex = colorblindModeIndex;
        persistentData.screenShakeOn = screenShakeOn;

        // Save immediately after changing settings
        SaveGame();
    }

    /// <summary>
    /// Gets achievement data, creating it if it doesn't exist.
    /// </summary>
    public AchievementEntry GetAchievement(string id)
    {
        if (persistentData.achievements == null)
            persistentData.achievements = new List<AchievementEntry>();

        foreach (var a in persistentData.achievements)
            if (a.id == id) return a;

        // Achievement not found, create and add it
        var entry = new AchievementEntry { id = id, currentTier = -1, currentValue = 0 };
        persistentData.achievements.Add(entry);
        return entry;
    }

    // Add this new public method inside the SaveDataManager class

    /// <summary>
    /// Simulates the XP gain and level-up process based on current persistent data
    /// without actually modifying it. Used to predict the final state for UI animations.
    /// </summary>
    /// <param name="xpGained">The amount of XP gained in the session.</param>
    /// <param name="finalLevel">OUTPUT: The predicted level after applying XP.</param>
    /// <param name="finalCurrentXp">OUTPUT: The predicted remaining XP after applying XP and level-ups.</param>
    /// <param name="finalXpNeeded">OUTPUT: The predicted XP needed for the level *after* the finalLevel.</param>
    public void SimulateXpGain(float xpGained, out int finalLevel, out long finalCurrentXp, out long finalXpNeeded) // Output long
    {
        int simLevel = persistentData.playerLevel;
        long simCurrentXp = persistentData.currentXP; // Use long
        long simXpNeeded = persistentData.xpNeededForNextLevel; // Use long

        // Safety check / initial calculation if neededXP seems invalid
        if (simXpNeeded <= 0L && simLevel >= 1)
        {
            simXpNeeded = CalculateXpForNextLevel(simLevel);
            Debug.LogWarning($"[SimulateXpGain] Initial xpNeededForNextLevel was invalid for level {simLevel}, recalculated to {simXpNeeded}");
        }
        else if (simLevel < 1)
        {
            simLevel = 1;
            simCurrentXp = 0L;
            simXpNeeded = CalculateXpForNextLevel(1);
            Debug.LogWarning($"[SimulateXpGain] Initial level was < 1, resetting simulation start state.");
        }

        // Convert float gain to long when adding
        long xpGainedLong = (long)Mathf.Round(xpGained);
        if (xpGainedLong < 0) xpGainedLong = 0;

        simCurrentXp += xpGainedLong; // Add long

        int safetyBreak = 0;
        int maxLevelUpsPerSim = 1000;

        while (simCurrentXp >= simXpNeeded && simXpNeeded > 0L && safetyBreak < maxLevelUpsPerSim)
        {
            simCurrentXp -= simXpNeeded; // Subtract long
            simLevel++;
            simXpNeeded = CalculateXpForNextLevel(simLevel); // Gets long

            if (simXpNeeded <= 0L)
            {
                Debug.LogError($"[SimulateXpGain] Calculated needed XP is {simXpNeeded} for level {simLevel}. Aborting simulation loop.");
                break;
            }
            safetyBreak++;
        }
        if (safetyBreak >= maxLevelUpsPerSim)
        {
            Debug.LogError($"[SimulateXpGain] Simulation hit safety break limit ({maxLevelUpsPerSim}). Check XP formula or gain amount.");
        }

        finalLevel = simLevel;
        finalCurrentXp = simCurrentXp; // Assign long
        finalXpNeeded = simXpNeeded; // Assign long

        // Debug.Log($"[SimulateXpGain] Simulation Result: EndLevel={finalLevel}, EndXP(long)={finalCurrentXp}, EndNeedForNext(long)={finalXpNeeded}");
    }
}