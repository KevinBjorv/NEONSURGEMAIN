using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Player Settings")]
    public Transform playerTransform;

    [Header("Spawn Settings")]
    [Tooltip("Minimum distance the player must move from the last spawn point to trigger new spawns.")]
    public float spawnDistanceThreshold = 1f;

    [Tooltip("Time between spawn checks (in seconds). Spawning won't happen more often than this.")]
    public float spawnInterval = 0.1f;

    [Tooltip("Time between despawn checks (in seconds). despawning won't happen more often than this.")]
    public float despawnInterval = 0.1f;
    private float despawnTimer = 0f;

    [Tooltip("Maximum number of squares (or enemies) to try spawning per spawn cycle.")]
    public int maxSquaresPerSpawn = 3;

    [Tooltip("How many times we attempt a valid random position before giving up.")]
    public int maxPositionTries = 10;

    public float noSpawnRadius = 8f;
    public float spawnRadius = 20f;
    public float despawnRadius = 22f;

    [Header("Perlin Noise Settings")]
    public float noiseScale = 0.05f;
    public float noiseThreshold = 0.4f;

    [Header("Square Pool Settings")]
    [Tooltip("Prefab for normal squares.")]
    public GameObject squarePrefab;

    [Tooltip("How many squares to pre-instantiate.")]
    public int poolSize = 20;

    [Header("Overlap Settings")]
    [Tooltip("Larger radius means squares must be spaced farther apart.")]
    public float overlapCheckRadius = 2.5f;

    [Header("Normal Square Types")]
    public SquareType[] squareTypes;

    [Header("Unique Square Types")]
    public UniqueSquareType[] uniqueSquareTypes;

    [Header("Hostile Enemy Entries")]
    public HostileEnemyEntry[] hostileEnemyEntries;
    public bool enableDespawning = false;

    [Header("Overlap Physics Settings")] 
    [Tooltip("Layer mask containing the layers that spawned entities occupy.")]
    public LayerMask spawnedEntityLayer; 
    private Collider2D[] overlapResults = new Collider2D[1]; // Pre-allocate array for NonAlloc check (size 1 is enough just to detect *any* overlap)

    // ----------------------------------------------------------------------
    //                        SAFE ZONE SETTINGS
    // ----------------------------------------------------------------------
    [Header("Safe Zone Settings")]
    [Tooltip("Prefab for the safe zone (Hexagon sprite, PolygonCollider2D isTrigger).")]
    public GameObject safeZonePrefab;
    [Tooltip("How many safe zones to pre-instantiate.")]
    public int safeZonePoolSize = 2;

    [Tooltip("Extra radius to prevent spawns too close to the safe zone.")]
    public float safeZoneExclusionRadius = 2f;

    [Header("Safe Zone Spawn Cooldown")]
    [Tooltip("Offset to spawn the safe zone on Z-axis, so it doesn't overlap other objects.")]
    public float safeZoneSpawnZOffset = 2f;

    private List<SafeZoneBehavior> activeSafeZones = new List<SafeZoneBehavior>();

    // ----------------------------------------------------------------------
    private Dictionary<GameObject, Queue<GameObject>> entityPools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly List<GameObject> activeEntities = new List<GameObject>();

    private Vector3 lastSpawnPosition;
    private float spawnTimer = 0f;

    // Dictionary to track the next allowed spawn time for each hostile enemy entry.
    private Dictionary<HostileEnemyEntry, float> hostileEnemyCooldowns = new Dictionary<HostileEnemyEntry, float>();

    // ----------------------------------------------------------------------
    //                        SUMMARY SCREEN / UI
    // ----------------------------------------------------------------------
    [Header("Summary Screen / UI")]
    public SummaryScreenManager summaryScreenManager;


    [Header("Mobile settings")]
    public PhoneUIManager phoneUIManager;
    public float spawnRadiusMobile = 25f;
    public float despawnRadiusMobile = 25f;
    public float noSpawnRadiusMobile = 20f;

    private void Awake()
    {
        if(phoneUIManager.isMobile) ConfigureMobileParameters();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Initialize normal square pool
        InitializePool(squarePrefab, poolSize, "NormalSquare");

        // Initialize enemy pools
        foreach (var entry in hostileEnemyEntries)
        {
            if (entry.enemyType != null && entry.enemyType.hostileEnemyPrefab != null)
            {
                InitializePool(entry.enemyType.hostileEnemyPrefab, 5, "Enemy");
            }
        }

        // Initialize safe zone pool
        if (safeZonePrefab != null && safeZonePoolSize > 0)
        {
            InitializePool(safeZonePrefab, safeZonePoolSize, "SafeZone");
        }

        // Initialize cooldown dictionary for hostile enemy entries.
        foreach (var entry in hostileEnemyEntries)
        {
            if (!hostileEnemyCooldowns.ContainsKey(entry))
            {
                hostileEnemyCooldowns.Add(entry, 0f);
            }
        }

        if (playerTransform != null)
        {
            lastSpawnPosition = playerTransform.position;
        }
    }

    private void Update()
    {
        HandleSpawning();

        HandleDespawning();
    }

    private void ConfigureMobileParameters()
    {
        spawnRadius = spawnRadiusMobile;
        despawnRadius = despawnRadiusMobile;
        noSpawnRadius = noSpawnRadiusMobile;
    }

    #region Universal Spawn Chance Validation

    private void OnValidate()
    {
        float total = 0f;
        total += SumChance(squareTypes);
        total += SumChance(uniqueSquareTypes);
        total += SumChance(hostileEnemyEntries);

        if (total > 1f)
        {
            Debug.LogWarning($"Total spawnChance across squares/enemies exceeds 100%! Current total: {total * 100f}%");
        }
        else if (Mathf.Abs(total - 1f) > Mathf.Epsilon)
        {
            Debug.LogWarning($"Total spawnChance across squares/enemies is not 100%. Current total: {total * 100f}%");
        }
    }

    private float SumChance<T>(T[] array)
    {
        if (array == null) return 0f;
        float sum = 0f;
        foreach (var item in array)
        {
            var field = item.GetType().GetField("spawnChance");
            if (field != null)
            {
                sum += (float)field.GetValue(item);
            }
        }
        return sum;
    }

    #endregion

    #region Pooling Methods

    private void InitializePool(GameObject prefab, int count, string defaultTag)
    {
        if (!entityPools.ContainsKey(prefab))
        {
            entityPools[prefab] = new Queue<GameObject>();
        }
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            obj.tag = defaultTag;
            entityPools[prefab].Enqueue(obj);
        }
    }

    private GameObject GetFromPool(GameObject prefab, string desiredTag)
    {
        if (!entityPools.ContainsKey(prefab))
        {
            InitializePool(prefab, 1, desiredTag);
        }

        var pool = entityPools[prefab];

        // 1) First, drain out any destroyed entries
        while (pool.Count > 0)
        {
            GameObject candidate = pool.Dequeue();
            if (candidate != null)
            {
                candidate.tag = desiredTag;
                return candidate;
            }
        }

        // 2) If we get here, pool was empty (or only had dead refs), so we instantiate a fresh one
        GameObject newObj = Instantiate(prefab);
        newObj.tag = desiredTag;
        newObj.SetActive(false);
        return newObj;
    }

    private void ReturnToPool(GameObject prefab, GameObject entity)
    {
        entity.SetActive(false);
        if (!entityPools.ContainsKey(prefab))
        {
            entityPools[prefab] = new Queue<GameObject>();
        }
        entityPools[prefab].Enqueue(entity);
    }

    #endregion

    #region Initial Square Spawn

    public void SpawnInitialSquares(Vector3 viewCenter, float viewWidth, float viewHeight, int amount, float initialMinSpawnRadius, float initialMaxSpawnRadius)
    {
        // Validate inputs
        if (playerTransform == null) { Debug.LogError("[SpawnManager] PlayerTransform not set! Needed for spawn generation center."); return; }
        if (amount <= 0) return;
        if (viewWidth <= 0 || viewHeight <= 0) { Debug.LogError("[SpawnManager] Invalid view dimensions provided for initial spawn!"); return; }

        Debug.Log($"[SpawnManager] Spawning up to {amount} initial squares using Circular Gen clipped by View Bounds (W:{viewWidth:F2}, H:{viewHeight:F2}) centered at {viewCenter}.");

        int spawnedCount = 0;
        int totalAttempts = 0;
        const int maxTotalAttemptsMultiplier = 20;
        int maxAttempts = amount * maxTotalAttemptsMultiplier;

        while (spawnedCount < amount && totalAttempts < maxAttempts)
        {
            totalAttempts++;
            // Pass the pre-calculated bounds to the checking function
            if (TrySpawnInitial_CircularThenClip(viewCenter, viewWidth, viewHeight, initialMinSpawnRadius, initialMaxSpawnRadius))
            {
                spawnedCount++;
            }
        }

        if (spawnedCount < amount)
        {
            Debug.LogWarning($"[SpawnManager] Only managed to spawn {spawnedCount}/{amount} initial squares within view after {totalAttempts} attempts. Check radii, density, or camera view size.");
        }
        else
        {
            Debug.Log($"[SpawnManager] Successfully spawned {spawnedCount} initial squares.");
        }
    }

    private bool TrySpawnInitialNormalSquareInView(Vector3 center, float width, float height, float minRadius)
    {
        float minRadSqr = minRadius * minRadius; // Squared for efficiency

        // Try up to maxPositionTries times to find a valid spot *within this attempt*
        for (int attempt = 0; attempt < maxPositionTries; attempt++)
        {
            // Generate a position RANDOMLY WITHIN THE RECTANGLE
            Vector3 spawnPos = GenerateRandomPositionInRectangle(center, width, height);

            // --- Validation Checks ---
            // 1. Check if too close to the center (minRadius)
            if ((spawnPos - center).sqrMagnitude < minRadSqr)
            {
                continue; // Too close, try generating another point
            }

            // 2. Check Perlin noise, overlap etc. using existing function
            if (IsValidSpawnPosition(spawnPos))
            {
                SpawnNormalSquare(spawnPos); // Use existing spawn function
                return true; // Success!
            }
            // --- End Validation Checks ---
        }
        return false; // Failed to find valid spot within maxPositionTries for this attempt
    }
    private Vector3 GenerateRandomPositionInRectangle(Vector3 center, float width, float height)
    {
        float minX = center.x - width / 2f;
        float maxX = center.x + width / 2f;
        float minY = center.y - height / 2f;
        float maxY = center.y + height / 2f;
        // Assuming Z position should match the center (usually 0 for 2D)
        return new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), center.z);
    }

    private bool GetCameraWorldBounds(Camera cam, Vector3 center, out float worldWidth, out float worldHeight)
    {
        worldWidth = 0;
        worldHeight = 0;
        if (cam == null) return false;

        if (!cam.orthographic)
        {
            Debug.LogWarning("Camera bounds calculation currently assumes orthographic!");
            // You might need specific calculations for perspective here if used
        }
        worldHeight = cam.orthographicSize * 2f;
        worldWidth = worldHeight * cam.aspect;
        return true;
    }

    private bool IsPositionInRectangle(Vector3 position, Vector3 rectCenter, float rectWidth, float rectHeight)
    {
        float halfWidth = rectWidth / 2f;
        float halfHeight = rectHeight / 2f;
        // Check X and Y bounds relative to the center
        return (position.x >= rectCenter.x - halfWidth && position.x <= rectCenter.x + halfWidth &&
                position.y >= rectCenter.y - halfHeight && position.y <= rectCenter.y + halfHeight);
    }

    private bool TrySpawnInitial_CircularThenClip(Vector3 viewCenter, float viewWidth, float viewHeight, float minRadius, float maxRadius)
    {
        // Try finding ONE valid position within maxPositionTries
        for (int attempt = 0; attempt < maxPositionTries; attempt++)
        {
            // 1. Generate position in the CIRCULAR ring (relative to player)
            //    Uses playerTransform directly as center.
            Vector3 spawnPos = GenerateRandomSpawnPosition(minRadius, maxRadius);
            if (spawnPos == Vector3.zero) continue; // Check if generation failed

            // 2. Check if the generated position is INSIDE the camera's calculated rectangle view
            if (!IsPositionInRectangle(spawnPos, viewCenter, viewWidth, viewHeight))
            {
                continue; // Outside camera view, try generating another point
            }

            // 3. Check Perlin noise, overlap etc. (Point is inside view AND outside minRadius already)
            if (IsValidSpawnPosition(spawnPos)) // Keep original validation
            {
                SpawnNormalSquare(spawnPos);
                return true; // Success! One square spawned.
            }
            // Point was inside view, but failed IsValidSpawnPosition (overlap/noise), try again
        }
        return false; // Failed to find a valid, visible spot within maxPositionTries
    }

    #endregion

    #region Normal Spawning

    private void HandleSpawning()
    {
        // Accumulate time
        spawnTimer += Time.deltaTime;

        // Only spawn if enough time has passed (spawnInterval),
        // and the player moved at least spawnDistanceThreshold
        if (spawnTimer >= spawnInterval && playerTransform != null)
        {
            float distanceMoved = Vector3.Distance(playerTransform.position, lastSpawnPosition);
            if (distanceMoved >= spawnDistanceThreshold)
            {
                SpawnEntities();
                lastSpawnPosition = playerTransform.position;
                spawnTimer = 0f; // reset timer
            }
        }
    }

    private void SpawnEntities()
    {
        for (int i = 0; i < maxSquaresPerSpawn; i++)
        {
            // ---------------- Safe‑zone first ----------------
            if (safeZonePrefab != null &&
                Random.value < DifficultyManager.Instance.SafeZoneSpawnChance &&
                TrySpawnSafeZone())
            {
                continue;   // we successfully spawned a safe‑zone, go to next loop slot
            }

            // --------------- Hostile enemy? ------------------
            if (hostileEnemyEntries.Length > 0 &&
                Random.value < DifficultyManager.Instance.HostileSpawnChance)
            {
                var chosen = GetRandomHostileEnemyEntry();
                if (chosen != null &&
                    (UIManager.Instance == null ||
                     UIManager.Instance.GetCurrentTime() >= chosen.minimumSpawnTime) &&   // ≥ not <
                    Time.time >= hostileEnemyCooldowns[chosen])
                {
                    bool spawned = TrySpawnHostileEnemy(chosen.enemyType);
                    if (spawned)
                    {
                        hostileEnemyCooldowns[chosen] = Time.time + chosen.spawnCooldown;
                        continue;   // enemy actually spawned, go to next loop slot
                    }
                    // If we couldn’t place the enemy, we just fall through
                    // to the square‑spawning logic below.
                }
            }

            // -------------- Square (unique or normal) ---------
            bool pickUnique = Random.value < GetUniqueChanceRatio();
            if (pickUnique && uniqueSquareTypes != null && uniqueSquareTypes.Length > 0)
                TrySpawnUniqueSquare();
            else
                TrySpawnNormalSquare();
        }
    }


    private bool TrySpawnSafeZone()
    {
        for (int attempt = 0; attempt < maxPositionTries; attempt++)
        {
            Vector3 spawnPos = GenerateRandomSpawnPosition(noSpawnRadius, spawnRadius);
            if (spawnPos != Vector3.zero && IsValidSpawnPosition(spawnPos))
            {
                SpawnSafeZone(spawnPos);
                return true;
            }
        }
        return false;
    }

    private void SpawnSafeZone(Vector3 spawnPos)
    {
        GameObject zoneObj = GetFromPool(safeZonePrefab, "SafeZone");
        Vector3 offsetPos = new Vector3(spawnPos.x, spawnPos.y, spawnPos.z + safeZoneSpawnZOffset);
        zoneObj.transform.position = offsetPos;
        zoneObj.SetActive(true);

        SafeZoneBehavior sz = zoneObj.GetComponent<SafeZoneBehavior>();
        if (sz != null)
        {
            if (summaryScreenManager != null)
            {
                sz.AssignSummaryScreenManager(summaryScreenManager);
            }
            sz.ActivateSafeZone();
            activeSafeZones.Add(sz);
        }
        activeEntities.Add(zoneObj);
    }

    private void TrySpawnNormalSquare()
    {
        for (int attempt = 0; attempt < maxPositionTries; attempt++)
        {
            Vector3 spawnPos = GenerateRandomSpawnPosition(noSpawnRadius, spawnRadius);
            if (spawnPos != Vector3.zero && IsValidSpawnPosition(spawnPos))
            {
                SpawnNormalSquare(spawnPos);
                break;
            }
        }
    }

    private void SpawnNormalSquare(Vector3 spawnPos)
    {
        SquareType sel = GetRandomSquareType();
        if (sel == null) return;

        GameObject square = GetFromPool(squarePrefab, "NormalSquare");
        if(square == null) return;
        square.transform.position = spawnPos;
        square.SetActive(true);

        PassiveSquare ps = square.GetComponent<PassiveSquare>();
        if (ps != null)
        {
            ps.spawnManager = this;
            ps.Initialize(sel);
        }
        activeEntities.Add(square);
    }

    private bool TrySpawnHostileEnemy(HostileEnemyType enemyType)
    {
        for (int attempt = 0; attempt < maxPositionTries; attempt++)
        {
            Vector3 spawnPos = GenerateRandomSpawnPosition(noSpawnRadius, spawnRadius);
            if (spawnPos != Vector3.zero && IsValidSpawnPosition(spawnPos))
            {
                SpawnHostileEnemy(enemyType, spawnPos);
                return true;
            }
        }
        return false;
    }

    private void SpawnHostileEnemy(HostileEnemyType enemyType, Vector3 pos)
    {
        if (enemyType.hostileEnemyPrefab == null)
        {
            Debug.LogError($"HostileEnemyType '{enemyType.name}' is missing its prefab!");
            return;
        }

        // ─── Get pooled instance ────────────────────────────
        GameObject enemyObj = GetFromPool(enemyType.hostileEnemyPrefab, "Enemy");
        HostileEnemy he = enemyObj.GetComponent<HostileEnemy>();

        // ─── Always position it where we intend to spawn ────
        enemyObj.transform.position = pos;
        he.isPooledInitialized = true;          // keep the flag in case other code relies on it

        // ─── Reactivate / init ──────────────────────────────
        enemyObj.SetActive(true);
        he.enemyType = enemyType;
        he.OnReactivated();

        // ─── Ensure it has a trigger collider for overlap‑checks ───
        Collider2D col = enemyObj.GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D c2d = enemyObj.AddComponent<CircleCollider2D>();
            c2d.isTrigger = true;
            c2d.radius = overlapCheckRadius;
        }

        // ─── Track as active ─────────────────────────────────
        activeEntities.Add(enemyObj);
    }


    private void TrySpawnUniqueSquare()
    {
        for (int attempt = 0; attempt < maxPositionTries; attempt++)
        {
            Vector3 spawnPos = GenerateRandomSpawnPosition(noSpawnRadius, spawnRadius);
            if (spawnPos != Vector3.zero && IsValidSpawnPosition(spawnPos))
            {
                SpawnUniqueSquare(spawnPos);
                break;
            }
        }
    }

    private void SpawnUniqueSquare(Vector3 spawnPos)
    {
        UniqueSquareType sel = GetRandomUniqueSquareType();
        if (sel == null) return;

        GameObject uniqueObj = Instantiate(sel.uniqueSquarePrefab, spawnPos, Quaternion.identity);
        uniqueObj.tag = "UniqueSquare";
        uniqueObj.SetActive(true);
        activeEntities.Add(uniqueObj);

        // Assign the UniqueSquareType if there's a field for it.
        var scripts = uniqueObj.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            var field = script.GetType().GetField("uniqueSquareType");
            if (field != null && field.FieldType == typeof(UniqueSquareType))
            {
                field.SetValue(script, sel);
            }
        }
    }

    #endregion

    #region Despawning

    private void HandleDespawning()
    {
        if (!enableDespawning) return;

        // Accumulate time
        despawnTimer += Time.deltaTime;

        // Only despawn if enough time has passed (despawnInterval)
        if (despawnTimer >= despawnInterval && playerTransform != null)
        {
            despawnTimer = 0f;
            for (int i = activeEntities.Count - 1; i >= 0; i--)
            {
                GameObject entity = activeEntities[i];
                if (entity == null)
                {
                    activeEntities.RemoveAt(i);
                    continue;
                }

                float dist = Vector3.Distance(playerTransform.position, entity.transform.position);
                if (dist > despawnRadius)
                {
                    DespawnEntity(entity);
                }
            }
        }
    }

    public void DespawnEntity(GameObject entity)
    {
        activeEntities.Remove(entity);

        if (entity.CompareTag("NormalSquare"))
        {
            entity.SetActive(false);
            ReturnToPool(squarePrefab, entity);
        }
        else if (entity.CompareTag("Enemy"))
        {
            HostileEnemy he = entity.GetComponent<HostileEnemy>();
            if (he != null && he.enemyType != null && he.enemyType.hostileEnemyPrefab != null)
            {
                entity.SetActive(false);
                ReturnToPool(he.enemyType.hostileEnemyPrefab, entity);
            }
            else
            {
                Destroy(entity);
            }
        }
        else if (entity.CompareTag("SafeZone"))
        {
            SafeZoneBehavior sz = entity.GetComponent<SafeZoneBehavior>();
            if (sz != null)
            {
                activeSafeZones.Remove(sz);
            }
            entity.SetActive(false);
            ReturnToPool(safeZonePrefab, entity);
        }
        else
        {
            // Unique square, etc.
            Destroy(entity);
        }
    }

    public void DestroyUniqueSquare(GameObject uniqueObj, int pointValue)
    {
        ScoreManager.Instance.AddPoints(pointValue);
        if (activeEntities.Contains(uniqueObj))
            activeEntities.Remove(uniqueObj);
        Destroy(uniqueObj);
    }

    #endregion

    #region Position Generation & Utilities

    private Vector3 GenerateRandomSpawnPosition(float minRadius, float maxRadius)
    {
        if (playerTransform == null) return Vector3.zero; // Cannot generate without center point
        float angle = Random.Range(0f, 360f);
        float distance = Random.Range(minRadius, maxRadius);
        float rad = angle * Mathf.Deg2Rad;
        // Generate relative to player position
        return playerTransform.position + new Vector3(Mathf.Cos(rad) * distance, Mathf.Sin(rad) * distance, 0f);
    }

    private Vector3 GenerateRandomSpawnPosition()
    {
        return GenerateRandomSpawnPosition(noSpawnRadius, spawnRadius);
    }

    private bool IsValidSpawnPosition(Vector3 spawnPos)
    {
        // Perlin noise check
        float noiseVal = Mathf.PerlinNoise(spawnPos.x * noiseScale, spawnPos.y * noiseScale);
        if (noiseVal < noiseThreshold) return false;

        // Overlap Check using Physics
        int hitCount = Physics2D.OverlapCircleNonAlloc(spawnPos, overlapCheckRadius, overlapResults, spawnedEntityLayer);
        if (hitCount > 0) return false; // Position is occupied

        // Check for "Border" colliders (Ensure this logic is correct for your layers/tags)
        // Consider using Physics2D.OverlapCircleNonAlloc with a specific Border layer mask if possible
        Collider2D[] borderHits = Physics2D.OverlapCircleAll(spawnPos, overlapCheckRadius); // This still allocates!
        foreach (var c in borderHits)
        {
            if (c.CompareTag("Border")) return false;
        }

        // Check Safe Zone
        if (IsInsideSafeZone(spawnPos)) return false;

        return true; // Position is valid
    }

    private bool IsInsideSafeZone(Vector3 candidatePos)
    {
        foreach (SafeZoneBehavior sz in activeSafeZones)
        {
            if (sz == null) continue;
            float dist = Vector2.Distance(sz.transform.position, candidatePos);
            if (dist < (sz.safeZoneRadius + safeZoneExclusionRadius))
            {
                return true;
            }
        }
        return false;
    }

    private SquareType GetRandomSquareType()
    {
        if (squareTypes == null || squareTypes.Length == 0)
        {
            Debug.LogWarning("No SquareTypes defined!");
            return null;
        }

        float totalChance = 0f;
        foreach (var st in squareTypes) totalChance += st.spawnChance;
        if (totalChance <= 0f) return null;

        float r = Random.value * totalChance;
        float cumulative = 0f;
        foreach (var st in squareTypes)
        {
            cumulative += st.spawnChance;
            if (r <= cumulative)
                return st;
        }
        return squareTypes[squareTypes.Length - 1];
    }

    private UniqueSquareType GetRandomUniqueSquareType()
    {
        if (uniqueSquareTypes == null || uniqueSquareTypes.Length == 0) return null;

        float totalChance = 0f;
        foreach (var ust in uniqueSquareTypes) totalChance += ust.spawnChance;
        if (totalChance <= 0f) return null;

        float r = Random.value * totalChance;
        float cumulative = 0f;
        foreach (var ust in uniqueSquareTypes)
        {
            cumulative += ust.spawnChance;
            if (r <= cumulative) return ust;
        }
        return uniqueSquareTypes[uniqueSquareTypes.Length - 1];
    }

    private float GetUniqueChanceRatio()
    {
        float totalNormalChance = 0f;
        foreach (var st in squareTypes) totalNormalChance += st.spawnChance;

        float totalUniqueChance = 0f;
        foreach (var ust in uniqueSquareTypes) totalUniqueChance += ust.spawnChance;

        float total = totalNormalChance + totalUniqueChance;
        if (total <= 0f) return 0f;
        return totalUniqueChance / total;
    }

    private HostileEnemyEntry GetRandomHostileEnemyEntry()
    {
        float totalChance = 0f;
        foreach (var entry in hostileEnemyEntries) totalChance += entry.spawnChance;
        if (totalChance <= 0f) return null;

        float r = Random.value * totalChance;
        float cumulative = 0f;
        foreach (var entry in hostileEnemyEntries)
        {
            cumulative += entry.spawnChance;
            if (r <= cumulative) return entry;
        }
        return hostileEnemyEntries[hostileEnemyEntries.Length - 1];
    }

    public List<GameObject> GetActiveEntities()
    {
        return activeEntities;
    }
    public void ForceUpdateLastSpawnPosition(Vector3 pos)
    {
        lastSpawnPosition = pos;
    }

    #endregion
}
