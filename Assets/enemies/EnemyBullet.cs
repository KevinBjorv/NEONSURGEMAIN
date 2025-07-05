using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyBullet : MonoBehaviour
{
    // Existing variables...
    private float speed;
    private float damage;
    private float lifetime;
    private float homingAccuracy;
    private float timer = 0f;

    private Transform playerTransform;
    private Rigidbody2D rb;
    private bool isDissolving = false;
    private Material dissolveMaterial;
    private SpriteRenderer spriteRenderer;

    public float dissolveDuration = 0.5f;
    public float fadeOutDuration = 0.2f; // Duration for audio fade-out
    public bool enableDissolveOnPlayerHit = false;  // New flag

    public Material baseDissolveMaterial;

    // --- New Penetration Parameter ---
    [Tooltip("The number of passive squares the bullet can penetrate before despawning.")]
    public int penetrationCount = 1;
    private int currentPenetration;

    // New AudioSource slot for the swoosh sound.
    public AudioSource audioSource;

    public void Initialize(float bulletSpeed, float bulletDamage, float bulletLifetime, float bulletHoming)
    {
        speed = bulletSpeed;
        damage = bulletDamage;
        lifetime = bulletLifetime;
        homingAccuracy = bulletHoming;
    }

    private void Start()
    {
        gameObject.tag = "enemyBullet";

        if (PlayerHealth.Instance != null)
            playerTransform = PlayerHealth.Instance.transform;

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.gravityScale = 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && baseDissolveMaterial != null)
        {
            dissolveMaterial = new Material(baseDissolveMaterial);
            spriteRenderer.material = dissolveMaterial;
        }

        // Initialize the penetration counter.
        currentPenetration = penetrationCount;

        // Play the swoosh sound.
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime && !isDissolving)
        {
            StartCoroutine(DissolveAndDestroy());
        }
    }

    // This coroutine handles the visual dissolve while triggering the audio fade-out on a temporary object.
    private IEnumerator DissolveAndDestroy()
    {
        isDissolving = true;

        // Detach audio fade-out so it doesn't delay bullet destruction.
        if (audioSource != null && audioSource.isPlaying)
        {
            DetachAndFadeOutAudio();
        }

        // Visual dissolve effect (optional).
        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float dissolveAmount = Mathf.Clamp01(elapsed / dissolveDuration);
            if (dissolveMaterial != null)
            {
                dissolveMaterial.SetFloat("_DissolveAmount", dissolveAmount);
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // Detaches the audio by creating a temporary AudioSource for fade-out.
    private void DetachAndFadeOutAudio()
    {
        // Create a temporary GameObject to hold the audio.
        GameObject tempAudio = new GameObject("TempAudio");
        AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
        // Copy settings from the original AudioSource.
        tempSource.clip = audioSource.clip;
        tempSource.volume = audioSource.volume;
        tempSource.pitch = audioSource.pitch;
        tempSource.loop = false;
        tempSource.Play();

        // Start fading out the temporary audio.
        StartCoroutine(FadeOutAndDestroyTempAudio(tempSource, fadeOutDuration));
    }

    // Fades out the temporary AudioSource and then destroys it.
    private IEnumerator FadeOutAndDestroyTempAudio(AudioSource tempSource, float duration)
    {
        float startVolume = tempSource.volume;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            tempSource.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
            yield return null;
        }
        tempSource.Stop();
        Destroy(tempSource.gameObject);
    }

    private void FixedUpdate()
    {
        if (rb == null || playerTransform == null) return;

        Vector2 currentVelocity = rb.velocity;
        Vector2 dirToPlayer = (playerTransform.position - transform.position).normalized;
        Vector2 newDir = Vector2.Lerp(
            currentVelocity.normalized,
            dirToPlayer,
            homingAccuracy * Time.fixedDeltaTime * 5f
        ).normalized;

        rb.velocity = newDir * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Collision with SafeZone: if the other collider is tagged "SafeZone" and is a trigger, dissolve and despawn.
        if (other.CompareTag("SafeZone") && other.isTrigger)
        {
            if (!isDissolving)
            {
                StartCoroutine(DissolveAndDestroy());
            }
            return;
        }

        // Collision with player.
        if (other.CompareTag("Player"))
        {
            PlayerHealth p = other.GetComponent<PlayerHealth>();
            if (p != null) p.TakeDamage(damage);
            if (!isDissolving)
            {
                StartCoroutine(DissolveAndDestroy());
            }
            return;
        }

        // Collision with PassiveSquare.
        PassiveSquare square = other.GetComponent<PassiveSquare>();
        if (square != null)
        {
            // Destroy the passive square.
            SpawnManager.Instance.DespawnEntity(square.gameObject);
            // Reduce penetration count.
            currentPenetration--;
            // If we've exhausted our penetration ability, start dissolve.
            if (currentPenetration <= 0 && !isDissolving)
            {
                StartCoroutine(DissolveAndDestroy());
            }
            return;
        }

        // Collision with a player's bullet.
        if (other.CompareTag("Bullet"))
        {
            // If dissolve on player hit is enabled, start dissolve effect.
            if (enableDissolveOnPlayerHit && !isDissolving)
            {
                StartCoroutine(DissolveAndDestroy());
            }
            else
            {
                if (!isDissolving)
                {
                    StartCoroutine(DissolveAndDestroy());
                }
                Destroy(other.gameObject);
            }
        }
    }
}

