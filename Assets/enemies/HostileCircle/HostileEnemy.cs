using UnityEngine;
using UnityEngine.UI; // Keep this if healthBarPrefab related UI components are accessed, though not directly in this script

[RequireComponent(typeof(Collider2D))]
public class HostileEnemy : MonoBehaviour
{
    public HostileEnemyType enemyType;
    [HideInInspector]
    public bool isPooledInitialized = false; // Flag to mark if position has been set by pooler

    protected float currentHealth;
    private float shootTimer = 0f;
    private SpriteRenderer spriteRenderer;
    protected Transform playerTransform;

    [Tooltip("Assign a prefab with a world-space Canvas and EnemyHealthBar component.")]
    public GameObject healthBarPrefab;
    public EnemyHealthBar enemyHealthBar;
    [Tooltip("Local offset above the enemy where the health bar should appear.")]
    public Vector3 healthBarOffset = new Vector3(0, 1f, 0);

    [Tooltip("Margin for health bar visibility (0 = exactly on screen, 0.1 = 10% buffer around screen edges).")]
    public float healthBarVisibilityMargin = 0.05f; // Added for screen visibility buffer

    private Camera mainCamera; // Cached main camera

    private void Start()
    {
        mainCamera = Camera.main; // Cache the main camera

        if (enemyType == null)
        {
            Debug.LogError($"HostileEnemy on '{name}' has no HostileEnemyType assigned!");
            // It's good practice to disable the component or GameObject if a critical dependency is missing
            // For example: enabled = false; or gameObject.SetActive(false);
            return;
        }

        currentHealth = enemyType.maxHealth;
        if (PlayerHealth.Instance != null)
        {
            playerTransform = PlayerHealth.Instance.transform;
        }
        else
        {
            Debug.LogWarning($"PlayerHealth.Instance not found for HostileEnemy '{name}'. Player tracking features will be limited.");
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer && enemyType.enemyMaterial != null)
        {
            // Instantiate material to avoid shared material modification issues if any shader properties are changed at runtime per instance
            spriteRenderer.material = Instantiate(enemyType.enemyMaterial);
        }

        if (!CompareTag("Bomber") && healthBarPrefab != null)
        {
            GameObject hbObj = Instantiate(healthBarPrefab, transform.position + healthBarOffset, Quaternion.identity, transform);
            enemyHealthBar = hbObj.GetComponent<EnemyHealthBar>();
            if (enemyHealthBar != null)
            {
                enemyHealthBar.Initialize(currentHealth, enemyType.maxHealth);
                enemyHealthBar.gameObject.SetActive(false); // Initially hide the health bar
            }
            else
            {
                Debug.LogError($"Health bar prefab on '{name}' is missing the EnemyHealthBar component.");
            }
        }
    }

    private void Update()
    {
        if (enemyType == null || mainCamera == null) return; // Do nothing if critical components are missing

        // Shooting logic
        shootTimer += Time.deltaTime;
        if (shootTimer >= enemyType.shootInterval)
        {
            if (playerTransform != null)
            {
                float distance = Vector2.Distance(transform.position, playerTransform.position);
                if (distance <= enemyType.shootRange)
                {
                    ShootAtPlayer();
                }
            }
            shootTimer = 0f;
        }

        // Health bar visibility and positioning
        if (enemyHealthBar != null)
        {
            // Determine if the enemy (and thus its health bar) should be visible
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
            bool shouldBeVisible = viewportPos.z > 0 && // Check if in front of the camera's near clip plane
                                   viewportPos.x >= (0 - healthBarVisibilityMargin) && viewportPos.x <= (1 + healthBarVisibilityMargin) &&
                                   viewportPos.y >= (0 - healthBarVisibilityMargin) && viewportPos.y <= (1 + healthBarVisibilityMargin);

            if (shouldBeVisible)
            {
                if (!enemyHealthBar.gameObject.activeSelf)
                {
                    enemyHealthBar.gameObject.SetActive(true);
                }
                // Update position and rotation only when active and visible
                enemyHealthBar.transform.position = transform.position + healthBarOffset;
                // Billboard effect: Make health bar face the camera
                enemyHealthBar.transform.LookAt(enemyHealthBar.transform.position + mainCamera.transform.rotation * Vector3.forward,
                                                mainCamera.transform.rotation * Vector3.up);
            }
            else
            {
                if (enemyHealthBar.gameObject.activeSelf)
                {
                    enemyHealthBar.gameObject.SetActive(false);
                }
            }
        }
    }

    protected virtual void ShootAtPlayer()
    {
        if (enemyType.bulletPrefab == null) return;
        // Consider using an object pool for bullets as well for performance
        GameObject bulletObj = Instantiate(enemyType.bulletPrefab, transform.position, Quaternion.identity);
        EnemyBullet eBullet = bulletObj.GetComponent<EnemyBullet>();
        if (eBullet != null)
        {
            eBullet.Initialize(
                enemyType.bulletSpeed,
                enemyType.bulletDamage,
                enemyType.bulletLifetime,
                enemyType.homingAccuracy
            );
        }
    }

    public virtual void TakeDamage(float dmg)
    {
        if (currentHealth <= 0) return; // Already destroyed

        float previousHealth = currentHealth;
        currentHealth -= dmg;
        currentHealth = Mathf.Max(currentHealth, 0f); // Ensure health doesn't go below zero

        if (enemyHealthBar != null)
        {
            enemyHealthBar.UpdateHealth(currentHealth, previousHealth);
        }

        if (currentHealth <= 0f)
        {
            OnEnemyDestroyed();
        }
    }

    protected virtual void OnEnemyDestroyed()
    {
        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.RegisterEnemyKill();
        }
        Debug.Log($"{enemyType.enemyName} destroyed!");

        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.enemiesKilled++;
        }

        if (enemyType.grantsAbilityOnDeath && enemyType.rewardAbility != null)
        {
            // Activate unique ability if needed
            // Example: AbilityManager.Instance.GrantAbility(enemyType.rewardAbility);
        }

        if (ScoreManager.Instance != null) // Good to check for null instances
        {
            ScoreManager.Instance.RewardPointsForHostileCircle();
        }

        // Instead of destroying, if health bars were pooled, you'd despawn it.
        // For now, destroying is fine if they are not part of a separate pool.
        if (enemyHealthBar != null)
        {
            Destroy(enemyHealthBar.gameObject);
            enemyHealthBar = null; // Clear reference
        }

        isPooledInitialized = false; // Reset for object pooling
        if (SpawnManager.Instance != null) // Good to check for null instances
        {
            SpawnManager.Instance.DespawnEntity(gameObject);
        }
        else
        {
            // Fallback if not using a SpawnManager or if it's not found
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Bullet")) // Assuming player bullets are tagged "Bullet"
        {
            Bullet b = other.GetComponent<Bullet>();
            if (b != null)
            {
                TakeDamage(b.damage);
                Destroy(other.gameObject); // Destroy player bullet on impact
            }
        }
    }

    /// <summary>
    /// Reset the enemy stats when reactivated from pool.
    /// </summary>
    public virtual void OnReactivated()
    {
        currentHealth = enemyType.maxHealth;
        shootTimer = 0f; // Reset shoot timer
        // isPooledInitialized should be handled by the spawner logic setting its position.

        // Ensure sprite renderer material is reset if it was modified (e.g. hit flash)
        // (This example doesn't modify it beyond initial assignment, but good to keep in mind)
        if (spriteRenderer && enemyType.enemyMaterial != null)
        {
            // If you were changing material properties, you might need to re-assign or reset them.
            // For now, a new instance is created in Start, if pooling reuses this component,
            // this material instance persists. If materials were changed, they'd need reset.
        }

        // Ensure we have a healthBar if it was destroyed or not created (e.g., for a Bomber that later becomes non-Bomber?)
        if (!CompareTag("Bomber"))
        {
            if (enemyHealthBar == null && healthBarPrefab != null) // If it got destroyed or was never made
            {
                GameObject hbObj = Instantiate(
                    healthBarPrefab,
                    transform.position + healthBarOffset,
                    Quaternion.identity,
                    transform);
                enemyHealthBar = hbObj.GetComponent<EnemyHealthBar>();
            }

            // Initialize (whether new or existing but reactivated)
            if (enemyHealthBar != null)
            {
                enemyHealthBar.Initialize(currentHealth, enemyType.maxHealth);
                enemyHealthBar.gameObject.SetActive(false); // Start hidden, Update loop will manage visibility
            }
        }
        else // If it IS a Bomber, ensure no health bar is active
        {
            if (enemyHealthBar != null)
            {
                Destroy(enemyHealthBar.gameObject); // Or pool it if health bars are pooled
                enemyHealthBar = null;
            }
        }
        // The object itself should be set active by the pooling system.
        // Visibility of health bar is handled in Update.
    }
}