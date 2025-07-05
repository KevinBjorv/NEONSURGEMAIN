using System;
using UnityEngine;

[Serializable]
public class HostileEnemyEntry
{
    public HostileEnemyType enemyType;
    [Range(0f, 1f)]
    public float spawnChance;

    [Tooltip("Cooldown (in seconds) between each spawn of this enemy type.")]
    public float spawnCooldown = 3f;

    [Tooltip("Minimum game time (in seconds) before this enemy is allowed to spawn.")]
    public float minimumSpawnTime = 30f;
}
