using UnityEngine;

/// <summary>
/// Applies the actual effects (setting values) after a store purchase.
/// Attach to a GameObject named "StoreUpgradeEffectsManager" in your scene.
/// </summary>
public class StoreUpgradeEffects : MonoBehaviour
{
    public static StoreUpgradeEffects Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private const string rocketCompanionAchvID = "9";

    /// <summary>
    /// Applies or unlocks the effect from StoreUpgradeData.
    /// Values are set absolutely, not added.
    /// </summary>
    public void ApplyUpgradeOrAbility(StoreUpgradeData data)
    {
        if (data == null) return;

        // Figure out which tier's value to use.
        float effectValue;
        if (data.isTiered)
        {
            int index = Mathf.Min(data.currentTier, data.effectValues.Length - 1);
            effectValue = data.effectValues[index];
        }
        else
        {
            effectValue = data.effectValues.Length > 0 ? data.effectValues[0] : 0f;
        }

        switch (data.effectType)
        {
            case StoreEffectType.None:
                Debug.Log("[StoreUpgradeEffects] 'None' effect selected. No changes made.");
                break;

            case StoreEffectType.DashUnlock:
                if (PlayerMovement.Instance != null)
                {
                    PlayerMovement.Instance.enableDash = true;
                    Debug.Log("[StoreUpgradeEffects] Dash unlocked!");
                }
                break;

            case StoreEffectType.Grenade:
                var grenadeThrow = FindObjectOfType<GrenadeThrow>();
                if (grenadeThrow != null)
                {
                    grenadeThrow.isEnabled = true;
                    Debug.Log("[StoreUpgradeEffects] Grenade throwing ability enabled!");
                }
                else
                {
                    Debug.LogWarning("[StoreUpgradeEffects] GrenadeThrow component not found in the scene.");
                }
                break;

            case StoreEffectType.RocketCompanion:
                var rocketMgr = FindObjectOfType<RocketCompanionManager>();
                if (rocketMgr != null)
                {
                    // effectValue → how many rockets to spawn
                    int count = Mathf.Max(0, Mathf.RoundToInt(effectValue));
                    rocketMgr.SetRocketCount(count);
                    AchievementManager.Instance.ReportProgress(rocketCompanionAchvID, 1);
                    Debug.Log($"[StoreUpgradeEffects] Rocket companions set to {count}.");
                }
                else
                {
                    Debug.LogWarning("[StoreUpgradeEffects] RocketCompanionManager not found in the scene.");
                }
                break;

            case StoreEffectType.AbilityTimeIncrease:
                var uniqMgr = FindObjectOfType<UniqueSquareManager>();
                if (uniqMgr != null)
                {
                    uniqMgr.universalUniquesquareAbilityDurationMultiplier = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Unique‑square ability duration ×{effectValue}.");
                }
                else
                {
                    Debug.LogWarning("[StoreUpgradeEffects] UniqueSquareManager component not found.");
                }
                break;

            case StoreEffectType.RunSpeed:
                if (PlayerMovement.Instance != null)
                {
                    PlayerMovement.Instance.enableRun = true;
                    PlayerMovement.Instance.runSpeed = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Running enabled/set to {effectValue}.");
                }
                break;

            case StoreEffectType.DamageImmunity:
                if (PlayerHealth.Instance != null)
                {
                    PlayerHealth.Instance.enableInvincibility = true;
                    PlayerHealth.Instance.invincibilityDuration = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Invincibility unlocked/set to {effectValue} sec.");
                }
                break;

            case StoreEffectType.HealthSurge:
                if (PlayerMovement.Instance != null)
                {
                    PlayerMovement.Instance.enableHealthSurge = true;
                    PlayerMovement.Instance.healthSurgeHealPercent = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Health Surge unlocked/set to {effectValue}%.");
                }
                break;

            case StoreEffectType.PlayerStaminaMax:
                if (PlayerStamina.Instance != null)
                {
                    PlayerStamina.Instance.maxStamina = effectValue;
                    PlayerStamina.Instance.ResetStamina();
                    Debug.Log($"[StoreUpgradeEffects] Max stamina set to {effectValue}.");
                }
                break;

            case StoreEffectType.PlayerHealthMax:
                if (PlayerHealth.Instance != null)
                {
                    float oldMax = PlayerHealth.Instance.maxHealth;
                    PlayerHealth.Instance.maxHealth = effectValue;
                    if (effectValue > oldMax)
                        PlayerHealth.Instance.Heal(effectValue - oldMax);
                    Debug.Log($"[StoreUpgradeEffects] Max health set to {effectValue}.");
                }
                break;

            case StoreEffectType.PlayerStaminaRegen:
                if (PlayerStamina.Instance != null)
                {
                    PlayerStamina.Instance.regenPerSecond = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Stamina regen set to {effectValue}/sec.");
                }
                break;

            case StoreEffectType.WalkSpeed:
                if (PlayerMovement.Instance != null)
                {
                    PlayerMovement.Instance.walkSpeed = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Walk speed set to {effectValue}.");
                }
                break;

            case StoreEffectType.HealthRegeneration:
                if (PlayerHealth.Instance != null)
                {
                    PlayerHealth.Instance.maxPassiveRegenPercent = effectValue / 100f;
                    Debug.Log($"[StoreUpgradeEffects] Passive health regen set to {effectValue}%/sec.");
                }
                break;

            case StoreEffectType.DamageMultiplier:
                if (WeaponManager.Instance != null)
                {
                    WeaponManager.Instance.universalDamageMultiplier = effectValue / 100f;
                    Debug.Log($"[StoreUpgradeEffects] Damage multiplier set to {effectValue}%.");
                }
                break;

            case StoreEffectType.IncomeBoost:
                if (CurrencyManager.Instance != null)
                {
                    CurrencyManager.Instance.incomeMultiplier = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Income multiplier set to {effectValue}.");
                }
                break;

            case StoreEffectType.ShieldBurst:
                if (PlayerMovement.Instance != null)
                {
                    PlayerMovement.Instance.enableShieldBurst = true;
                    PlayerMovement.Instance.shieldBurstDuration =
                        effectValue > 0 ? effectValue : PlayerMovement.Instance.shieldBurstDuration;
                    Debug.Log($"[StoreUpgradeEffects] Shield Burst unlocked; duration = {PlayerMovement.Instance.shieldBurstDuration} sec.");
                }
                break;

            case StoreEffectType.BulletRichoet:
                if (WeaponManager.Instance != null)
                {
                    WeaponManager.Instance.bulletRicochetChance = effectValue;
                    Debug.Log($"[StoreUpgradeEffects] Bullet ricochet chance set to {effectValue}%.");
                }
                break;

            default:
                Debug.LogWarning($"[StoreUpgradeEffects] No handler for effectType = {data.effectType}.");
                break;
        }
    }
}
