using UnityEngine;

[CreateAssetMenu(menuName = "Game/Achievement", fileName = "NewAchievement")]
public class AchievementDefinition : ScriptableObject
{
    [Header("Meta")]
    [Tooltip("Unique identifier for this achievement.")]
    public string id;

    [Tooltip("Display name shown in the UI.")]
    public string displayName;

    [TextArea]
    [Tooltip("Description of what the player must do.")]
    public string description;

    [Header("Icon")]
    [Tooltip("Icon to show on the achievement tile.")]
    public Sprite icon;

    [Header("Tier Layout")]
    [Tooltip("Define one entry per tier: how much progress is needed and how much money is rewarded.")]
    public Tier[] tiers;       // e.g. tiers.Length >= 1

    [Header("Final‑Tier Unlock (Optional)")]
    [Tooltip("ID of an item or feature to unlock when the last tier is completed.")]
    public string unlockItemId;

    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Progress required to complete this tier (e.g. number of kills).")]
        public int targetValue;

        [Tooltip("Amount of money awarded when this tier is completed.")]
        public int moneyReward;
    }
}
