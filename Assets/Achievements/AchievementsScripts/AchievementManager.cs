using UnityEngine;
using System.Collections.Generic;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    [Tooltip("Populate in the Inspector with all achievement ScriptableObjects")]
    public AchievementDefinition[] allAchievements;

    private const string masterAchvID = "12";

    private readonly Dictionary<string, AchievementDefinition> lookup = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var a in allAchievements)
            lookup[a.id] = a;
    }

    /* Call this from gameplay: */
    public void ReportProgress(string id, int delta = 1)
    {
        if (!lookup.TryGetValue(id, out var def)) return;

        var entry = SaveDataManager.Instance.GetAchievement(id);
        if (entry.currentTier >= def.tiers.Length - 1) return;          // already maxed

        entry.currentValue += delta;
        var nextTier = def.tiers[entry.currentTier + 1];

        if (entry.currentValue >= nextTier.targetValue)
        {
            entry.currentTier++;
            entry.currentValue = 0;

            CurrencyManager.Instance.AddMoney(nextTier.moneyReward);

            if (entry.currentTier == def.tiers.Length - 1 &&
                !string.IsNullOrEmpty(def.unlockItemId))
            {
                Debug.Log($"Unlocked item {def.unlockItemId}");
            }
            CheckMasterAchievement();
        }
    }

    private void CheckMasterAchievement()
    {
        // 1) get the definition & entry for ID=12
        if (!lookup.TryGetValue(masterAchvID, out var masterDef)) return;
        var masterEntry = SaveDataManager.Instance.GetAchievement(masterAchvID);

        // already done?
        if (masterEntry.currentTier >= masterDef.tiers.Length - 1)
            return;

        // 2) make sure every OTHER achievement is at its max tier
        foreach (var def in allAchievements)
        {
            if (def.id == masterAchvID)
                continue;

            var e = SaveDataManager.Instance.GetAchievement(def.id);
            if (e.currentTier < def.tiers.Length - 1)
                return;  // one isn’t done yet, bail out
        }

        // 3) if we get here, *all* others are maxed → award ID12
        var nextTier = masterDef.tiers[masterEntry.currentTier + 1];
        ReportProgress(masterAchvID, nextTier.targetValue);
        SaveDataManager.Instance.SaveGame();
    }
}
