using UnityEngine;
using System.Collections;

/// <summary>
/// Manages the Score Multiplier ability, activating a multiplier and updating the UI accordingly.
/// </summary>
public class ScoreMultiplierAbility : MonoBehaviour
{
    [Header("Multiplier Settings")]
    public int multiplierValue = 2;
    public float multiplierDuration = 10f;

    [Header("Point Settings")]
    public int pointValue = 10; // Points awarded when this square is destroyed

    [Header("Unique Square Ability Parameters")]
    public AudioClip DestructionSFX;
    public GameObject DestructionParticleEffect;

    [Header("Reference to UniqueSquareType")]
    public UniqueSquareType uniqueSquareType; 
    private UniqueSquareManager uniqueSquareManager;

    private void Awake()
    {
        uniqueSquareManager = FindObjectOfType<UniqueSquareManager>();
        if (uniqueSquareManager != null) { 
            multiplierDuration *= uniqueSquareManager.universalUniquesquareAbilityDurationMultiplier;
        } else {
            Debug.LogWarning("ScoreMultiplierAbility could not find uniquesquareManager in the scene");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Bullet"))
        {
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.ReportProgress("2", 1);
            }
            // Activate the multiplier
            Debug.Log("Score multiplier ability activated");
            ScoreManager.Instance.ActivateMultiplier(multiplierValue, multiplierDuration);

            // Activate UI icon
            if (UIManager.Instance != null && uniqueSquareType != null)
            {
                UIManager.Instance.OnAbilityActivated(uniqueSquareType, multiplierDuration);
            }
            else
            {
                Debug.LogWarning("UIManager.Instance is null or uniqueSquareType is not assigned.");
            }

            // Delegate awarding points and destruction to SpawnManager
            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.DestroyUniqueSquare(gameObject, pointValue / 2);
            }
            else
            {
                // Fallback if no SpawnManager
                Debug.LogWarning("SpawnManager.Instance is null. Destroying locally.");
                Destroy(gameObject);
            }

            // Start coroutine to deactivate the UI icon after duration
            StartCoroutine(DeactivateAbilityAfterDelay(multiplierDuration));
        }
    }

    /// <summary>
    /// Coroutine to deactivate the ability icon after a delay.
    /// </summary>
    private IEnumerator DeactivateAbilityAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Deactivate the UI icon
        if (UIManager.Instance != null && uniqueSquareType != null)
        {
            UIManager.Instance.OnAbilityDeactivated(uniqueSquareType);
        }
    }
}
