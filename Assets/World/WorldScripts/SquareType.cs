using UnityEngine;

[System.Serializable]
public class SquareType
{
    [Header("Basic Info")]
    public string squareName = "DefaultSquare";

    [Header("Visuals")]
    public Color color = Color.white;

    [Header("Scoring & Effects")]
    public int pointValue = 10;
    public bool damagesPlayerOnContact = false;
    public int contactDamage = 0;

    [Header("Spawn Settings")]
    [Range(0f, 1f)]
    public float spawnChance = 0.2f;
}
