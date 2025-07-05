using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the player's score, including optional score multipliers and milestone checks.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Current Score")]
    public int currentScore = 0;

    [Header("Multiplier Variables")]

    public float scoreMultiplier = 1f;
    public bool isMultiplierActive = false;

    [Header("Points Awarded")]
    public int hostileCircleReward = 25;
    public int bomberEnemyReward = 25;
    public int orbiterReward = 25;

    // Optionally keep these events if other scripts rely on them
    public UnityEvent<string, float> OnMultiplierActivated = new UnityEvent<string, float>();
    public UnityEvent<string> OnMultiplierDeactivated = new UnityEvent<string>();

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

    public void AddPoints(int amount) // Universal way to add points
    {
        if (isMultiplierActive)
        {
            amount = Mathf.RoundToInt(amount * scoreMultiplier);
        }

        currentScore += amount;
    }

    public int GetScore()
    {
        return currentScore;
    }

    public void ActivateMultiplier(int multiplier, float duration)
    {
        // If a multiplier is already active, optionally reset the timer by stopping coroutines
        if (isMultiplierActive)
        {
            StopAllCoroutines();
        }

        scoreMultiplier = multiplier;
        isMultiplierActive = true;
        Debug.Log($"Score multiplier x{scoreMultiplier} activated for {duration} seconds.");

        // Invoke optional event (string-based for backward compatibility)
        OnMultiplierActivated?.Invoke("ScoreMultiplier", duration);

        StartCoroutine(DeactivateMultiplierAfterDelay(duration));
    }

    public void RewardPointsForHostileCircle()
    {
        AddPoints(hostileCircleReward);
    }

    public void RewardPointsForBomberEnemy()
    {
        AddPoints(bomberEnemyReward);
    }

    public void RewardPointsForOrbiter()
    {
        AddPoints(orbiterReward);
    }

    private IEnumerator DeactivateMultiplierAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        scoreMultiplier = 1f;
        isMultiplierActive = false;
        Debug.Log("Score multiplier deactivated.");

        // Invoke optional event
        OnMultiplierDeactivated?.Invoke("ScoreMultiplier");
    }
}
