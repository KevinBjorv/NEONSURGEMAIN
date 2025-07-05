using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro; // <-- Add this namespace for TextMeshPro

/// <summary>
/// Attach this to the SafeZone prefab (hexagon sprite + PolygonCollider2D isTrigger).
/// The prefab should also have a world-space Canvas with:
///    1) A Slider for lifetime
///    2) A separate Slider for "Hold R" progress
///    3) A TextMeshProUGUI component for the platform-specific instruction text.
/// </summary>
public class SafeZoneBehavior : MonoBehaviour
{
    [Header("Safe Zone Lifetime")]
    [Tooltip("How long (in seconds) this safe zone remains active.")]
    public float zoneLifetime = 30f;
    public Slider lifetimeSlider; // in-world slider for lifetime

    [Header("Hold Input Settings")] // Renamed section slightly
    [Tooltip("How long the player must hold the input to trigger the save & summary screen.")]
    public float holdInputDuration = 2f; // Renamed variable for clarity
    public Slider holdInputSlider;  // Renamed variable for clarity
    [Tooltip("Assign the TextMeshProUGUI component here.")]
    public TextMeshProUGUI holdInputText; // Changed type to TextMeshProUGUI

    // Default texts for different platforms
    private const string mobileHoldText = "Press and Hold Screen to Save";
    private const string pcHoldText = "Press and hold R to Save";

    [Header("Safe Zone Radius")]
    [Tooltip("Approx radius used to exclude spawns inside it.")]
    public float safeZoneRadius = 3f;

    private float timer;
    private bool playerInside;
    private float holdInputTime; // Renamed variable

    [Header("Summary Screen Manager Reference")]
    public SummaryScreenManager summaryScreenManager;
    // Assign this in the Inspector (drag the SummaryScreenManager from your UI)

    public Transform safeZoneTransform;
    public PhoneUIManager phoneUIManager;

    private void Start()
    {
        // Find UIManager if not already assigned
        if (phoneUIManager == null)
        {
            phoneUIManager = FindObjectOfType<PhoneUIManager>();
            // Optional: Log warning if not found, but allow default PC behaviour
            // if (phoneUIManager == null) { Debug.LogWarning("[SafeZone] PhoneUIManager not found. Defaulting to PC input/text."); }
        }

        // Find Summary Manager if not already assigned
        if (summaryScreenManager == null)
        {
            summaryScreenManager = FindObjectOfType<SummaryScreenManager>();
            if (summaryScreenManager == null)
            {
                Debug.LogWarning("[SafeZone] No SummaryScreenManager found in the scene.");
            }
        }

        // Ensure there is an EventSystem in the scene for mobile UI checks
        if (EventSystem.current == null)
        {
            Debug.LogError("[SafeZone] No EventSystem found in the scene. Mobile UI interaction checks will not work!");
        }
    }

    private void OnEnable()
    {
        // Reset everything when zone is activated
        timer = zoneLifetime;
        holdInputTime = 0f;
        playerInside = false;

        if (lifetimeSlider != null)
        {
            lifetimeSlider.maxValue = zoneLifetime;
            lifetimeSlider.value = zoneLifetime;
        }
        if (holdInputSlider != null)
        {
            holdInputSlider.maxValue = holdInputDuration; // Use renamed variable
            holdInputSlider.value = 0f;
            holdInputSlider.gameObject.SetActive(false);
        }

        // --- Set the correct instruction text based on platform ---
        if (holdInputText != null)
        {
            if (phoneUIManager != null && phoneUIManager.isMobile)
            {
                holdInputText.text = mobileHoldText; // Set mobile text
            }
            else
            {
                holdInputText.text = pcHoldText; // Set PC text (or default if manager is missing)
            }
            holdInputText.gameObject.SetActive(false); // Hide the text initially
        }
        else
        {
            Debug.LogWarning("[SafeZone] Hold Input Text (TextMeshProUGUI) is not assigned in the Inspector!", this.gameObject);
        }
        // --- End of text setting ---

        Vector3 pos = safeZoneTransform.position;
        pos.z = 2;
        safeZoneTransform.position = pos;
    }

    private void Update()
    {
        // Count down lifetime
        timer -= Time.deltaTime;
        if (lifetimeSlider != null)
            lifetimeSlider.value = timer;

        // If out of time, despawn
        if (timer <= 0f)
        {
            DespawnSelf();
            return;
        }

        // If player is inside, check for hold input
        if (playerInside)
        {
            bool saveInputHeld = false; // Flag to simplify logic

            // --- Platform-Specific Input Detection ---
            if (phoneUIManager != null && phoneUIManager.isMobile)
            {
                // Mobile behavior: Check for non-UI touch
                saveInputHeld = false;
                if (Input.touchCount > 0 && EventSystem.current != null)
                {
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        Touch touch = Input.GetTouch(i);
                        if ((touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) &&
                            !EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        {
                            saveInputHeld = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Non-Mobile behavior (or if manager is missing): Check 'R' key
                if (Input.GetKey(KeyCode.R))
                {
                    saveInputHeld = true;
                }
            }

            // --- Process Save Timer Based on Input ---
            if (saveInputHeld)
            {
                holdInputTime += Time.deltaTime;
                if (holdInputSlider != null)
                    holdInputSlider.value = holdInputTime;

                if (holdInputTime >= holdInputDuration) // Use renamed variable
                {
                    SaveAndExit();
                    return;
                }
            }
            else
            {
                // Input not held correctly, reset timer
                holdInputTime = 0f;
                if (holdInputSlider != null)
                    holdInputSlider.value = 0f;
            }
        }
        else // Ensure timer resets if player leaves while holding
        {
            if (holdInputTime > 0f)
            {
                holdInputTime = 0f;
                if (holdInputSlider != null)
                    holdInputSlider.value = 0f;
            }
        }
    }

    /// <summary>
    /// Called by SpawnManager after retrieving from pool.
    /// Resets internal states, sets zone lifetime, etc.
    /// </summary>
    public void ActivateSafeZone()
    {
        OnEnable(); // OnEnable already handles all initialization
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Despawn squares entering the zone
        if (other.CompareTag("NormalSquare") || other.CompareTag("UniqueSquare"))
        {
            SpawnManager.Instance?.DespawnEntity(other.gameObject);
            if (SpawnManager.Instance == null) Destroy(other.gameObject); // Fallback
            return;
        }

        // Handle Player entering the zone
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.shieldBurstActive = true;
            if (WeaponManager.Instance != null) WeaponManager.Instance.enabled = false;

            // Show "Hold Input" UI elements
            if (holdInputSlider != null) holdInputSlider.gameObject.SetActive(true);
            if (holdInputText != null) holdInputText.gameObject.SetActive(true); // Show the text object
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Continuously despawn squares inside the zone
        if (other.CompareTag("NormalSquare") || other.CompareTag("UniqueSquare"))
        {
            SpawnManager.Instance?.DespawnEntity(other.gameObject);
            if (SpawnManager.Instance == null) Destroy(other.gameObject); // Fallback
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            holdInputTime = 0f; // Reset timer

            // Hide "Hold Input" UI elements
            if (holdInputSlider != null)
            {
                holdInputSlider.value = 0f;
                holdInputSlider.gameObject.SetActive(false);
            }
            if (holdInputText != null)
            {
                holdInputText.gameObject.SetActive(false); // Hide the text object
            }

            // Restore player state
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.shieldBurstActive = false;
            if (WeaponManager.Instance != null) WeaponManager.Instance.enabled = true;
        }
    }

    private void SaveAndExit()
    {
        Debug.Log("[SafeZone] Player held input long enough. Saving game & opening summary...");
        SaveDataManager.Instance?.CollectAllData();
        summaryScreenManager?.ShowSummary();
        if (summaryScreenManager == null) Debug.LogWarning("[SafeZone] No SummaryScreenManager assigned!");
        DespawnSelf();
    }

    public void AssignSummaryScreenManager(SummaryScreenManager manager)
    {
        summaryScreenManager = manager;
    }

    private void DespawnSelf()
    {
        // If player is still physically inside when despawning, revert immunity & shooting
        Collider2D playerCollider = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Collider2D>();
        bool stillPhysicallyInside = playerInside && playerCollider != null && GetComponent<Collider2D>().IsTouching(playerCollider);

        if (stillPhysicallyInside)
        {
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.shieldBurstActive = false;
            if (WeaponManager.Instance != null) WeaponManager.Instance.enabled = true;
        }
        playerInside = false; // Ensure flag is reset

        // Return to pool or destroy
        SpawnManager.Instance?.DespawnEntity(gameObject);
        if (SpawnManager.Instance == null) Destroy(gameObject); // Fallback
    }
}