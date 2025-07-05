using UnityEngine;

/// The list of different upgrade/ability effects we can apply.
public enum StoreEffectType
{
    None,

    // Single-Purchase:
    // - DashUnlock: No effectValues needed (simply unlocks dashing).
    // - IncomeBoost: effectValues[0] represents the income multiplier as a percentage (e.g., 200 means 2× income).
    DashUnlock,
    IncomeBoost,
    BulletRichoet,
    Grenade,
    RocketCompanion,


    // Tiered (first tier unlocks the ability, subsequent tiers improve its effect):
    // - RunSpeed: effectValues array sets the player's run speed.
    //          Example: {8, 10, 12} means run speed is set to 8, then 10, then 12.
    RunSpeed,
    
    // - DamageImmunity: effectValues array sets the invincibility duration in seconds.
    //          Example: {2, 3, 4} means 2 seconds at tier 1, 3 seconds at tier 2, etc.
    DamageImmunity,
    
    // - HealthSurge: effectValues array sets the percentage (as an integer) of max health restored.
    //          Example: {20, 25, 30} means 20% heal at tier 1, 25% at tier 2, etc.
    HealthSurge,
    
    // - ShieldBurst: effectValues array sets the duration of the shield burst in seconds.
    //          Example: {3, 4, 5} means 3 seconds at tier 1, 4 seconds at tier 2, etc.
    ShieldBurst,

    // Also single or tier-based absolute stat upgrades:
    // - PlayerStaminaMax: effectValues array sets the new maximum stamina.
    //          Example: {100, 120, 150} means max stamina becomes 100, then 120, then 150.
    PlayerStaminaMax,
    
    // - PlayerHealthMax: effectValues array sets the new maximum health.
    //          Example: {130, 150, 180, 250} means max health becomes 130, then 150, then 180, then 250.
    PlayerHealthMax,
    
    // - PlayerStaminaRegen: effectValues array sets the stamina regeneration rate (units per second).
    //          Example: {5, 6, 7} means regeneration increases to 5, then 6, then 7 units per second.
    PlayerStaminaRegen,
    
    // - WalkSpeed: effectValues array sets the player's walking speed.
    //          Example: {3, 4, 5} means walk speed becomes 3, then 4, then 5.
    WalkSpeed,
    
    // - HealthRegeneration: effectValues array sets the passive health regeneration percentage.
    //          Example: {2, 3, 5} means 2%, then 3%, then 5% of max health per second.
    HealthRegeneration,
    
    // - DamageMultiplier: effectValues array sets the damage multiplier as an integer percentage.
    //          Example: {120, 150, 200} means 120% damage (1.2×), 150% (1.5×), 200% (2×).
    DamageMultiplier,

    // - AbilityTimeIncrease: effectValues array sets the ability duration multiplier as an integer percentage.
    //          Example: {120, 150, 200} means 120% damage (1.2×), 150% (1.5×), 200% (2×).
    AbilityTimeIncrease
}
