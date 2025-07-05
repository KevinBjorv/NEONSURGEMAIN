using UnityEngine;

public class GrenadeThrow : MonoBehaviour
{
    

    [Header("Grenade Ability Settings")]
    [Tooltip("Is the grenade ability activated by the storeManager")]
    public bool isEnabled;

    [Tooltip("The grenade prefab to instantiate when throwing a grenade.")]
    public GameObject grenadePrefab;

    [Tooltip("Cooldown in seconds between grenade throws.")]
    public float throwCooldown = 2f;

    [Tooltip("The explosion radius of the grenade.")]
    public float explosionRadius = 5f;

    [Tooltip("The force with which the grenade is thrown.")]
    public float throwForce = 10f;

    // Tracks when the next grenade can be thrown.
    private float nextThrowTime = 0f;

    // Cached main camera reference.
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
            // Check for right-click and cooldown expiration.
            if (isEnabled && Input.GetMouseButtonDown(1) && Time.time >= nextThrowTime)
            {
                ThrowGrenade();
                nextThrowTime = Time.time + throwCooldown;
            }
    }

    private void ThrowGrenade()
    {
        if (grenadePrefab == null)
        {
            Debug.LogWarning("Grenade prefab is not assigned.");
            return;
        }

        // Instantiate the grenade prefab at the player's position.
        GameObject grenadeInstance = Instantiate(grenadePrefab, transform.position, Quaternion.identity);

        // Apply a force to the grenade based on the mouse position.
        if (mainCamera != null)
        {
            // Get the mouse position in world coordinates.
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f; // Ensure the grenade stays in 2D.

            // Calculate the throw direction from the player's position to the mouse position.
            Vector2 throwDirection = (mouseWorldPos - transform.position).normalized;

            // Apply the throwing force using the grenade's Rigidbody2D.
            Rigidbody2D rb = grenadeInstance.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.AddForce(throwDirection * throwForce, ForceMode2D.Impulse);
            }
        }

        // If the grenade prefab contains a Grenade script, pass the explosion radius.
        Grenade grenadeScript = grenadeInstance.GetComponent<Grenade>();
        if (grenadeScript != null)
        {
            grenadeScript.explosionRadius = explosionRadius;
        }
    }
}
