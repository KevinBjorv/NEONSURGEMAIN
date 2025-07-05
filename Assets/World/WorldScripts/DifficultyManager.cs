using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Initial Spawn Chances")]
    [Range(0f, 1f)]
    public float initialSafeZoneSpawnChance = 0.02f;
    [Range(0f, 1f)]
    public float initialHostileSpawnChance = 0.024f;

    [Header("Spawn Chance Limits")]
    [Tooltip("Lowest possible safe‑zone spawn chance.")]
    [Range(0f, 1f)]
    public float minSafeZoneSpawnChance = 0.005f;
    [Tooltip("Highest possible hostile spawn chance.")]
    [Range(0f, 1f)]
    public float maxHostileSpawnChance = 0.5f;

    [Header("Ramp Rates (per second)")]
    [Tooltip("How fast the safe‑zone spawn chance decreases over time.")]
    public float safeZoneSpawnChanceDecreaseRate = 0.001f;
    [Tooltip("How fast the hostile spawn chance increases over time.")]
    public float hostileSpawnChanceIncreaseRate = 0.001f;

    // Current, runtime‑updated values
    public float SafeZoneSpawnChance { get; private set; }
    public float HostileSpawnChance { get; private set; }

    [Header("Debug (Read‑Only)")]
    [Tooltip("Shows the effective safe‑zone spawn chance at runtime.")]
    [SerializeField] private float debugSafeZoneSpawnChance;
    [Tooltip("Shows the effective hostile spawn chance at runtime.")]
    [SerializeField] private float debugHostileSpawnChance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SafeZoneSpawnChance = initialSafeZoneSpawnChance;
        HostileSpawnChance = initialHostileSpawnChance;

        // initialize debug values so they show from frame one
        debugSafeZoneSpawnChance = SafeZoneSpawnChance;
        debugHostileSpawnChance = HostileSpawnChance;
    }

    private void Update()
    {
        // ramp down safe zones, clamped to minimum
        SafeZoneSpawnChance = Mathf.Max(
            minSafeZoneSpawnChance,
            SafeZoneSpawnChance - safeZoneSpawnChanceDecreaseRate * Time.deltaTime
        );

        // ramp up hostiles, clamped to maximum
        HostileSpawnChance = Mathf.Min(
            maxHostileSpawnChance,
            HostileSpawnChance + hostileSpawnChanceIncreaseRate * Time.deltaTime
        );

        // update debug‑only fields so you can watch them in the Inspector
        debugSafeZoneSpawnChance = SafeZoneSpawnChance;
        debugHostileSpawnChance = HostileSpawnChance;
    }
}
