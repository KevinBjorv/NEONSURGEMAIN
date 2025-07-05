using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// A more performance-friendly minimap system using UI icons (pooled).
/// Ensures the minimap is always visible in Screen Space - Overlay
/// and provides a brightness parameter to prevent fade-out issues.
///
/// Usage:
/// 1) Create a Canvas in "Screen Space - Overlay" mode.
/// 2) Place a Panel/Image as your 'minimapRect' for the background.
/// 3) Assign 'playerIconPrefab', 'squareIconPrefab', 'enemyIconPrefab', and 'orbiterIconPrefab' (all should have an Image).
/// 4) Adjust 'minimapRadius' to scale how much of the world is shown.
/// 5) Set 'brightness' to control how opaque/bright icons appear (1.0 = fully visible).
/// </summary>
public class MinimapManagerPooled : MonoBehaviour
{
    [Header("Minimap UI")]
    [Tooltip("RectTransform representing the minimap area (can be circular).")]
    public RectTransform minimapRect;

    [Tooltip("Prefab for the player icon (sprite).")]
    public GameObject playerIconPrefab;

    [Tooltip("Prefab for the squares' icons (sprite).")]
    public GameObject squareIconPrefab;

    [Tooltip("Prefab for the enemies' icons (sprite).")]
    public GameObject enemyIconPrefab;

    [Tooltip("Prefab for the orbiters' icons (sprite).")]
    public GameObject orbiterIconPrefab;

    [Header("References")]
    [Tooltip("The Player Transform to highlight on the minimap.")]
    public Transform playerTransform;

    [Tooltip("If true, we'll get squares/enemies from SpawnManager activeEntities; otherwise from a tag.")]
    public bool useSpawnManager = true;

    [Header("Minimap Settings")]
    [Tooltip("World distance from player that fits fully in the minimap.")]
    public float minimapRadius = 20f;

    [Tooltip("Refresh rate (seconds) for updating icon positions (e.g., 0.1).")]
    public float refreshInterval = 0.1f;

    [Tooltip("Ignore squares/enemies beyond this distance from the player.")]
    public float cullDistance = 40f;

    [Header("Icon Brightness")]
    [Tooltip("How visible the icons are (1.0 = fully visible, 0.0 = invisible).")]
    public float brightness = 1.0f;

    // Internal icon pools
    private RectTransform _playerIcon;
    private List<RectTransform> _squareIcons = new List<RectTransform>();
    private List<RectTransform> _enemyIcons = new List<RectTransform>();
    private List<RectTransform> _orbiterIcons = new List<RectTransform>();

    // Reusable Lists
    private List<Transform> _nearbySquares = new List<Transform>();
    private List<Transform> _nearbyEnemies = new List<Transform>();
    private List<Transform> _nearbyOrbiters = new List<Transform>();

    private float _timer;
    private float _minimapDiameter;
    private float _halfMapSize;

    private void Awake()
    {
        if (!minimapRect)
        {
            Debug.LogError("MinimapManagerPooled: 'minimapRect' is not assigned!");
            return;
        }

        // Calculate dimensions of minimapRect (assuming it's a circle => width = height)
        _minimapDiameter = minimapRect.sizeDelta.x;
        _halfMapSize = _minimapDiameter * 0.5f;

        // Create the player icon in the minimap
        if (playerIconPrefab)
        {
            GameObject pIcon = Instantiate(playerIconPrefab, minimapRect);
            _playerIcon = pIcon.GetComponent<RectTransform>();
            if (!_playerIcon)
            {
                _playerIcon = pIcon.AddComponent<RectTransform>();
            }
        }

        _nearbySquares = new List<Transform>(50); // Initialize with a reasonable capacity
        _nearbyEnemies = new List<Transform>(50);
        _nearbyOrbiters = new List<Transform>(10);
    }

    private void Start()
    {
        // Initial forced refresh
        _timer = refreshInterval;
        RefreshMinimap();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            RefreshMinimap();
        }
    }

    /// <summary>
    /// Refreshes player icon and any squares, enemies, and orbiters on the minimap.
    /// </summary>
    private void RefreshMinimap()
    {
        if (!playerTransform || !minimapRect) return;
        PositionPlayerIcon();

        // --- Gather entities into the REUSABLE lists ---
        GatherEntities(_nearbySquares, _nearbyEnemies, _nearbyOrbiters); // Pass lists in

        // Update icons using the filled reusable lists
        UpdateSquareIcons(_nearbySquares);
        UpdateEnemyIcons(_nearbyEnemies);
        UpdateOrbiterIcons(_nearbyOrbiters);
    }

    /// <summary>
    /// Positions the player icon at the center of the minimap and applies brightness.
    /// </summary>
    private void PositionPlayerIcon()
    {
        if (_playerIcon)
        {
            _playerIcon.anchoredPosition = Vector2.zero;

            // Apply brightness to the player icon image
            Image iconImg = _playerIcon.GetComponent<Image>();
            if (iconImg != null)
            {
                Color c = iconImg.color;
                c.a = brightness;
                iconImg.color = c;
            }
        }
    }

    /// <summary>
    /// Gathers squares, enemies, and orbiters (within cullDistance).
    /// For orbiters, if OrbiterSpawning is available, use its activeOrbiters list.
    /// Otherwise, fallback to tag search.
    /// </summary>
    private void GatherEntities(List<Transform> squaresOutput, List<Transform> enemiesOutput, List<Transform> orbitersOutput)
    {
        // Clear the lists passed in, ready for refilling
        squaresOutput.Clear();
        enemiesOutput.Clear();
        orbitersOutput.Clear();

        // Ensure useSpawnManager is true for performance!
        if (useSpawnManager && SpawnManager.Instance != null)
        {
            var entities = SpawnManager.Instance.GetActiveEntities(); // Get reference (no allocation)
            float cullDistSqr = cullDistance * cullDistance; // Pre-calculate squared distance

            foreach (var obj in entities)
            {
                if (!obj) continue;

                // --- Use sqrMagnitude for distance check (faster) ---
                float distSqr = (obj.transform.position - playerTransform.position).sqrMagnitude;
                if (distSqr > cullDistSqr) continue;
                // ----------------------------------------------------

                // Fill the lists passed in
                if (obj.CompareTag("NormalSquare"))
                {
                    squaresOutput.Add(obj.transform);
                }
                else if (obj.CompareTag("Enemy"))
                {
                    enemiesOutput.Add(obj.transform);
                }
            }
        }
        else
        {
            // AVOID THIS PATH if possible during gameplay! FindGameObjectsWithTag is slow and allocates.
            Debug.LogWarning("Minimap using slow FindGameObjectsWithTag fallback!");
            // ... (keep fallback logic but understand its cost) ...
            // Remember to use sqrMagnitude here too if you keep it
        }

        // Gather Orbiters (also use sqrMagnitude)
        float orbCullDistSqr = cullDistance * cullDistance;
        if (OrbiterSpawning.Instance != null)
        {
            foreach (var orb in OrbiterSpawning.Instance.activeOrbiters)
            {
                if (orb == null) continue;
                float distSqr = (orb.transform.position - playerTransform.position).sqrMagnitude;
                if (distSqr <= orbCullDistSqr) // Compare squared distances
                {
                    orbitersOutput.Add(orb.transform);
                }
            }
        }
        else
        {
            // AVOID THIS PATH if possible
            Debug.LogWarning("Minimap using slow FindGameObjectsWithTag fallback for Orbiters!");
            // ... (keep fallback logic but use sqrMagnitude) ...
        }

        // No return value needed as we modified the lists passed by reference
    }

    /// <summary>
    /// Updates pooled icons for squares.
    /// </summary>
    private void UpdateSquareIcons(List<Transform> squares)
    {
        if (!squareIconPrefab) return;

        // Ensure we have enough icons in the pool
        EnsureSquareIconPoolSize(squares.Count);

        // Position each square icon
        for (int i = 0; i < squares.Count; i++)
        {
            RectTransform iconRT = _squareIcons[i];
            iconRT.gameObject.SetActive(true);

            Vector2 localPos = ConvertWorldPosToMinimap(squares[i].position);
            iconRT.anchoredPosition = localPos;

            // Apply brightness
            Image sqImg = iconRT.GetComponent<Image>();
            if (sqImg != null)
            {
                Color c = sqImg.color;
                c.a = brightness;
                sqImg.color = c;
            }
        }

        // Disable leftover icons
        for (int i = squares.Count; i < _squareIcons.Count; i++)
        {
            _squareIcons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Updates pooled icons for enemies.
    /// </summary>
    private void UpdateEnemyIcons(List<Transform> enemies)
    {
        if (!enemyIconPrefab) return;

        // Ensure we have enough icons in the pool
        EnsureEnemyIconPoolSize(enemies.Count);

        // Position each enemy icon
        for (int i = 0; i < enemies.Count; i++)
        {
            RectTransform iconRT = _enemyIcons[i];
            iconRT.gameObject.SetActive(true);

            Vector2 localPos = ConvertWorldPosToMinimap(enemies[i].position);
            iconRT.anchoredPosition = localPos;

            // Apply brightness
            Image enemyImg = iconRT.GetComponent<Image>();
            if (enemyImg != null)
            {
                Color c = enemyImg.color;
                c.a = brightness;
                enemyImg.color = c;
            }
        }

        // Disable leftover icons
        for (int i = enemies.Count; i < _enemyIcons.Count; i++)
        {
            _enemyIcons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Updates pooled icons for orbiters.
    /// </summary>
    private void UpdateOrbiterIcons(List<Transform> orbiters)
    {
        if (!orbiterIconPrefab) return;

        // Ensure we have enough icons in the pool
        EnsureOrbiterIconPoolSize(orbiters.Count);

        // Position each orbiter icon
        for (int i = 0; i < orbiters.Count; i++)
        {
            RectTransform iconRT = _orbiterIcons[i];
            iconRT.gameObject.SetActive(true);

            Vector2 localPos = ConvertWorldPosToMinimap(orbiters[i].position);
            iconRT.anchoredPosition = localPos;

            // Apply brightness
            Image orbImg = iconRT.GetComponent<Image>();
            if (orbImg != null)
            {
                Color c = orbImg.color;
                c.a = brightness;
                orbImg.color = c;
            }
        }

        // Disable leftover icons
        for (int i = orbiters.Count; i < _orbiterIcons.Count; i++)
        {
            _orbiterIcons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ensures the pool for square icons is large enough.
    /// </summary>
    private void EnsureSquareIconPoolSize(int neededCount)
    {
        while (_squareIcons.Count < neededCount)
        {
            GameObject sqIcon = Instantiate(squareIconPrefab, minimapRect);
            RectTransform rt = sqIcon.GetComponent<RectTransform>();
            if (!rt) rt = sqIcon.AddComponent<RectTransform>();
            sqIcon.SetActive(false);
            _squareIcons.Add(rt);
        }
    }

    /// <summary>
    /// Ensures the pool for enemy icons is large enough.
    /// </summary>
    private void EnsureEnemyIconPoolSize(int neededCount)
    {
        while (_enemyIcons.Count < neededCount)
        {
            GameObject enIcon = Instantiate(enemyIconPrefab, minimapRect);
            RectTransform rt = enIcon.GetComponent<RectTransform>();
            if (!rt) rt = enIcon.AddComponent<RectTransform>();
            enIcon.SetActive(false);
            _enemyIcons.Add(rt);
        }
    }

    /// <summary>
    /// Ensures the pool for orbiter icons is large enough.
    /// </summary>
    private void EnsureOrbiterIconPoolSize(int neededCount)
    {
        while (_orbiterIcons.Count < neededCount)
        {
            GameObject orbIcon = Instantiate(orbiterIconPrefab, minimapRect);
            RectTransform rt = orbIcon.GetComponent<RectTransform>();
            if (!rt) rt = orbIcon.AddComponent<RectTransform>();
            orbIcon.SetActive(false);
            _orbiterIcons.Add(rt);
        }
    }

    /// <summary>
    /// Converts a world position to local minimap coordinates, clamped by minimapRadius.
    /// </summary>
    private Vector2 ConvertWorldPosToMinimap(Vector3 worldPos)
    {
        Vector3 offset = worldPos - playerTransform.position;
        float dist = offset.magnitude;

        // Clamp offset to minimapRadius
        if (dist > minimapRadius)
        {
            offset = offset.normalized * minimapRadius;
        }

        // Scale to minimap rect
        float scale = _halfMapSize / minimapRadius;
        return new Vector2(offset.x * scale, offset.y * scale);
    }
}
