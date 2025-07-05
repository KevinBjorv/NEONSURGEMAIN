using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/HostileEnemyType", fileName = "NewHostileEnemyType")]
public class HostileEnemyType : ScriptableObject
{
    [Header("Base Enemy Prefab")]
    [Tooltip("Prefab with HostileEnemy script, SpriteRenderer, Collider2D, etc.")]
    public GameObject hostileEnemyPrefab; // The actual enemy base 

    [Header("Visual Settings")]
    [Tooltip("Material to apply to the HostileEnemy's SpriteRenderer.")]
    public Material enemyMaterial;

    [Header("Basic Settings")]
    public string enemyName = "Hostile Circle";
    public float maxHealth = 50f;

    [Header("Movement & Range")]
    public float moveSpeed = 2f;
    [Tooltip("Enemies only shoot if the player is within this distance.")]
    public float shootRange = 10f;

    [Header("Shooting Settings")]
    [Tooltip("Delay between shots in seconds.")]
    public float shootInterval = 1f;
    [Tooltip("Prefab for the bullet that this enemy fires.")]
    public GameObject bulletPrefab;

    [Tooltip("Bullet speed, lifetime, damage, and homing come from here.")]
    public float bulletSpeed = 2f;
    public float bulletLifetime = 5f;
    public float bulletDamage = 10f;
    [Range(0f, 1f)] public float homingAccuracy = 0.5f;

    [Header("Ability Reward on Death")]
    public bool grantsAbilityOnDeath = true;
    public UniqueSquareType rewardAbility;
}
