using UnityEngine;
using System.Collections;

public class InvincibilityAbility : MonoBehaviour
{
    [Header("Invincibility Settings")]
    public float customInvincibilityDuration = 5f;

    [Header("Point Settings")]
    public int pointValue = 10; // Points awarded when this square is destroyed

    [Header("Unique Square Ability Parameters")]
    public bool UseDissolveEffect = false;
    public AudioClip DestructionSFX;
    public GameObject DestructionParticleEffect;

    // Reference to UniqueSquareType so we can display the correct UI icon
    [Header("Reference to UniqueSquareType (for UI icon, etc.)")]
    public UniqueSquareType uniqueSquareType;
    private UniqueSquareManager uniqueSquareManager;
    private void Awake()
    {
        uniqueSquareManager = FindObjectOfType<UniqueSquareManager>();
        if (uniqueSquareManager != null)
        {
            customInvincibilityDuration *= uniqueSquareManager.universalUniquesquareAbilityDurationMultiplier;
        }
        else
        {
            Debug.LogWarning("InvincibilityAbility could not find uniquesquareManager in the scene");
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
            // 1) Activate temporary invincibility on the player (always activate, regardless of store upgrade)
            Debug.Log($"Invincibility ability activated. Player is invincible for {customInvincibilityDuration} seconds.");
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.ActivateInvincibility(customInvincibilityDuration);
            }
            else
            {
                Debug.LogWarning("PlayerHealth.Instance is null. Cannot set player invincible.");
            }

            // 2) Show UI icon for the same duration as the invincibility
            if (UIManager.Instance != null && uniqueSquareType != null)
            {
                UIManager.Instance.OnAbilityActivated(uniqueSquareType, customInvincibilityDuration);
                StartCoroutine(DeactivateAbilityAfterDelay(customInvincibilityDuration));
            }

            // 3) Award points and remove the square
            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.DestroyUniqueSquare(gameObject, pointValue);
            }
            else
            {
                Debug.LogWarning("SpawnManager.Instance is null. Destroying locally.");
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Hides the invincibility icon in the UI after the specified duration.
    /// </summary>
    private IEnumerator DeactivateAbilityAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (UIManager.Instance != null && uniqueSquareType != null)
        {
            UIManager.Instance.OnAbilityDeactivated(uniqueSquareType);
        }
    }
}
