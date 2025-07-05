using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/OrbiterEnemyType", fileName = "NewOrbiterEnemyType")]
public class OrbiterEnemyType : HostileEnemyType
{
    [Header("Orbiter Specific Settings")]
    [Tooltip("Fixed distance the orbiter maintains from the player.")]
    public float orbitDistance = 5f;

    [Tooltip("Speed at which the orbiter rotates around the player.")]
    public float orbitSpeed = 30f;

    [Tooltip("Time interval between shooting projectiles at the player.")]
    public float orbiterShootInterval = 2f;

    [Tooltip("Optional warning sound when the player is standing still (penalty indicator).")]
    public AudioClip idleWarningSound;

    // You can reuse minimumSpawnTime inherited from HostileEnemyEntry
}
