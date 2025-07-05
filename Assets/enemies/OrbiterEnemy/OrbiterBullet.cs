using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class OrbiterBullet : MonoBehaviour
{
    // Basic bullet parameters.
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
    public bool enableDissolveOnPlayerHit = false;  // Flag to enable dissolve effect on player hit.
    public Material baseDissolveMaterial;

    // New penetration variable: number of PassiveSquares this bullet can penetrate before being destroyed.
    [Tooltip("Number of PassiveSquares this bullet can penetrate before being destroyed.")]
    public int penetration = 1;

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
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime && !isDissolving)
        {
            StartCoroutine(DissolveAndDestroy());
        }
    }

    private IEnumerator DissolveAndDestroy()
    {
        isDissolving = true;
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

        // Collision with the player.
        if (other.CompareTag("Player"))
        {
            PlayerHealth p = other.GetComponent<PlayerHealth>();
            if (p != null) p.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Collision with PassiveSquare.
        PassiveSquare square = other.GetComponent<PassiveSquare>();
        if (square != null)
        {
            // Destroy the PassiveSquare.
            SpawnManager.Instance.DespawnEntity(square.gameObject);
            // Decrement penetration count.
            penetration--;
            if (penetration <= 0)
            {
                Destroy(gameObject);
            }
            return;
        }

        // Collision with a player's bullet.
        if (other.CompareTag("Bullet"))
        {
            if (enableDissolveOnPlayerHit && !isDissolving)
            {
                StartCoroutine(DissolveAndDestroy());
            }
            else
            {
                Destroy(other.gameObject);
                Destroy(gameObject);
            }
        }
    }
}
