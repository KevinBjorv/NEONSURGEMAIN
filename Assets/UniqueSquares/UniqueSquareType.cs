using UnityEngine;

[System.Serializable]
public class UniqueSquareType
{
    [Header("Basic Info")]
    public string squareName = "UniqueSquare";

    [Header("Prefab with Ability")]
    [Tooltip("Prefab that has the ability script attached.")]
    public GameObject uniqueSquarePrefab;

    [Header("Visuals")]
    public Material material;

    [Header("Scoring & Effects")]
    public int pointValue = 10;
    public bool damagesPlayerOnContact = false;
    public int contactDamage = 0;

    [Header("Spawn Settings")]
    [Range(0f, 1f)]
    public float spawnChance = 0.1f;

    [Header("UI Integration")]
    [Tooltip("Icon displayed in the UI when this ability is active.")]
    public Sprite abilityIcon; // Direct reference to the ability's UI icon
}
