using UnityEngine;
using System.Collections;

public class OrbiterEnemy : HostileEnemy
{
    // Cast base enemyType to OrbiterEnemyType for orbiter-specific parameters.
    private OrbiterEnemyType orbiterType
    {
        get { return enemyType as OrbiterEnemyType; }
    }

    // Timer for shooting projectiles.
    private float orbiterShootTimer = 0f;

    // Variable to control how snappy the orbiter adjusts its distance from the player.
    [Tooltip("Controls how quickly the orbiter adjusts its distance from the player. Higher values mean snappier movement.")]
    public float orbitSnappiness = 3f;

    // Variables for smooth distance variability.
    [Tooltip("Maximum additional distance offset (both inward and outward) the orbiter can vary from its base orbit distance.")]
    public float distanceVariability = 1f;

    [Tooltip("Controls how fast the distance variability changes over time.")]
    public float distanceVariationSpeed = 0.5f;

    // --- Damage Flash Settings ---
    [Tooltip("Duration for which the enemy is darkened when hit.")]
    public float damageFlashDuration = 0.2f;

    [Tooltip("Amount to decrease the HSV value (0 to 1) to darken the enemy's emission.")]
    public float darkenAmount = 0.2f;

    // AudioSource for orbiter-specific sounds (if needed).
    private AudioSource audioSource;

    // To store the original material color and emission color.
    private Color baseColor;
    private Color defaultEmissionColor;

    // Coroutine reference for the damage flash effect.
    private Coroutine damageFlashCoroutine;

    private void Start()
    {
        if (orbiterType == null)
        {
            Debug.LogError("OrbiterEnemy has no OrbiterEnemyType assigned or enemyType is not an OrbiterEnemyType!");
            return;
        }

        // Initialize health and get player reference.
        currentHealth = orbiterType.maxHealth;
        playerTransform = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;

        // Set sprite material if available.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr && orbiterType.enemyMaterial != null)
        {
            sr.material = Instantiate(orbiterType.enemyMaterial);
            baseColor = sr.material.color;
            defaultEmissionColor = sr.material.GetColor("_EmissionColor");
        }

        // Initialize shooting timer.
        orbiterShootTimer = 0f;

        // Set up AudioSource for orbiter sounds if needed.
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        // Instantiate the health bar if a prefab is assigned.
        if (healthBarPrefab != null)
        {
            GameObject hbObj = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity, transform);
            enemyHealthBar = hbObj.GetComponent<EnemyHealthBar>();
            if (enemyHealthBar != null)
            {
                enemyHealthBar.Initialize(currentHealth, orbiterType.maxHealth);
            }
        }
    }

    public override void OnReactivated()
    {
        currentHealth = orbiterType.maxHealth;
        orbiterShootTimer = 0f;
        if (enemyHealthBar != null)
        {
            enemyHealthBar.Initialize(currentHealth, orbiterType.maxHealth);
        }
    }

    private void Update()
    {
        if (orbiterType == null) return;
        if (playerTransform == null)
        {
            playerTransform = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (playerTransform == null) return;
        }

        // Orbit around the player.
        transform.RotateAround(playerTransform.position, Vector3.forward, orbiterType.orbitSpeed * Time.deltaTime);

        // Get the current offset and distance from the player.
        Vector2 currentOffset = transform.position - playerTransform.position;
        float currentDistance = currentOffset.magnitude;

        // Calculate a smooth variation offset using Perlin noise.
        float noiseValue = Mathf.PerlinNoise(Time.time * distanceVariationSpeed, 0f);
        float variation = (noiseValue - 0.5f) * 2f * distanceVariability;

        // Determine the target orbit distance by adding the variation offset.
        float targetOrbitDistance = orbiterType.orbitDistance + variation;

        // Smoothly interpolate from the current distance to the target orbit distance.
        float newDistance = Mathf.Lerp(currentDistance, targetOrbitDistance, orbitSnappiness * Time.deltaTime);
        Vector2 newOffset = currentOffset.normalized * newDistance;
        transform.position = playerTransform.position + (Vector3)newOffset;

        // Update the health bar position and lock its rotation so it stays horizontal.
        if (enemyHealthBar != null)
        {
            enemyHealthBar.transform.position = transform.position + healthBarOffset;
            enemyHealthBar.transform.rotation = Quaternion.identity;
        }

        // Shooting logic.
        orbiterShootTimer += Time.deltaTime;
        if (orbiterShootTimer >= orbiterType.orbiterShootInterval)
        {
            ShootAtPlayer();
            orbiterShootTimer = 0f;
        }
    }

    public override void TakeDamage(float dmg)
    {
        float previousHealth = currentHealth;
        currentHealth -= dmg;
        if (enemyHealthBar != null)
        {
            enemyHealthBar.UpdateHealth(currentHealth, previousHealth);
        }
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            OnEnemyDestroyed();
        }

        // Trigger damage flash effect: darken the enemy's emission color.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.material != null)
        {
            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
            }
            damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.material != null)
        {
            Color originalEmission = defaultEmissionColor;
            try
            {
                // Convert the original emission color to HSV, decrease the value, then convert back.
                float h, s, v;
                Color.RGBToHSV(originalEmission, out h, out s, out v);
                float newV = Mathf.Clamp01(v - darkenAmount);
                Color darkenedEmission = Color.HSVToRGB(h, s, newV);
                sr.material.SetColor("_EmissionColor", darkenedEmission);
                yield return new WaitForSecondsRealtime(damageFlashDuration);
            }
            finally
            {
                if (sr != null && sr.material != null)
                {
                    sr.material.SetColor("_EmissionColor", originalEmission);
                }
            }
        }
        damageFlashCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If hit by a bullet, take damage.
        if (other.CompareTag("Bullet"))
        {
            Bullet bullet = other.GetComponent<Bullet>();
            if (bullet != null)
            {
                TakeDamage(bullet.damage);
                Destroy(other.gameObject);
            }
        }
        // If colliding with a passive square, destroy the square without taking damage.
        else if (other.CompareTag("NormalSquare"))
        {
            Destroy(other.gameObject);
        }
    }
}
