using UnityEngine;

public class AchievementsUIManager : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public RectTransform contentParent;          // the "Content" under your Scroll View
    public AchievementItemUI itemPrefab;        // the prefab you just made

    void Start()
    {
        Populate();
    }
    void Populate()
    {
        foreach (Transform c in contentParent)
            Destroy(c.gameObject);

        foreach (var def in AchievementManager.Instance.allAchievements)
        {
            GameObject go = Instantiate(itemPrefab.gameObject, contentParent);
            go.SetActive(true);

            var ui = go.GetComponent<AchievementItemUI>();
            ui.Init(def);
        }
    }
}
