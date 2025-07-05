using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AchievementHover))]
public class AchievementItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI tierText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject checkmark;

    // store for tooltip
    private string _description;

    /// <summary>
    /// Call this when you instantiate the tile.
    /// </summary>
    public void Init(AchievementDefinition def)
    {
        // 1) Basic visuals
        title.text = def.displayName;
        tierText.text = $"{SaveDataManager.Instance.GetAchievement(def.id).currentTier + 1}/{def.tiers.Length}";
        if (iconImage != null) iconImage.sprite = def.icon;

        // 2) Checkmark if fully completed
        bool done = SaveDataManager.Instance.GetAchievement(def.id).currentTier >= def.tiers.Length - 1;
        if (checkmark != null) checkmark.SetActive(done);

        // 3) Store description and hand off to hover
        _description = def.description;
        var hover = GetComponent<AchievementHover>();
        hover.SetDescription(_description);
    }
}
