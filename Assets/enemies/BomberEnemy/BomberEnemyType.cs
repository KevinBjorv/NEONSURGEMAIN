using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/BomberEnemyType", fileName = "NewBomberEnemyType")]
public class BomberEnemyType : HostileEnemyType
{
    [Header("Bomber Specific Settings")]
    [Tooltip("Speed at which the bomber moves towards the player.")]
    public float bomberMoveSpeed = 1f;

    [Tooltip("Distance from the player at which the bomber will trigger its explosion.")]
    public float explosionTriggerDistance = 2f;

    [Tooltip("Prefab for the explosion effect.")]
    public GameObject explosionPrefab;

    [Tooltip("Damage dealt by the explosion.")]
    public float explosionDamage = 30f;

    [Tooltip("Radius of the explosion.")]
    public float explosionRadius = 3f;

    [Tooltip("Optional: Flashing light or beep interval when close to the player.")]
    public float warningInterval = 0.5f;
}
