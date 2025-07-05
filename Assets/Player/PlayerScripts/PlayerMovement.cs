using UnityEngine;
using UnityEngine.UI; // Required for Button references (Health/Shield)
using System.Collections;
using TMPro; // Required if using TextMeshPro for the ability text
using UnityEngine.EventSystems; // Required for IsPointerOverGameObject

/// <summary>
/// Handles player movement, rotation, dashing, and ability activation.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    [Header("Movement Settings")]
    [Range(1, 25)] public float walkSpeed = 5f;
    [Range(1, 25)] public float runSpeed = 8f;
    public float rotationSpeed = 700f;

    [Header("Joystick Run Settings")]
    [Tooltip("How far you have to push the joystick (0..1) before it counts as a run.")]
    [Range(0.8f, 1f)] public float joystickRunThreshold = 0.99f;
    [Tooltip("How far the joystick must be released (0..1) after stamina depletion before running can be re-enabled.")]
    [Range(0.1f, 0.5f)] public float joystickReleaseThreshold = 0.2f;
    [Tooltip("The amount the joystick has to be moved in order to start player movement")]
    [Range(0.0f, 0.2f)]
    public float deadZoneTreshold = 0.1f;

    public bool enableRun = true; // Initialized to true, stamina will gate it
    public bool isRunning { get; private set; }

    [Header("Acceleration Settings")]
    [Tooltip("Time (in seconds) to accelerate from 0 to full speed.")]
    public float accelerationTime = 0.2f;
    [Tooltip("Time (in seconds) to decelerate from full speed to 0.")]
    public float decelerationTime = 0.2f;

    [Header("Dashing Settings")]
    public bool enableDash = true; // Ensure dash is enabled to test
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1.0f;
    public bool isDashing = false;
    private bool canDash = true;
    private float dashTimeLeft;
    private Vector2 dashDir;

    [Header("References")]
    // --- Modified Joystick References ---
    [SerializeField] public VirtualJoystick joystick; // Assign LEFT/Movement joystick here
    [SerializeField] public VirtualJoystick aimingJoystick; // Assign RIGHT/Aiming joystick here
    // ---
    public WeaponManager weaponManager; // Assign Weapon Manager

    [Header("Mobile Ability Buttons")]
    [Tooltip("Assign the UI Button for Health Surge")]
    public Button healthSurgeButton;
    [Tooltip("Assign the UI Button for Shield Burst")]
    public Button shieldBurstButton;
    // Note: No mobileDashButton variable needed anymore

    private Camera mainCamera;
    private bool isMoving = false;
    private bool useJoystick; // Determined in Awake
    public bool UseJoystick => useJoystick;

    private float currentSpeed = 0f; // Current smoothed speed

    public Vector2 CurrentVelocity { get; private set; } // Player's current velocity

    [Header("Health Surge Ability")]
    public bool enableHealthSurge = false;
    public AbilityHotkey healthSurgeKey = AbilityHotkey.H;
    public float healthSurgeCooldown = 10f;
    public float healthSurgeHealPercent = 20f;
    public AudioSource healthSurgeSfx;
    private float nextHealthSurgeTime = 0f;

    [Header("Health Surge Color Flash")]
    public Color healthSurgeEffectColor = Color.green;
    public float healthSurgeEffectDuration = 0.5f;

    [Header("Shield Burst Effect")]
    public bool enableShieldBurst = false;
    public AbilityHotkey shieldBurstKey = AbilityHotkey.G;
    public float shieldBurstDuration = 3f;
    public float shieldBurstCooldown = 10f;
    public AudioSource shieldBurstSfx;
    public Color shieldBurstColor = Color.blue;
    private float nextShieldBurstTime = 0f;

    [Header("Renderer for Color Effects")]
    public Renderer playerRenderer;
    private Color originalEmissionColor;
    private Coroutine healthSurgeColorCoroutine;
    private Coroutine shieldBurstColorCoroutine;

    private const string specialAbilityAchvID = "10";

    [Header("Squash & Stretch Settings")]
    [Range(1f, 2f)] public float maxStretch = 1.25f;
    [Range(0.5f, 1f)] public float maxSquash = 0.8f;
    public float stretchLerpSpeed = 8f;
    public float returnLerpSpeed = 6f;
    private Vector3 originalScale;

    [Header("Squash Node")]
    public Transform bodyPivot;

    // --- Private State Variables ---
    private PhoneUIManager phoneUIManager;
    private Vector2 lastAimDirection = Vector2.right;

    // --- Double Tap Dash Detection Variables ---
    private int tapCount = 0;
    private float doubleTapTimer = 0f;
    // Adjust time window (in seconds) for detecting a double tap
    private const float DOUBLE_TAP_TIME_WINDOW = 0.3f;
    // ---

    /// <summary>
    /// Gets the pointerId (fingerId) currently controlling the movement joystick.
    /// Returns -1 if not on mobile or joystick isn't assigned/active.
    /// </summary>
    public int MovementJoystickPointerId
    {
        get
        {
            if (useJoystick && joystick != null)
            {
                // Ensure your VirtualJoystick script has ControllingPointerId property
                return joystick.ControllingPointerId;
            }
            return -1;
        }
    }

    private void Awake()
    {
        // Singleton Pattern
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        mainCamera = Camera.main;

        // Find PhoneUIManager
        phoneUIManager = FindObjectOfType<PhoneUIManager>();
        if (phoneUIManager == null) { Debug.LogWarning("[PlayerMovement] PhoneUIManager not found.", this.gameObject); }

        // Determine Input Method
        if (phoneUIManager != null) { useJoystick = phoneUIManager.isMobile; }
        else
        {
#if UNITY_IOS || UNITY_ANDROID
            useJoystick = true;
#else
                useJoystick = false;
#endif
        }

        // Get Original Scale
        if (bodyPivot != null) { originalScale = bodyPivot.localScale; }
        else { Debug.LogError("[PlayerMovement] Body Pivot not assigned!", this.gameObject); originalScale = transform.localScale; }

        enableRun = true; // Ensure running is allowed initially
    }

    private void Start()
    {
        // Store Original Emission Color
        if (playerRenderer != null)
        {
            if (playerRenderer.material != null && playerRenderer.material.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = playerRenderer.material.GetColor("_EmissionColor");
            }
            else { /* Log Warnings */ }
        }
        else { /* Log Warning */ }

        // Re-evaluate input method after PhoneUIManager's Awake has run
        if (phoneUIManager != null)
        {
            useJoystick = phoneUIManager.isMobile;
        }
        else
        {
            useJoystick = Application.isMobilePlatform;
        }

        // Setup Mobile Ability Buttons (Health/Shield ONLY)
        bool isMobile = useJoystick;
        SetupAbilityButton(healthSurgeButton, ActivateHealthSurge, isMobile, "Health Surge");
        SetupAbilityButton(shieldBurstButton, ActivateShieldBurst, isMobile, "Shield Burst");
        // Dash button setup removed
    }

    // Helper method for button setup
    private void SetupAbilityButton(Button button, UnityEngine.Events.UnityAction action, bool isMobile, string abilityName)
    {
        if (button != null)
        {
            if (isMobile)
            {
                button.onClick.AddListener(action);
                button.gameObject.SetActive(false);
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }
        else if (isMobile) { Debug.LogWarning($"[PlayerMovement] {abilityName} Button not assigned.", this.gameObject); }
    }

    // Remove listeners on destroy
    private void OnDestroy()
    {
        if (healthSurgeButton != null) { healthSurgeButton.onClick.RemoveListener(ActivateHealthSurge); }
        if (shieldBurstButton != null) { shieldBurstButton.onClick.RemoveListener(ActivateShieldBurst); }
        // Dash button removal removed
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.gameStarted) return;

        // Core Handlers
        HandleRotation();
        HandleMovement();
        HandleRunState();
        HandleHealthSurge(); // Keyboard check inside
        HandleShieldBurst(); // Keyboard check inside

        // --- NEW: Handle Mobile Double Tap Dash ---
        if (useJoystick) // Only run tap detection on mobile/joystick setup
        {
            HandleMobileDoubleTapDash();
        }
        // ---

        // Dash Execution Logic
        if (isDashing)
        {
            float delta = Time.deltaTime;
            transform.Translate(dashDir * dashSpeed * delta, Space.World);
            dashTimeLeft -= delta;
            if (dashTimeLeft <= 0f)
            {
                isDashing = false;
                StartCoroutine(DashCooldownCoroutine());
            }
        }
        // PC Dash Activation
        else if (!useJoystick && enableDash && canDash && Input.GetKeyDown(KeyCode.Space) && PlayerStamina.Instance != null && PlayerStamina.Instance.CanDash())
        {
            PlayerStamina.Instance.Dash();
            BeginDash((GetMouseWorldPosition() - transform.position).normalized);
        }

        HandleSquashStretch();

        // Update Mobile Button Visibility (Health/Shield only)
        bool isMobile = (phoneUIManager != null && phoneUIManager.isMobile);
        if (isMobile)
        {
            if (healthSurgeButton != null)
            {
                bool healthSurgeReady = enableHealthSurge && Time.time >= nextHealthSurgeTime;
                if (healthSurgeButton.gameObject.activeSelf != healthSurgeReady) { healthSurgeButton.gameObject.SetActive(healthSurgeReady); }
            }
            if (shieldBurstButton != null)
            {
                bool shieldBurstReady = enableShieldBurst && Time.time >= nextShieldBurstTime;
                if (shieldBurstButton.gameObject.activeSelf != shieldBurstReady) { shieldBurstButton.gameObject.SetActive(shieldBurstReady); }
            }
            // Dash button visibility removed
        }
    }

    private IEnumerator DashCooldownCoroutine()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mp = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mp.z = transform.position.z;
        return mp;
    }

    // --- Rotation, Movement, RunState, SquashStretch Handlers (Modified for new joystick names) ---
    // (HandleRotation and HandleMovement methods updated previously - ensure they use
    // 'joystick' for movement and 'aimingJoystick' for aiming correctly)
    private void HandleRotation()
    {
        Vector2 aimDirection = lastAimDirection;
        const float AIM_DEADZONE_THRESHOLD = 0.2f;

        if (!useJoystick) // PC
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            Vector2 directionToMouse = (mouseWorldPos - transform.position);
            if (directionToMouse.sqrMagnitude > 0.01f) { aimDirection = directionToMouse.normalized; }
        }
        else // Mobile
        {
            if (aimingJoystick != null)
            {
                float aimX = aimingJoystick.Horizontal;
                float aimY = aimingJoystick.Vertical;
                Vector2 aimInput = new Vector2(aimX, aimY);
                if (aimInput.magnitude > AIM_DEADZONE_THRESHOLD) { aimDirection = aimInput.normalized; }
            }
            else { Debug.LogError("[PlayerMovement] Aiming Joystick not assigned!", this.gameObject); }
        }
        lastAimDirection = aimDirection;
        float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.unscaledDeltaTime);
    }

    private void HandleMovement()
    {
        Vector3 moveDirectionInput = Vector3.zero;
        float joystickMagnitude = 0f;

        if (!useJoystick) // PC
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            moveDirectionInput = new Vector3(moveX, moveY, 0f);
        }
        else // Mobile
        {
            if (joystick != null) // Uses the 'joystick' field for movement
            {
                float moveX = joystick.Horizontal;
                float moveY = joystick.Vertical;
                moveDirectionInput = new Vector3(moveX, moveY, 0f);
                joystickMagnitude = moveDirectionInput.magnitude; // Calculate magnitude here
            }
            else { Debug.LogError("Movement Joystick (joystick field) not assigned!", this.gameObject); return; }
        }

        // Use a small deadzone threshold to determine if input is significant
        
        isMoving = moveDirectionInput.magnitude > deadZoneTreshold;

        // Determine if the player wants to run (based on input method and threshold)
        bool wantsToRun = false;
        if (!useJoystick) { wantsToRun = Input.GetKey(KeyCode.LeftShift); }
        else { wantsToRun = joystickMagnitude > joystickRunThreshold; }

        // Determine if running should be re-enabled after stamina depletion
        if (!useJoystick) { if (!Input.GetKey(KeyCode.LeftShift)) { enableRun = true; } }
        else { if (joystickMagnitude < joystickReleaseThreshold) { enableRun = true; } }

        // Set isRunning state based on conditions
        if (enableRun && wantsToRun && PlayerStamina.Instance != null && PlayerStamina.Instance.CanRun() && isMoving) { isRunning = true; }
        else { isRunning = false; }

        // Calculate target speed based on whether moving/running
        Vector3 normalizedMoveDirection = moveDirectionInput.normalized; // Normalize even if magnitude is small
        float targetSpeed = isMoving ? (isRunning ? runSpeed : walkSpeed) : 0f;

        // Apply Acceleration/Deceleration
        if (currentSpeed < targetSpeed)
        {
            // Use runSpeed as the base for acceleration calculation if target is runSpeed
            float accelerationBase = targetSpeed >= runSpeed ? runSpeed : walkSpeed;
            accelerationBase = Mathf.Max(accelerationBase, 0.1f); // Avoid division by zero
            float accelerationRate = accelerationTime > 0 ? accelerationBase / accelerationTime : float.MaxValue;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelerationRate * Time.unscaledDeltaTime);
        }
        else if (currentSpeed > targetSpeed)
        {
            // Use the speed we are coming *from* as the base for deceleration
            float decelerationBase = currentSpeed > walkSpeed ? runSpeed : walkSpeed;
            decelerationBase = Mathf.Max(decelerationBase, 0.1f); // Avoid division by zero
            float decelerationRate = decelerationTime > 0 ? decelerationBase / decelerationTime : float.MaxValue;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, decelerationRate * Time.deltaTime);
        }

        // Ensure speed doesn't go below zero
        currentSpeed = Mathf.Max(0, currentSpeed);

        // --- ADD THIS BLOCK ---
        // Force immediate stop if the joystick is actively being held but is in the neutral/deadzone position
        if (useJoystick && joystick != null && joystick.ControllingPointerId != -1 && joystickMagnitude <= deadZoneTreshold)
        {
            currentSpeed = 0f;
            // Optionally also clear the isRunning flag here if needed, although targetSpeed being 0 should handle it.
            // isRunning = false;
        }
        // --- END ADDED BLOCK ---

        // Calculate final velocity and apply translation
        // Use the normalized direction from input even if speed is zero, prevents snapping
        Vector3 velocity = normalizedMoveDirection * currentSpeed;
        transform.Translate(velocity * Time.deltaTime, Space.World);
        CurrentVelocity = velocity; // Update public velocity property
    }

    private void HandleRunState()
    {
        if (PlayerStamina.Instance != null) { PlayerStamina.Instance.SetRunning(isRunning); }
    }

    private void HandleSquashStretch()
    {
        if (bodyPivot == null) return;
        if (CurrentVelocity.sqrMagnitude > 0.001f)
        {
            float moveAngle = Mathf.Atan2(CurrentVelocity.y, CurrentVelocity.x) * Mathf.Rad2Deg;
            Quaternion targetRot = Quaternion.Euler(0f, 0f, moveAngle);
            bodyPivot.rotation = Quaternion.RotateTowards(bodyPivot.rotation, targetRot, rotationSpeed * Time.unscaledDeltaTime);
        }
        float topSpeed = isDashing ? dashSpeed : (isRunning ? runSpeed : walkSpeed);
        topSpeed = Mathf.Max(topSpeed, 0.1f);
        float speed = CurrentVelocity.magnitude;
        float speedPercent = Mathf.Clamp01(speed / topSpeed);
        Vector3 targetScale;
        float lerpSpeed;
        if (speedPercent < 0.01f && !isDashing)
        {
            targetScale = originalScale;
            lerpSpeed = returnLerpSpeed;
        }
        else
        {
            float stretch = Mathf.Lerp(1f, maxStretch, speedPercent);
            float squash = Mathf.Lerp(1f, maxSquash, speedPercent);
            targetScale = new Vector3(stretch * originalScale.x, squash * originalScale.y, originalScale.z);
            lerpSpeed = stretchLerpSpeed;
        }
        bodyPivot.localScale = Vector3.Lerp(bodyPivot.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }
    // --- End Handlers ---


    // --- Double Tap Dash Handling ---
    private void HandleMobileDoubleTapDash()
    {
        if (doubleTapTimer > 0)
        {
            doubleTapTimer -= Time.deltaTime;
            if (doubleTapTimer <= 0) { tapCount = 0; }
        }

        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    if (touch.position.x > Screen.width / 2)
                    { // Right side check
                        // Ignore taps on UI elements
                        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        {
                            continue; // Skip UI taps
                        }

                        tapCount++;
                        if (tapCount == 1)
                        {
                            doubleTapTimer = DOUBLE_TAP_TIME_WINDOW;
                        }
                        else if (tapCount >= 2 && doubleTapTimer > 0)
                        {
                            // DOUBLE TAP!
                            AttemptDash();
                            tapCount = 0;
                            doubleTapTimer = 0f;
                            break; // Exit loop after successful double tap
                        }
                    }
                }
            }
        }
    }

    // Private method to attempt dashing (checks conditions)
    private void AttemptDash()
    {
        if (!enableDash || !canDash || PlayerStamina.Instance == null || !PlayerStamina.Instance.CanDash())
        {
            Debug.Log("Attempted Dash Failed (Not Ready/No Stamina).");
            return;
        }
        PlayerStamina.Instance.Dash(); // Consume Stamina

        // Determine Dash Direction
        Vector2 direction = transform.right; // Default forward
        if (joystick != null && isMoving)
        { // Check movement joystick
            Vector2 moveInput = new Vector2(joystick.Horizontal, joystick.Vertical);
            if (moveInput.sqrMagnitude > 0.01f) { direction = moveInput.normalized; }
            else if (lastAimDirection != Vector2.zero) { direction = lastAimDirection; }
        }
        else if (lastAimDirection != Vector2.zero) { direction = lastAimDirection; }

        BeginDash(direction); // Execute Dash
        Debug.Log($"Dashing in direction: {direction}");
    }
    // --- End Double Tap Dash Handling ---


    public void ForceStopRunning()
    {
        isRunning = false;
        enableRun = false;
    }

    private void BeginDash(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.01f) dir = transform.right;
        isDashing = true;
        dashDir = dir.normalized;
        dashTimeLeft = dashDuration;
        canDash = false; // Start cooldown timer via coroutine in Update
    }

    private KeyCode GetKeyCode(AbilityHotkey hotkey)
    {
        switch (hotkey)
        {
            case AbilityHotkey.H: return KeyCode.H;
            case AbilityHotkey.G: return KeyCode.G;
            case AbilityHotkey.Q: return KeyCode.Q;
            case AbilityHotkey.X: return KeyCode.X;
            default: Debug.LogWarning($"Unhandled AbilityHotkey: {hotkey}"); return KeyCode.None;
        }
    }

    // --- ABILITY HANDLING (Health/Shield) ---
    private void HandleHealthSurge()
    {
        bool checkKeyboard = !useJoystick;
        if (checkKeyboard)
        {
            KeyCode assignedKey = GetKeyCode(healthSurgeKey);
            if (Input.GetKeyDown(assignedKey)) { ActivateHealthSurge(); }
        }
    }
    private void HandleShieldBurst()
    {
        bool checkKeyboard = !useJoystick;
        if (checkKeyboard)
        {
            KeyCode shieldKey = GetKeyCode(shieldBurstKey);
            if (Input.GetKeyDown(shieldKey)) { ActivateShieldBurst(); }
        }
    }
    public void ActivateHealthSurge()
    {
        if (enableHealthSurge && Time.time >= nextHealthSurgeTime) { PerformHealthSurge(); }
    }
    public void ActivateShieldBurst()
    {
        if (enableShieldBurst && Time.time >= nextShieldBurstTime) { PerformShieldBurst(); }
    }
    private void PerformHealthSurge()
    {
        float healAmount = 0f;
        if (PlayerHealth.Instance != null)
        {
            healAmount = (healthSurgeHealPercent * 0.01f) * PlayerHealth.Instance.maxHealth;
            PlayerHealth.Instance.Heal(healAmount);
        }
        if (healthSurgeSfx != null) healthSurgeSfx.Play();
        nextHealthSurgeTime = Time.time + healthSurgeCooldown;
        AchievementManager.Instance?.ReportProgress(specialAbilityAchvID, 1);
        SaveDataManager.Instance?.SaveGame();
        if (healthSurgeColorCoroutine != null) StopCoroutine(healthSurgeColorCoroutine);
        healthSurgeColorCoroutine = StartCoroutine(FlashColorRoutine(healthSurgeEffectColor, healthSurgeEffectDuration));
        if (healthSurgeButton != null) healthSurgeButton.gameObject.SetActive(false);
        Debug.Log($"Health Surge used! Healed {healAmount} HP.");
    }
    private void PerformShieldBurst()
    {
        nextShieldBurstTime = Time.time + shieldBurstCooldown;
        StartCoroutine(ShieldBurstRoutine());
        AchievementManager.Instance?.ReportProgress(specialAbilityAchvID, 1);
        SaveDataManager.Instance?.SaveGame();
        if (shieldBurstButton != null) shieldBurstButton.gameObject.SetActive(false);
        Debug.Log("Shield Burst activated!");
    }
    private IEnumerator ShieldBurstRoutine()
    {
        if (PlayerHealth.Instance != null) { PlayerHealth.Instance.shieldBurstActive = true; }
        if (shieldBurstSfx != null) shieldBurstSfx.Play();
        if (shieldBurstColorCoroutine != null) StopCoroutine(shieldBurstColorCoroutine);
        shieldBurstColorCoroutine = StartCoroutine(SetColorForDuration(shieldBurstColor, shieldBurstDuration));
        yield return new WaitForSeconds(shieldBurstDuration);
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.shieldBurstActive = false;
            if (playerRenderer != null && playerRenderer.material != null && playerRenderer.material.HasProperty("_EmissionColor"))
            {
                playerRenderer.material.SetColor("_EmissionColor", originalEmissionColor);
            }
        }
        Debug.Log("Shield Burst deactivated.");
    }
    private IEnumerator FlashColorRoutine(Color newColor, float duration)
    {
        if (playerRenderer != null && playerRenderer.material != null && playerRenderer.material.HasProperty("_EmissionColor"))
        {
            playerRenderer.material.SetColor("_EmissionColor", newColor);
            yield return new WaitForSeconds(duration);
            if (playerRenderer != null && playerRenderer.material != null) { playerRenderer.material.SetColor("_EmissionColor", originalEmissionColor); }
        }
        else { yield break; }
    }
    private IEnumerator SetColorForDuration(Color newColor, float duration)
    {
        if (playerRenderer != null && playerRenderer.material != null && playerRenderer.material.HasProperty("_EmissionColor"))
        {
            playerRenderer.material.SetColor("_EmissionColor", newColor);
            yield return new WaitForSeconds(duration);
            if (playerRenderer != null && playerRenderer.material != null) { playerRenderer.material.SetColor("_EmissionColor", originalEmissionColor); }
        }
        else { yield break; }
    }
    // --- End Abilities ---


    // --- Utility ---
    public void ResetMomentum()
    {
        currentSpeed = 0f;
        isRunning = false;
        enableRun = true;
        CurrentVelocity = Vector2.zero;
        if (isDashing) { isDashing = false; }
    }
    // --- End Utility ---
}

// Enum definition (ensure accessible)
public enum AbilityHotkey { H, G, Q, X }