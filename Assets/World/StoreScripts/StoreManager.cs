using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StoreManager : MonoBehaviour
{
    [Header("References to Other Systems")]
    public SpawnManager spawnManager;
    public PlayerMovement playerMovement;
    public PlayerStamina playerStamina;
    public PlayerHealth playerHealth;
    public WeaponManager weaponManager;
    public GrenadeThrow grenadeThrow;

    [Header("Currency & UI")]
    public CurrencyManager currencyManager;
    public TMP_Text balanceTMP;

    [Header("Store Upgrades")]
    public StoreUpgradeData[] storeUpgrades;

    // Achievement ID for "Upgrade X amount of upgrades to max tier"
    private const string MaxUpgradeAchvID = "6";

    // Define your default/normal color for interactable buttons
    // You might want to serialize this or get it from a button's normalColor state
    [Header("UI Style")]
    public Color defaultButtonColor = Color.white; // Or whatever your default is

    private void Start()
    {
        // Load the player's total currency from SaveDataManager
        if (SaveDataManager.Instance != null)
        {
            currencyManager.currentMoney = SaveDataManager.Instance.persistentData.totalCurrency;
        }

        RefreshBalanceDisplay();

        // Initialize the store items UI and reapply purchased upgrades
        for (int i = 0; i < storeUpgrades.Length; i++)
        {
            StoreUpgradeData upgrade = storeUpgrades[i];

            // Display the upgrade's name
            if (upgrade.upgradeNameTMP != null)
                upgrade.upgradeNameTMP.text = upgrade.upgradeName;

            // Check if we already own some tier in permanent data
            int existingTier = SaveDataManager.Instance.GetUpgradeTier(upgrade.upgradeName);
            upgrade.currentTier = existingTier;

            // If any tier was purchased, reapply the upgrade effect immediately
            if (upgrade.currentTier > 0)
            {
                // It's important that ApplyUpgradeOrAbility can handle being called with
                // the currentTier correctly, even if it's just reapplying the effect for that tier.
                StoreUpgradeEffects.Instance.ApplyUpgradeOrAbility(upgrade);
            }

            // Determine if the upgrade is fully maxed out based on saved data
            bool isFullyMaxedBasedOnSave = (upgrade.isTiered && upgrade.currentTier >= upgrade.maxTiers) ||
                                           (!upgrade.isTiered && upgrade.currentTier > 0); // For non-tiered, currentTier > 0 means purchased/maxed

            if (isFullyMaxedBasedOnSave)
            {
                upgrade.purchased = true; // Ensure purchased flag is set
                    GrayOut(upgrade);
            }
            else
            {
                // Not fully purchased or not purchased at all
                upgrade.purchased = false; // Ensure purchased flag is clear

                // Show the price for the next tier or initial purchase
                int cost = upgrade.GetNextCost();
                if (upgrade.priceTMP != null)
                {
                    upgrade.priceTMP.text = (cost >= 0) ? cost.ToString() : "N/A"; // N/A or "" if something is wrong with cost logic
                }

                // Reset UI to default interactable state
                if (upgrade.upgradeDisplay != null)
                {
                    Button btn = upgrade.upgradeDisplay.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                    }
                    Image img = upgrade.upgradeDisplay.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = defaultButtonColor; // Reset to default color
                    }
                }
            }

            // Update the tier indicator text
            UpdateTierIndicator(upgrade);
        }
    }

    public void OnUpgradeClicked(int upgradeIndex)
    {
        if (upgradeIndex < 0 || upgradeIndex >= storeUpgrades.Length)
            return;

        StoreUpgradeData upgrade = storeUpgrades[upgradeIndex];

        // This 'purchased' flag should be correctly set by Start() or previous OnUpgradeClicked calls
        if (upgrade.purchased)
        {
            Debug.Log($"Upgrade '{upgrade.upgradeName}' is already purchased/maxed out.");
            return;
        }

        int cost = upgrade.GetNextCost();
        if (cost == -1) // Should ideally be caught by the 'purchased' flag above
        {
            Debug.LogWarning($"No more tiers for '{upgrade.upgradeName}', but was not marked as purchased. Correcting state.");
            // Force update UI to maxed state just in case
             GrayOut(upgrade);
            UpdateTierIndicator(upgrade);
            return;
        }

        // Check if the player has enough money
        if (!currencyManager.TrySpendMoney(cost))
        {
            Debug.Log($"Not enough money for '{upgrade.upgradeName}'. Cost: {cost}, Owned: {currencyManager.currentMoney}");
            // Potentially add a visual cue for insufficient funds here
            return;
        }

        // Purchase successful
        Debug.Log($"Purchased '{upgrade.upgradeName}' (Tier {upgrade.currentTier + 1}) for {cost} coins!");

        // Increment the upgrade's tier BEFORE applying effects if effects depend on the NEW tier
        upgrade.currentTier++;

        // Apply the upgrade/ability effects immediately for the new tier
        StoreUpgradeEffects.Instance.ApplyUpgradeOrAbility(upgrade);


        bool justMaxedOut = false;
        if (!upgrade.isTiered || upgrade.currentTier >= upgrade.maxTiers)
        {
            upgrade.purchased = true; // Now it's fully purchased/maxed
            justMaxedOut = true;

            // ACHIEVEMENT: one more max‐level upgrade completed
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.ReportProgress(MaxUpgradeAchvID, 1);
                // SaveDataManager.Instance.SaveGame(); // Consider if saving twice is needed here and below
            }
        }

        // Save the new tier in permanent data
        SaveDataManager.Instance.SetUpgradeTier(upgrade.upgradeName, upgrade.currentTier);

        // Update permanent total currency to keep them in sync
        SaveDataManager.Instance.persistentData.totalCurrency = currencyManager.currentMoney;
        SaveDataManager.Instance.SaveGame(); // Save all changes

        // Update the UI balance
        RefreshBalanceDisplay();

        // Update the specific upgrade's UI
        if (justMaxedOut)
        {
            GrayOut(upgrade);
        }
        else
        {
            // Still tiers left, update price for the NEXT tier
            int newCost = upgrade.GetNextCost();
            if (upgrade.priceTMP != null)
            {
                upgrade.priceTMP.text = (newCost >= 0) ? newCost.ToString() : "Maxed"; // Should show cost
            }
        }

        // Update the tier indicator text after purchase
        UpdateTierIndicator(upgrade);
    }

    private void RefreshBalanceDisplay()
    {
        if (balanceTMP != null && currencyManager != null)
        {
            balanceTMP.text = "$" + currencyManager.currentMoney.ToString("N0"); // N0 for commas
        }
    }

    private void UpdateTierIndicator(StoreUpgradeData upgrade)
    {
        if (upgrade.upgradeTierIndicatorTMP == null) return;

        if (upgrade.isTiered)
        {
            upgrade.upgradeTierIndicatorTMP.text = $"{upgrade.currentTier}/{upgrade.maxTiers}";
        }
        else
        {
            // For non-tiered items, "purchased" implies it's "Maxed" or "Acquired"
            upgrade.upgradeTierIndicatorTMP.text = upgrade.purchased ? "Purchased" : "Not Purchased";
        }
    }

    private void SetMaxedOutGreen(StoreUpgradeData upgrade)
    {
        if (upgrade.upgradeDisplay != null)
        {
            Button btn = upgrade.upgradeDisplay.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = false;
            }

            Image img = upgrade.upgradeDisplay.GetComponent<Image>();
            if (img != null)
            {
                img.color = Color.green; // Set to GREEN
            }
        }

        upgrade.purchased = true; // Redundant if called when already known, but safe
        if (upgrade.priceTMP != null)
        {
            upgrade.priceTMP.text = "Maxed";
        }
        UpdateTierIndicator(upgrade); // Ensure tier indicator is also updated
    }

    private void GrayOut(StoreUpgradeData upgrade)
    {
        if (upgrade.upgradeDisplay != null)
        {
            Button btn = upgrade.upgradeDisplay.GetComponent<Button>();
            if (btn != null)
                btn.interactable = false;

            Image img = upgrade.upgradeDisplay.GetComponent<Image>();
            if (img != null)
                img.color = Color.gray; // Sets to GRAY
        }

        upgrade.purchased = true; // Redundant if called when already known, but safe
        if (upgrade.priceTMP != null)
            upgrade.priceTMP.text = "Maxed";

        UpdateTierIndicator(upgrade); // Ensure tier indicator is also updated
    }
}