using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour
{
    [Header("Grenade Settings")]
    [Tooltip("Time in seconds before the grenade explodes (if not triggered earlier).")]
    public float fuseTime = 3f;

    [Tooltip("The explosion radius of the grenade.")]
    public float explosionRadius = 5f;

    [Tooltip("Points awarded per square destroyed by the explosion.")]
    public int pointPerSquare = 10;

    [Tooltip("Reference AudioSource with explosion SFX and 3D settings configured in the inspector.")]
    public AudioSource explosionAudioSource;

    [Tooltip("Optional: Particle effect prefab to spawn when the grenade explodes.")]
    public GameObject explosionEffectPrefab;

    // Flag to ensure the explosion only occurs once.
    private bool hasExploded = false;

    private void Start()
    {
        // Start the fuse timer.
        Invoke(nameof(Explode), fuseTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger explosion if the grenade collides with a bullet or enemy bullet.
        if (!hasExploded && (other.CompareTag("Bullet") || other.CompareTag("enemyBullet")))
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (hasExploded)
            return;

        hasExploded = true;
        Debug.Log("Grenade exploded!");

        // Spawn explosion effect if assigned.
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        // Play explosion sound preserving 3D settings.
        if (explosionAudioSource != null && explosionAudioSource.clip != null)
        {
            PlayClipAtPointPreserveSettings(explosionAudioSource, transform.position);
        }

        // Perform the explosion: find nearby objects.
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        int squaresDestroyed = 0;
        foreach (Collider2D col in colliders)
        {
            // Skip certain objects.
            if (col.CompareTag("Player") || col.CompareTag("Bullet") || col.gameObject == gameObject)
                continue;

            squaresDestroyed++;

            // Use SpawnManager if available, otherwise just destroy.
            if (SpawnManager.Instance != null)
            {
                if (col.CompareTag("NormalSquare"))
                {
                    SpawnManager.Instance.DespawnEntity(col.gameObject);
                }
                else
                {
                    // For unique squares, pass 0 to avoid double points.
                    SpawnManager.Instance.DestroyUniqueSquare(col.gameObject, 0);
                }
            }
            else
            {
                Destroy(col.gameObject);
            }
        }

        int totalPoints = squaresDestroyed * pointPerSquare;
        if (totalPoints > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddPoints(totalPoints);
            Debug.Log($"Grenade destroyed {squaresDestroyed} squares. Gained {totalPoints} points.");
        }

        // Destroy the grenade (using SpawnManager if available).
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.DestroyUniqueSquare(gameObject, 0);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Mimics AudioSource.PlayClipAtPoint but preserves the additional 3D AudioSource settings 
    /// from the provided sourceTemplate.
    /// </summary>
    /// <param name="sourceTemplate">The reference AudioSource with desired settings.</param>
    /// <param name="position">The position where the sound should play.</param>
    /// <returns>The temporary AudioSource that was created to play the clip.</returns>
    private AudioSource PlayClipAtPointPreserveSettings(AudioSource sourceTemplate, Vector3 position)
    {
        // Create a temporary game object at the given position.
        GameObject tempGO = new GameObject("TempAudio");
        tempGO.transform.position = position;
        AudioSource aSource = tempGO.AddComponent<AudioSource>();

        // Copy settings from the reference AudioSource.
        aSource.clip = sourceTemplate.clip;
        aSource.volume = sourceTemplate.volume;
        aSource.pitch = sourceTemplate.pitch;
        aSource.spatialBlend = sourceTemplate.spatialBlend;
        aSource.rolloffMode = sourceTemplate.rolloffMode;
        aSource.minDistance = sourceTemplate.minDistance;
        aSource.maxDistance = sourceTemplate.maxDistance;
        aSource.dopplerLevel = sourceTemplate.dopplerLevel;
        aSource.spread = sourceTemplate.spread;
        aSource.outputAudioMixerGroup = sourceTemplate.outputAudioMixerGroup;

        aSource.Play();
        Destroy(tempGO, aSource.clip.length);
        return aSource;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the explosion radius in the scene view.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
