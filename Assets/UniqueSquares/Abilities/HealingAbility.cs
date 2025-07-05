using UnityEngine;
using System.Collections;

public class HealingAbility : MonoBehaviour
{
    [Header("Healing Settings")]
    public float healAmount = 20f;

    [Header("Point Settings")]
    public int pointValue = 10;
    [Header("Unique Square Ability Parameters")]
    public AudioClip DestructionSFX;
    public GameObject DestructionParticleEffect;

    [Header("Reference to UniqueSquareType (for UI icon, etc.)")]
    public UniqueSquareType uniqueSquareType;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Bullet"))
        {
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.ReportProgress("2", 1);
            }
            // Heal player
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.Heal(healAmount);
                Debug.Log($"Healing ability: restored {healAmount} health.");
            }

            // Display the icon with duration=0 => no slider
            if (UIManager.Instance != null && uniqueSquareType != null)
            {
                UIManager.Instance.ShowAbilityForTime(uniqueSquareType, 1f);
            }

            // Destroy the square
            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.DestroyUniqueSquare(gameObject, pointValue / 2);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    // If you want the icon to vanish immediately, do that here
    // Otherwise, just leave the icon up until OnAbilityDeactivated is called if you prefer.
    private IEnumerator HideIconImmediately()
    {
        yield return new WaitForSeconds(1f); // or 2f, or 0f
        if (UIManager.Instance != null && uniqueSquareType != null)
        {
            UIManager.Instance.OnAbilityDeactivated(uniqueSquareType);
        }
    }
}
