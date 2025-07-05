using UnityEngine;

/// <summary>
/// Manages the player's currency (money).
/// You can set 'currentMoney' to any value you want in the Inspector.
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    [Header("Currency Settings")]
    [Tooltip("The current money in this session or moment. (Could be the same as SaveDataManager totalCurrency.)")]
    public int currentMoney = 1000;

    [Header("Income Multiplier")]
    [Tooltip("Multiplier applied to any income earned (set via store upgrade).")]
    public float incomeMultiplier = 1f;


    public static CurrencyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optional: Persist across scenes if desired.
        // DontDestroyOnLoad(gameObject);

        Debug.Log("[CurrencyManager] Starting currentMoney = " + currentMoney);
    }

    /// <summary>
    /// Adds money to the player's balance.
    /// </summary>
    public void AddMoney(int amount)
    {
        // Multiply the incoming amount by the income multiplier
        int finalAmount = Mathf.RoundToInt(amount * incomeMultiplier);
        currentMoney += finalAmount;
    }

    /// <summary>
    /// Tries to spend the given cost from the player's balance.
    /// Returns true if successful, false if insufficient funds.
    /// </summary>
    public bool TrySpendMoney(int cost)
    {
        if (currentMoney >= cost)
        {
            currentMoney -= cost;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Converts a final score into currency using the universal income multiplier.
    /// </summary>
    public int CalculateCurrencyFromScore(int score)
    {
        // For example, if incomeMultiplier is 2, then currency = score * 2.
        return Mathf.RoundToInt(score * incomeMultiplier);
    }
}
