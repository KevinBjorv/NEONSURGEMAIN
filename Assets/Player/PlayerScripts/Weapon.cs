using UnityEngine;

[System.Serializable]
public class Weapon
{
    [Header("General Info")]
    public string weaponName;
    public KeyCode hotkey = KeyCode.Alpha1;

    [Header("Firing Modes")]
    public bool automatic;
    public bool semiAutomatic;

    [Header("Firing Parameters")]
    public float fireRate = 0.2f;
    public float bulletSpeed = 10f;
    public float spread = 0f;
    public int pelletCount = 1;

    [Header("Ammunition")]
    public int magazineSize = 10;
    [HideInInspector] public int currentAmmo;

    [Header("Reloading")]
    public float reloadTime = 1.5f;

    [Header("Recoil")]
    public bool recoilEnabled = true;

    [Header("Bullet Penetration")]
    public int bulletPenetration = 0;

    [Header("Bullet Spawn Time")]
    public float bulletSpawnTime = 0.1f;

    [Header("Audio Clips")]
    public AudioClip firingSound;

    [Header("Audio Settings")]
    [Tooltip("Volume multiplier for this weapon's sounds.")]
    public float volumeMultiplier = 1f;

    [Header("Particle Settings")]
    [Tooltip("Number of particles to spawn per shot.")]
    public int particleCount = 10;

    [Tooltip("Spread angle (in degrees) for particle emission.")]
    public float particleSpread = 15f;

    [Header("UI Settings")]
    public Sprite weaponIcon;

    // ------------------- NEW FIELD -------------------
    [Header("Damage")]
    [Tooltip("How much damage each bullet deals to Hostile Enemies.")]
    public float bulletDamage = 10f;
    // -------------------------------------------------

    public void InitializeWeapon()
    {
        currentAmmo = magazineSize;
    }
}
