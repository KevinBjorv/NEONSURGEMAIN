using UnityEngine;
using System.Collections;

public class BombAbility : MonoBehaviour
{
    [Header("Bomb Settings")]
    public float bombRadius = 5f;
    public int pointPerSquare = 10;

    [Header("Reference to UniqueSquareType")]
    public UniqueSquareType uniqueSquareType;

    [Header("Unique Square Ability Parameters")]
    public AudioClip DestructionSFX;
    public GameObject DestructionParticleEffect;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger with bullets or enemy bullets
        if (other.CompareTag("Bullet") || other.CompareTag("enemyBullet"))
        {
            if (AchievementManager.Instance != null) {
                AchievementManager.Instance.ReportProgress("2", 1);
                Debug.Log("+1 value added to ID:2");
            }
            Debug.Log("Bomb triggered!");

            // UI with duration=0 => no slider
            if (UIManager.Instance != null && uniqueSquareType != null)
            {
                UIManager.Instance.OnAbilityActivated(uniqueSquareType, -1.5f);
                StartCoroutine(HideIconShortly());
            }

            // Bomb effect
            Collider2D[] squaresInRange = Physics2D.OverlapCircleAll(transform.position, bombRadius);
            int squaresDestroyed = 0;
            foreach (var col in squaresInRange)
            {
                if (col.CompareTag("Player") || col.CompareTag("Bullet") || col.gameObject == this.gameObject)
                    continue;

                squaresDestroyed++;
                if (SpawnManager.Instance != null)
                {
                    if (col.CompareTag("NormalSquare"))
                    {
                        SpawnManager.Instance.DespawnEntity(col.gameObject);
                    }
                    else
                    {
                        // UniqueSquare => pass 0 to avoid double points
                        SpawnManager.Instance.DestroyUniqueSquare(col.gameObject, 0);
                    }
                }
                else
                {
                    Destroy(col.gameObject);
                }
            }

            int totalPoints = squaresDestroyed * pointPerSquare;
            if (totalPoints > 0)
            {
                ScoreManager.Instance.AddPoints(totalPoints);
                Debug.Log($"Bomb destroyed {squaresDestroyed} squares. Gained {totalPoints} points.");
            }

            // Destroy self
            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.DestroyUniqueSquare(gameObject, 0);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private IEnumerator HideIconShortly()
    {
        yield return new WaitForSeconds(1f); // Or 0.5f, or 2f, etc.
        if (UIManager.Instance != null && uniqueSquareType != null)
        {
            UIManager.Instance.OnAbilityDeactivated(uniqueSquareType);
        }
    }
}
