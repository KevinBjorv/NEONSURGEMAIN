using UnityEngine;
using TMPro;

/// <summary>
/// Holds data for a single store upgrade/ability entry.
/// Allows you to select an enum effectType and specify effectValues.
/// </summary>
[System.Serializable]
public class StoreUpgradeData
{
    [Header("Upgrade Info")]
    public string upgradeName;

    [Tooltip("Check if this upgrade is tiered (multiple levels) or single purchase.")]
    public bool isTiered;

    [Tooltip("If 'isTiered' is true, how many tiers exist?")]
    public int maxTiers;

    [Tooltip("An array of prices for each tier. If single purchase, only use index 0.")]
    public int[] tierPrices;

    [Header("Effect Settings")]
    [Tooltip("Select which effect to apply from the dropdown.")]
    public StoreEffectType effectType;

    [Tooltip("Value(s) for each tier. If single purchase, only use index 0.")]
    public float[] effectValues;

    [Header("UI References")]
    [Tooltip("The overall button/display for this upgrade.")]
    public GameObject upgradeDisplay;

    [Tooltip("TMP field that displays the price (only the number).")]
    public TMP_Text priceTMP;

    [Tooltip("TMP field that displays the upgrade name.")]
    public TMP_Text upgradeNameTMP;

    [Tooltip("TMP field that displays the current tier indicator (or purchase state).")]
    public TMP_Text upgradeTierIndicatorTMP;

    [HideInInspector] public int currentTier = 0;
    [HideInInspector] public bool purchased = false;

    /// <summary>
    /// Returns the cost for the next tier or the single purchase.
    /// If at max tier or purchased, returns -1.
    /// </summary>
    public int GetNextCost()
    {
        if (!isTiered)
        {
            if (purchased) return -1;
            return tierPrices.Length > 0 ? tierPrices[0] : 0;
        }
        else
        {
            if (currentTier >= maxTiers) return -1;
            if (currentTier >= tierPrices.Length) return -1;
            return tierPrices[currentTier];
        }
    }
}
