using UnityEngine;
using System.Collections;

public class BomberEnemy : HostileEnemy
{
    // Cast base enemyType to BomberEnemyType for access to bomber-specific settings.
    private BomberEnemyType bomberType
    {
        get { return enemyType as BomberEnemyType; }
    }

    // Audio sources for the bomber. Attach audio sources and assign their respective clips in the Inspector.
    [Tooltip("Idle moving sound (3D) that follows the bomb.")]
    public AudioSource idleAudioSource;
    [Tooltip("Warning beep sound played repeatedly in the warning zone.")]
    public AudioSource warningAudioSource;
    [Tooltip("Explosion sound effect.")]
    public AudioSource explosionAudioSource;

    // Timer for repeating warning beeps.
    private float warningTimer = 0f;

    // State variables.
    private bool isLocked = false;    // True when the bomber has locked in its position.
    private bool hasExploded = false; // True once the explosion has occurred.
    private float lockDelay = 0.5f;     // Delay after the final warning beep before explosion.

    private void Start()
    {
        if (bomberType == null)
        {
            Debug.LogError("BomberEnemy has no BomberEnemyType assigned or enemyType is not a BomberEnemyType!");
            return;
        }
        // Initialize health and get player reference.
        currentHealth = bomberType.maxHealth;
        playerTransform = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;

        // Set material if defined.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr && bomberType.enemyMaterial != null)
        {
            sr.material = Instantiate(bomberType.enemyMaterial);
        }

        // Start playing idle sound on loop.
        if (idleAudioSource != null && idleAudioSource.clip != null)
        {
            idleAudioSource.loop = true;
            idleAudioSource.Play();
        }

        isLocked = false;
        hasExploded = false;
    }

    public override void OnReactivated()
    {
        base.OnReactivated(); // Reset health and shootTimer

        if (bomberType == null)
        {
            Debug.LogError("BomberEnemy has no BomberEnemyType assigned!");
            return;
        }
        currentHealth = bomberType.maxHealth;
        isLocked = false;
        hasExploded = false;  // Reset explosion flag.
        warningTimer = 0f;
        // Restart idle sound if necessary.
        if (idleAudioSource != null && idleAudioSource.clip != null)
        {
            idleAudioSource.loop = true;
            if (!idleAudioSource.isPlaying)
                idleAudioSource.Play();
        }
    }

    private void Update()
    {
        if (bomberType == null) return;
        if (playerTransform == null)
        {
            playerTransform = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (playerTransform == null) return;
        }

        // If not locked, the bomber moves toward the player.
        if (!isLocked)
        {
            transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, bomberType.bomberMoveSpeed * Time.deltaTime);
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            // Repeated warning beep logic when within the warning zone (3× explosion radius).
            if (distance <= bomberType.explosionRadius * 3f)
            {
                warningTimer += Time.deltaTime;
                if (warningTimer >= bomberType.warningInterval)
                {
                    if (warningAudioSource != null && warningAudioSource.clip != null)
                    {
                        Debug.Log("Warning sound played (repeating)");
                        warningAudioSource.Play();
                    }
                    warningTimer = 0f;
                }
            }

            // Lock in when reaching the explosion trigger distance.
            if (distance <= bomberType.explosionTriggerDistance)
            {
                isLocked = true;
                warningTimer = 0f; // Reset timer.
                // Mute the idle sound.
                if (idleAudioSource != null && idleAudioSource.isPlaying)
                {
                    idleAudioSource.Stop();
                }
                // Start the final warning sequence then explosion.
                StartCoroutine(WarningThenExplode());
            }
        }

        // Update enemy health bar position if applicable.
        if (enemyHealthBar != null)
        {
            enemyHealthBar.transform.position = transform.position + healthBarOffset;
            enemyHealthBar.transform.rotation = Quaternion.LookRotation(Vector3.forward, Camera.main.transform.up);
        }

        if(idleAudioSource != null)
        {
            idleAudioSource.mute = (Time.timeScale == 0f);
        }

        if(Time.timeScale == 0f)
            return;
    }

    public override void TakeDamage(float dmg)
    {
        float previousHealth = currentHealth;
        currentHealth -= dmg;
        if (enemyHealthBar != null)
        {
            enemyHealthBar.UpdateHealth(currentHealth, previousHealth);
        }
        // Explode if health depletes and explosion hasn't already occurred.
        if (currentHealth <= 0f && !hasExploded)
        {
            Explode();
        }
    }

    // Coroutine to play a final warning beep, wait for it and a brief delay before exploding.
    private IEnumerator WarningThenExplode()
    {
        if (warningAudioSource != null && warningAudioSource.clip != null)
        {
            Debug.Log("Final warning beep played");
            warningAudioSource.Play();
            yield return new WaitForSeconds(warningAudioSource.clip.length);
        }
        // Wait an additional silent delay.
        yield return new WaitForSeconds(lockDelay);
        Explode();
    }

    private void Explode()
    {
        // Prevent multiple explosions.
        if (hasExploded) return;
        hasExploded = true;

        // Stop any idle or warning sounds.
        if (idleAudioSource != null && idleAudioSource.isPlaying)
        {
            idleAudioSource.Stop();
        }
        if (warningAudioSource != null && warningAudioSource.isPlaying)
        {
            warningAudioSource.Stop();
        }
        // Play explosion sound using PlayClipAtPoint to ensure it plays even if this GameObject is despawned.
        if (explosionAudioSource != null && explosionAudioSource.clip != null)
        {
            AudioSource.PlayClipAtPoint(explosionAudioSource.clip, transform.position);
        }
        // Instantiate explosion visual effect.
        if (bomberType.explosionPrefab != null)
        {
            Instantiate(bomberType.explosionPrefab, transform.position, Quaternion.identity);
        }
        // Apply AoE damage.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, bomberType.explosionRadius);
        foreach (Collider2D col in hits)
        {
            if (col.CompareTag("Player"))
            {
                PlayerHealth player = col.GetComponent<PlayerHealth>();
                if (player != null)
                {
                    player.TakeDamage(bomberType.explosionDamage);
                }
            }
            else if (col.CompareTag("Enemy") && col.gameObject != gameObject)
            {
                HostileEnemy enemy = col.GetComponent<HostileEnemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage(bomberType.explosionDamage);
                }
            }
        }
        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.RegisterEnemyKill();
        }
        ScoreManager.Instance.RewardPointsForBomberEnemy();
        if (enemyHealthBar != null)
        {
            Destroy(enemyHealthBar.gameObject);
        }
        // Despawn this enemy for pooling.
        SpawnManager.Instance.DespawnEntity(gameObject);
    }
}
