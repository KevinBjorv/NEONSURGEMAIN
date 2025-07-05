using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 2f;
    public float damage = 10f; // Overwritten from WeaponManager
    public float bulletThickness = 0.05f;

    // NEW: Normalized duration (0-1) where no damage decrease occurs.
    [Tooltip("Portion (0-1) of the bullet's lifetime during which it deals full damage.")]
    public float noDamageDecreaseDuration = 0.2f;

    // NEW: Damage decay curve (normalized, where 0 corresponds to the start of decay and 1 to the bullet's end).
    [Tooltip("Curve used to scale the bullet's damage after the no-damage-decrease period. " +
             "The curve's input is normalized time (0-1) and output is a multiplier.")]
    public AnimationCurve damageDecayCurve = AnimationCurve.Linear(0, 1, 1, 0.1f);

    private float spawnTime;
    private int penetrationsRemaining = 0;

    // The final velocity that includes both the bullet's direction/speed and the player's inherited movement.
    private Vector2 bulletVelocity;

    // Reference to the Particle System
    private ParticleSystem bulletTrail;

    // NEW: Stores the bullet's original damage as set by the WeaponManager.
    private float originalDamage;

    // ---------------------------------------
    // NEW: Bullet Ricochet Upgrade Implementation
    [Tooltip("Percentage chance (0-100) for the bullet to ricochet on hitting an enemy.")]
    public float ricochetChance = 0f;
    [Tooltip("Maximum number of ricochet bounces allowed for this bullet.")]
    public int ricochetMaxBounces = 0;
    private int ricochetBouncesUsed = 0;
    [Tooltip("Radius within which to search for a new target when ricocheting.")]
    public float ricochetBounceRadius = 10f;
    // Flag set when a ricochet upgrade is activated (i.e. when a ricochet hit has been triggered).
    private bool ricochetActivated = false;
    // ---------------------------------------

    private void Start()
    {
        spawnTime = Time.time;
        gameObject.tag = "Bullet";

        // Get the Particle System attached to the bullet
        bulletTrail = GetComponentInChildren<ParticleSystem>();
        if (bulletTrail != null)
        {
            bulletTrail.Play();
        }

        // Store the original damage for dynamic adjustment.
        originalDamage = damage;
    }

    private void Update()
    {
        // Update the bullet's damage based on its lifetime progress.
        UpdateDamageOverLifetime();

        // Move the bullet based on its final velocity.
        transform.Translate(bulletVelocity * Time.deltaTime, Space.World);

        // If the bullet has never activated ricochet, apply lifetime.
        if (!ricochetActivated && Time.time - spawnTime >= lifeTime)
        {
            DestroyBullet();
        }
    }

    /// <summary>
    /// Gradually updates the bullet's damage based on its current lifetime progress.
    /// For the first noDamageDecreaseDuration fraction of its life, the bullet deals full damage.
    /// After that, the damage is scaled using the damageDecayCurve.
    /// </summary>
    private void UpdateDamageOverLifetime()
    {
        float normalizedTime = (Time.time - spawnTime) / lifeTime;

        // If within the no-decrease period, keep full damage.
        if (normalizedTime <= noDamageDecreaseDuration)
        {
            damage = originalDamage;
        }
        else
        {
            // Remap the normalized time to 0-1 for the decay curve.
            float t = (normalizedTime - noDamageDecreaseDuration) / (1f - noDamageDecreaseDuration);
            float multiplier = damageDecayCurve.Evaluate(t);
            damage = originalDamage * multiplier;
        }
    }

    /// <summary>
    /// Sets the bullet's final velocity based on direction, speed, and optional player velocity inheritance.
    /// </summary>
    /// <param name="direction">Unit direction for the bullet.</param>
    /// <param name="bulletSpeed">Base speed of the bullet.</param>
    /// <param name="playerVelocity">Current velocity of the player.</param>
    /// <param name="inheritFactor">Percentage factor to multiply the player's velocity.</param>
    public void SetInitialVelocity(Vector2 direction, float bulletSpeed, Vector2 playerVelocity, float inheritFactor)
    {
        Vector2 baseBulletVelocity = direction.normalized * bulletSpeed;
        Vector2 inherited = playerVelocity * inheritFactor;
        bulletVelocity = baseBulletVelocity + inherited;
    }

    /// <summary>
    /// Sets how many entities the bullet can penetrate.
    /// </summary>
    /// <param name="penetration">Number of penetrations allowed.</param>
    public void SetPenetration(int penetration)
    {
        penetrationsRemaining = penetration;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If bullet collides with a SafeZone trigger, simply despawn.
        if (other.CompareTag("SafeZone") && other.isTrigger)
        {
            DestroyBullet();
            return;
        }

        PassiveSquare square = other.GetComponent<PassiveSquare>();
        if (square != null)
        {
            // If the bullet is configured with a ricochet upgrade:
            if (ricochetMaxBounces > 0)
            {
                // If this is the first time the bullet has hit something,
                // attempt to activate the ricochet upgrade.
                if (!ricochetActivated)
                {
                    if (Random.Range(0f, 100f) < ricochetChance)
                    {
                        ricochetActivated = true;
                        ricochetBouncesUsed = 1; // Count the current hit as the first bounce
                        if (AttemptRicochet(other))
                        {
                            return;
                        }
                        else
                        {
                            // If no new target is found, destroy the bullet.
                            DestroyBullet();
                            return;
                        }
                    }
                    else
                    {
                        // Ricochet upgrade not activated; use normal hit logic.
                        if (penetrationsRemaining > 0)
                        {
                            penetrationsRemaining--;
                        }
                        else
                        {
                            DestroyBullet();
                        }
                        return;
                    }
                }
                else // Ricochet is already activated.
                {
                    if (ricochetBouncesUsed < ricochetMaxBounces)
                    {
                        ricochetBouncesUsed++;
                        if (AttemptRicochet(other))
                        {
                            return;
                        }
                        else
                        {
                            DestroyBullet();
                            return;
                        }
                    }
                    else
                    {
                        DestroyBullet();
                        return;
                    }
                }
            }
            else // Not a ricochet bullet; follow normal penetration logic.
            {
                if (penetrationsRemaining > 0)
                {
                    penetrationsRemaining--;
                }
                else
                {
                    DestroyBullet();
                }
            }
        }
    }

    /// <summary>
    /// Attempts to find a new target (PassiveSquare) within a given radius to ricochet toward.
    /// If a valid target is found, the bullet's velocity and rotation are updated accordingly.
    /// Returns true if a new target is found, false otherwise.
    /// </summary>
    /// <param name="currentHit">The collider that was just hit, to exclude it from the search.</param>
    private bool AttemptRicochet(Collider2D currentHit)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ricochetBounceRadius);
        PassiveSquare newTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == currentHit.gameObject)
                continue;
            PassiveSquare potentialTarget = hit.GetComponent<PassiveSquare>();
            if (potentialTarget != null)
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    newTarget = potentialTarget;
                }
            }
        }

        if (newTarget != null)
        {
            Vector2 newDirection = (newTarget.transform.position - transform.position).normalized;
            bulletVelocity = newDirection * speed;
            // Rotate the bullet to face the new direction.
            float angle = Mathf.Atan2(newDirection.y, newDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Destroys the bullet and handles cleanup of its particle trail.
    /// </summary>
    private void DestroyBullet()
    {
        if (bulletTrail != null)
        {
            // Detach the particle system so it doesn’t disappear immediately.
            bulletTrail.transform.parent = null;
            bulletTrail.Stop();
            Destroy(bulletTrail.gameObject, bulletTrail.main.duration); // Destroy the trail after it finishes.
        }
        Destroy(gameObject);
    }
}
