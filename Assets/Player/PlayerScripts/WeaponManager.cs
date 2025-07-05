using UnityEngine;
using TMPro;
using Cinemachine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using UnityEngine.EventSystems;

public class WeaponManager : MonoBehaviour
{
    public Weapon[] weapons;
    public int currentWeaponIndex = 0;

    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public Transform barrelEnd;

    [Header("Recoil Settings")]
    public float recoilFactor = 0.01f;
    public float recoilRecoverySpeed = 5f;
    [Tooltip("The absolute maximum amount of recoil allowed.")]
    public float maxRecoilAmount = 5f;
    [Tooltip("Current recoil intensity being applied.")]
    public float currentRecoil = 0f;
    private Vector3 recoilVelocity = Vector3.zero;

    [Header("Automatic Recoil Settings")]
    [Tooltip("Multiplier for how much recoil increases over time when firing automatic weapons.")]
    public float autoRecoilMultiplier = 1f;
    private float autoFireDuration = 0f;  // Tracks continuous fire duration for automatic weapons

    [Header("Camera Shake Settings")]
    public float cameraShakeIntensity = 1f;
    [Tooltip("The absolute maximum amount of camera shake allowed.")]
    public float maxCameraShakeIntensity = 10f;
    [Tooltip("Current camera shake intensity being applied.")]
    public float currentCameraShake = 0f;
    public CinemachineImpulseSource impulseSource;

    [Header("Spread Settings")]
    public float runningSpreadMultiplier = 2f;

    [Header("Universal Sounds")]
    public AudioClip emptyMagazineSound;
    [Range(0f, 1f)] public float emptyMagazineVolume = 1f;

    public AudioClip switchingWeaponSound;
    [Range(0f, 1f)] public float switchingWeaponVolume = 1f;

    public AudioClip reloadSound;
    [Range(0f, 1f)] public float reloadVolume = 1f;

    [Header("UI Settings")]
    public TextMeshProUGUI ammoText;
    public TMP_ColorGradient normalAmmoGradient;
    public TMP_ColorGradient emptyAmmoGradient;

    [Header("Weapon Icon UI")]
    public Image weaponIconImage;

    [Header("Particle Settings")]
    public GameObject muzzleEffectPrefab;

    public PlayerMovement playerMovement;

    [Header("Phone settings")]
    public PhoneUIManager phoneUIManager;


    // How strongly bullets inherit player's movement speed/direction
    [Header("Bullet Inheritance")]
    [Tooltip("Factor by which bullets inherit the player's current velocity.")]
    [Range(0f, 2f)] public float bulletInheritMovementFactor = 0.2f;

    [Header("Universal Damage Multiplier")]
    [Tooltip("All weapons' bullet damage will be multiplied by this value.")]
    public float universalDamageMultiplier = 1f;

    // New parameter to control scroll "heaviness"
    [Header("Weapon Switching via Scrollwheel")]
    [Tooltip("The accumulated scroll delta required to trigger a weapon switch. Higher values mean heavier scrolling.")]
    public float scrollThreshold = 0.2f;
    private float scrollAccumulator = 0f;

    // ---------------------------------------
    // NEW: Bullet Ricochet Upgrade Settings
    [Header("Bullet Ricochet Upgrade Settings")]
    [Tooltip("Percentage chance (0-100) that a bullet will ricochet to a new target.")]
    public float bulletRicochetChance = 0f;
    [Tooltip("Maximum number of ricochet bounces allowed for each bullet.")]
    public int bulletRicochetMaxBounces = 0;
    // ---------------------------------------

    private AudioSource audioSource;
    private float nextFireTime = 0f;
    private bool isReloading = false;
    public static WeaponManager Instance { get; private set; }
    public bool canFire = true; // NEW

    // NEW: Flag to ensure dry shooting SFX only plays once for automatic weapons
    private bool dryShotPlayed = false;

    // ACHIEVEMENTS
    [Header("ACHIEVEMENT variables (debug only)")]
    public int killsSinceReload = 0;
    public int killsWithoutDamage = 0;

    private const string noReloadAchvID = "3";
    private const string noDamageAchvID = "4";

    private bool wasAimStickFiringLastFrame = false;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (weapons.Length > 0)
        {
            if (currentWeaponIndex >= weapons.Length)
                currentWeaponIndex = 0;

            foreach (Weapon w in weapons)
            {
                w.InitializeWeapon();
            }

            PlaySwitchWeaponSound();
            UpdateAmmoDisplay();
            UpdateWeaponIcon();
        }
    }

    private void Update()
    {
        HandleWeaponSwitching();
        HandleManualReload();
        UpdateRecoil();

        // For automatic weapons, reset dryShotPlayed when the fire button is released
        Weapon currentWeapon = weapons[currentWeaponIndex];
        if (currentWeapon.automatic && Input.GetMouseButtonUp(0))
        {
            dryShotPlayed = false;
        }
    }

    private void HandleManualReload()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && weapons.Length > 0)
        {
            Weapon currentWeapon = weapons[currentWeaponIndex];
            if (currentWeapon.currentAmmo < currentWeapon.magazineSize)
            {
                StartCoroutine(Reload(currentWeapon));
            }
        }
    }

    public void RegisterEnemyKill()
    {
        // 1) bump both counters
        killsSinceReload++;
        killsWithoutDamage++;

        // 2) handle the no‑reload achievement
        TryReportStreakAchievement(noReloadAchvID, killsSinceReload, () => { killsSinceReload = 0; });

        // 3) handle the no‑damage achievement
        TryReportStreakAchievement(noDamageAchvID, killsWithoutDamage, null);
    }

    private void TryReportStreakAchievement(string achvID, int streakCount, System.Action resetCallback)
    {
        // find the definition
        var def = AchievementManager.Instance.allAchievements
                    .FirstOrDefault(a => a.id == achvID);
        if (def == null) return;

        // get your saved entry
        var entry = SaveDataManager.Instance.GetAchievement(achvID);

        // calculate next tier index (clamped)
        int nextTier = Mathf.Clamp(entry.currentTier + 1, 0, def.tiers.Length - 1);
        int needed = def.tiers[nextTier].targetValue;

        if (streakCount >= needed)
        {
            // award 1 tick (completes exactly one tier)
            AchievementManager.Instance.ReportProgress(achvID, 1);

            // immediately persist
            SaveDataManager.Instance.SaveGame();

            // if they gave you a reset callback (for reload), call it.
            resetCallback?.Invoke();
        }
    }

    /// <summary>
    /// Resets the streak. Call this at the start of your reload routine.
    /// </summary>
    private void ResetKillStreak()
    {
        killsSinceReload = 0;
    }

    public void FireCurrentWeapon()
    {
        if (!canFire) return; // Early return if shooting is disabled

        if (isReloading || weapons.Length == 0) return;

        Weapon currentWeapon = weapons[currentWeaponIndex];

        if (currentWeapon.currentAmmo < currentWeapon.pelletCount)
        {
            // For automatic weapons, play dry shooting SFX only once
            if (currentWeapon.automatic)
            {
                if (!dryShotPlayed)
                {
                    PlaySound(emptyMagazineSound, emptyMagazineVolume);
                    dryShotPlayed = true;
                }
            }
            else
            {
                PlaySound(emptyMagazineSound, emptyMagazineVolume);
            }

            if (phoneUIManager.isMobile) {
                if (!isReloading) { 
                    StartCoroutine(Reload(currentWeapon));
                }
            }
            return;
        }

        if (Time.time >= nextFireTime)
        {
            PlayMuzzleEffect(currentWeapon);

            for (int i = 0; i < currentWeapon.pelletCount; i++)
            {
                SpawnBullet(currentWeapon);
            }

            PlaySound(currentWeapon.firingSound, currentWeapon.volumeMultiplier);

            currentWeapon.currentAmmo -= currentWeapon.pelletCount;
            UpdateAmmoDisplay();

            nextFireTime = Time.time + currentWeapon.fireRate;

            if (currentWeapon.recoilEnabled)
            {
                ApplyRecoil(currentWeapon, currentWeapon.pelletCount);
            }
        }
    }

    private void SpawnBullet(Weapon weapon)
    {
        if (bulletPrefab == null || barrelEnd == null) return;

        GameObject bullet = Instantiate(bulletPrefab, barrelEnd.position, barrelEnd.rotation);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.damage = weapon.bulletDamage * universalDamageMultiplier;

            // NEW: Pass the ricochet upgrade properties from the store upgrade
            bulletScript.ricochetChance = bulletRicochetChance;
            bulletScript.ricochetMaxBounces = bulletRicochetMaxBounces;

            float finalSpread = weapon.spread;
            if (playerMovement != null && playerMovement.isRunning)
            {
                finalSpread *= runningSpreadMultiplier;
            }

            float angleOffset = Random.Range(-finalSpread, finalSpread);
            Quaternion spreadRotation = Quaternion.Euler(0f, 0f, barrelEnd.rotation.eulerAngles.z + angleOffset);
            Vector2 direction = spreadRotation * Vector2.right;

            bulletScript.SetInitialVelocity(
                direction,
                weapon.bulletSpeed,
                playerMovement != null ? playerMovement.CurrentVelocity : Vector2.zero,
                bulletInheritMovementFactor
            );

            bulletScript.SetPenetration(weapon.bulletPenetration);

            EmissionPulseManager.Instance.OnGunShotPulse();
        }
    }

    private void ApplyRecoil(Weapon weapon, int pelletsFired)
    {
        Vector3 recoilDirection = -barrelEnd.right;
        float recoilAmount = weapon.bulletSpeed * recoilFactor * pelletsFired;

        if (weapon.automatic)
        {
            recoilAmount *= (1f + autoRecoilMultiplier * autoFireDuration);
        }

        Vector3 proposedRecoil = recoilVelocity + recoilDirection * recoilAmount;
        if (proposedRecoil.magnitude > maxRecoilAmount)
        {
            proposedRecoil = recoilDirection * maxRecoilAmount;
        }
        recoilVelocity = proposedRecoil;

        currentRecoil = recoilVelocity.magnitude;

        if (impulseSource != null)
        {
            float impulseMagnitude = recoilAmount * cameraShakeIntensity;
            impulseMagnitude = Mathf.Min(impulseMagnitude, maxCameraShakeIntensity);
            currentCameraShake = impulseMagnitude;
            impulseSource.GenerateImpulse(impulseMagnitude);
        }
    }

    private void UpdateRecoil()
    {
        recoilVelocity = Vector3.Lerp(recoilVelocity, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);
        currentRecoil = recoilVelocity.magnitude;
        transform.position += recoilVelocity * Time.deltaTime;
    }

    private IEnumerator Reload(Weapon weapon)
    {
        if (isReloading) yield break;
        killsSinceReload = 0;

        isReloading = true;
        PlaySound(reloadSound, reloadVolume);

        yield return new WaitForSecondsRealtime(weapon.reloadTime);

        weapon.currentAmmo = weapon.magazineSize;
        isReloading = false;
        UpdateAmmoDisplay();
    }

    public void SwitchToNextWeapon()
    {
        if (weapons.Length == 0) return;
        currentWeaponIndex = (currentWeaponIndex + 1) % weapons.Length;
        StopReloadIfAny();
        PlaySwitchWeaponSound();
        UpdateAmmoDisplay();
        UpdateWeaponIcon();
    }

    public void SwitchToPreviousWeapon()
    {
        if (weapons.Length == 0) return;
        currentWeaponIndex--;
        if (currentWeaponIndex < 0) currentWeaponIndex = weapons.Length - 1;
        StopReloadIfAny();
        PlaySwitchWeaponSound();
        UpdateAmmoDisplay();
        UpdateWeaponIcon();
    }

    private void HandleWeaponSwitching()
    {
        // Accumulate scrollwheel delta to make switching feel heavier.
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        scrollAccumulator += scroll;

        if (scrollAccumulator >= scrollThreshold)
        {
            SwitchToNextWeapon();
            scrollAccumulator -= scrollThreshold;
        }
        else if (scrollAccumulator <= -scrollThreshold)
        {
            SwitchToPreviousWeapon();
            scrollAccumulator += scrollThreshold;
        }

        // Allow weapon selection via hotkeys
        for (int i = 0; i < weapons.Length; i++)
        {
            if (Input.GetKeyDown(weapons[i].hotkey))
            {
                // Only switch weapon and play sound if a different weapon is selected.
                if (currentWeaponIndex != i)
                {
                    currentWeaponIndex = i;
                    StopReloadIfAny();
                    PlaySwitchWeaponSound();
                    UpdateAmmoDisplay();
                    UpdateWeaponIcon();
                }
                break;
            }
        }

        // --- Firing Logic Section ---
        if (weapons.Length == 0) return; // No weapons to handle

        Weapon currentWeapon = weapons[currentWeaponIndex];

        // --- Firing Logic Section in HandleWeaponSwitching ---
        if (weapons.Length == 0 || PlayerMovement.Instance == null) return; // Need weapons and player movement instance

        bool fireIntentDown = false; // Triggered on the frame stick crosses threshold
        bool fireIntentHold = false; // True if stick is held past threshold

        // Define the threshold for the aiming stick to trigger firing
        const float AIM_FIRE_THRESHOLD = 0.5f; // Adjust as needed (0.0 to 1.0)

        // --- Check Input Source ---
        bool isMobile = false;
        if (PlayerMovement.Instance != null)
        {
            isMobile = PlayerMovement.Instance.UseJoystick;
        }
        else if (phoneUIManager != null)
        {
            isMobile = phoneUIManager.isMobile;
        }

        if (isMobile)
        {
            // --- Mobile: Check Aiming Joystick ---
            VirtualJoystick aimingJoystick = PlayerMovement.Instance.aimingJoystick; // Get from PlayerMovement
            if (aimingJoystick != null)
            {
                float aimMagnitude = new Vector2(aimingJoystick.Horizontal, aimingJoystick.Vertical).magnitude;

                // Check if currently held past threshold
                fireIntentHold = aimMagnitude > AIM_FIRE_THRESHOLD;

                // Check if it *just* crossed the threshold this frame (for semi-auto)
                fireIntentDown = !wasAimStickFiringLastFrame && fireIntentHold;

                // Store current state for next frame's check
                wasAimStickFiringLastFrame = fireIntentHold;

                // Note: We are currently NOT checking IsPointerOverGameObject for the aiming stick.
                // Assumes joystick interaction takes priority. Add checks here if needed.

            }
            else
            {
                // Aiming joystick not found, reset state
                wasAimStickFiringLastFrame = false;
            }
        }
        else // Not Mobile (PC/Editor)
        {
            // --- PC/Editor: Check Mouse ---
            bool pointerOverUI = EventSystem.current.IsPointerOverGameObject();
            if (!pointerOverUI)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    fireIntentDown = true;
                    fireIntentHold = true;
                }
                else if (Input.GetMouseButton(0))
                {
                    fireIntentHold = true;
                }
            }
            // Reset aim stick tracking if switching from mobile
            wasAimStickFiringLastFrame = false;
        }

        // --- Process Firing Based on Intent and Weapon Type ---
        // (This section uses the fireIntentDown/Hold flags determined above)

        // Track continuous automatic fire duration
        if (fireIntentHold && currentWeapon.automatic)
        {
            autoFireDuration += Time.deltaTime;
        }
        else
        {
            autoFireDuration = 0f;
            if (currentWeapon.automatic)
            {
                dryShotPlayed = false;
            }
        }

        // Decide whether to call FireCurrentWeapon()
        if (currentWeapon.automatic)
        {
            if (fireIntentHold)
            { // Fire if aim stick held past threshold
                FireCurrentWeapon();
            }
        }
        else
        { // SemiAutomatic and single-shot
            if (fireIntentDown)
            { // Fire only when aim stick *crosses* threshold
                FireCurrentWeapon();
            }
        }
        // --- End of Firing Logic Section ---

        // Track continuous automatic fire duration (only if holding and not over UI)
        if (fireIntentHold && currentWeapon.automatic) // Check the hold flag
        {
            autoFireDuration += Time.deltaTime;
        }
        else
        {
            autoFireDuration = 0f;
            // Reset dry shot flag if player stops holding fire intent for automatic weapons
            if (currentWeapon.automatic)
            {
                dryShotPlayed = false;
            }
        }


        // Decide whether to call FireCurrentWeapon()
        if (currentWeapon.automatic)
        {
            if (fireIntentHold) // Use the hold flag
            {
                FireCurrentWeapon();
            }
        }
        else // Covers SemiAutomatic and single-shot weapons
        {
            if (fireIntentDown) // Use the down flag
            {
                FireCurrentWeapon();
            }
        }

        // --- End of Firing Logic Section ---
    }

    private void StopReloadIfAny()
    {
        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;
        }
    }

    private void PlaySwitchWeaponSound()
    {
        PlaySound(switchingWeaponSound, switchingWeaponVolume);
    }

    private void PlaySound(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volumeMultiplier);
        }
    }

    private void UpdateAmmoDisplay()
    {
        if (ammoText == null || weapons.Length == 0) return;
        Weapon current = weapons[currentWeaponIndex];

        ammoText.text = current.currentAmmo + "/" + current.magazineSize;
        ammoText.enableVertexGradient = true;

        if (current.currentAmmo == 0)
        {
            ammoText.colorGradientPreset = emptyAmmoGradient;
        }
        else
        {
            ammoText.colorGradientPreset = normalAmmoGradient;
        }
    }

    private void UpdateWeaponIcon()
    {
        if (weaponIconImage == null || weapons.Length == 0) return;
        Weapon current = weapons[currentWeaponIndex];

        weaponIconImage.sprite = current.weaponIcon;
        weaponIconImage.enabled = current.weaponIcon != null;
    }

    private void PlayMuzzleEffect(Weapon weapon)
    {
        if (muzzleEffectPrefab == null || barrelEnd == null)
        {
            Debug.LogWarning("Muzzle Effect Prefab or Barrel End is not assigned.");
            return;
        }

        GameObject effectInstance = Instantiate(muzzleEffectPrefab, barrelEnd.position, barrelEnd.rotation, barrelEnd);
        effectInstance.transform.localRotation = Quaternion.identity;

        ParticleSystem particleSystem = effectInstance.GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            var emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            particleSystem.Emit(weapon.pelletCount);

            var shape = particleSystem.shape;
            shape.angle = weapon.particleSpread;

            float totalDuration = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
            Destroy(effectInstance, totalDuration);
        }
        else
        {
            Destroy(effectInstance, 2f);
        }
    }
}
