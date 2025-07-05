using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps every rocket companion in a small wing that
/// sits directly *behind the player’s aiming direction* (opposite the
/// muzzle). Formation drifts very slightly so it never looks rigid.
/// </summary>
public class RocketCompanionManager : MonoBehaviour
{
    /* ─────────────   Inspector knobs   ───────────── */
    [Header("Prefabs & Player")]
    [Tooltip("Rocket companion prefab (must contain RocketCompanion script).")]
    public GameObject rocketPrefab;
    [Tooltip("Reference to the player (automatically found if left null).")]
    public Transform playerTransform;

    [Header("Wing Formation")]
    [Tooltip("Distance behind the weapon muzzle that the wing rests.")]
    public float followDistance = 1.8f;
    [Tooltip("Side‑to‑side spacing between rockets.")]
    public float lateralSpacing = 0.9f;
    [Tooltip("Tiny idle sway so the wing never looks frozen (deg/sec).")]
    public float idleDriftDegPerSecond = 25f;

    [Header("How many rockets")]
    [Tooltip("Active rocket companions (set by store upgrade).")]
    public int rocketCount = 1;
    /* ─────────────────────────────────────────────── */

    public bool IsActive => enabled && gameObject.activeInHierarchy;

    // Tag PassiveSquares must carry
    public const string PassiveSquareTag = "NormalSquare";

    /* — Public helper for rockets — */
    public Vector2 PlayerPos => playerTransform != null ? (Vector2)playerTransform.position : Vector2.zero;
    public Vector2 PlayerFwd => playerTransform != null ? (Vector2)playerTransform.right : Vector2.right; // muzzle direction

    /* — Private — */
    readonly List<RocketCompanion> rockets = new();
    // Removed lastPlayerPos as it wasn't used
    float driftAngle;

    /* ─────────────   Unity   ───────────── */
    void Awake()
    {
        if (playerTransform == null)
        {
            // Try finding PlayerMovement first
            var p = FindObjectOfType<PlayerMovement>();
            if (p != null)
            {
                playerTransform = p.transform;
            }
            else
            {
                // Fallback to finding GameObject tagged "Player" if PlayerMovement script isn't found
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    playerTransform = playerObj.transform;
                }
                else
                {
                    Debug.LogError("RocketCompanionManager: Could not find Player transform!", this);
                    enabled = false; // Disable the manager if no player is found
                    return;
                }
            }
        }

        // Initial spawn requires player position, no need for lastPlayerPos here
        SpawnOrCull();
    }

    void Update()
    {
        if (playerTransform == null) return; // Guard clause if player was destroyed or not found

        // Calculate formation orientation based on current player aim
        Vector2 backDir = -PlayerFwd.normalized;
        Vector2 rightDir = new(backDir.y, -backDir.x); // Perpendicular to backDir

        // Idle wiggle calculation
        driftAngle += idleDriftDegPerSecond * Time.deltaTime;
        // Keep driftAngle from growing infinitely large
        if (driftAngle > 360f) driftAngle -= 360f;
        else if (driftAngle < -360f) driftAngle += 360f;

        // Calculate wiggle offset (slight side-to-side movement)
        // Using Sin ensures smooth oscillation. The 0.2f controls the magnitude of the wiggle.
        Vector2 wiggle = rightDir * Mathf.Sin(driftAngle * Mathf.Deg2Rad) * 0.2f;

        // Calculate the center point of the rocket wing formation
        Vector2 centre = PlayerPos + backDir * followDistance + wiggle;

        // Assign target follow slots to each active rocket
        for (int i = 0; i < rockets.Count; ++i)
        {
            if (rockets[i] != null) // Ensure rocket hasn't been destroyed externally
            {
                // Calculate the position for this specific rocket's slot
                Vector2 slot = CalculateFormationSlot(i, rockets.Count, centre, rightDir);
                rockets[i].SetFollowSlot(slot);
            }
            else
            {
                // If a rocket was destroyed unexpectedly, log it and consider removing it from the list
                Debug.LogWarning($"RocketCompanionManager: Rocket at index {i} is null. It might have been destroyed.");
                // Optionally remove it here: rockets.RemoveAt(i); i--; // Decrement i because the list size changed
            }
        }
    }

    /* ─────────────   Public API   ───────────── */
    /// <summary>
    /// Sets the desired number of active rocket companions.
    /// </summary>
    /// <param name="count">The target number of rockets.</param>
    public void SetRocketCount(int count)
    {
        rocketCount = Mathf.Max(0, count);
        SpawnOrCull();
    }

    /* ─────────────   Internals   ───────────── */

    /// <summary>
    /// Calculates the world position for a specific slot in the wing formation.
    /// </summary>
    /// <param name="index">The index of the rocket (0 to count-1).</param>
    /// <param name="totalCount">The total number of rockets in the formation.</param>
    /// <param name="formationCenter">The calculated center point of the formation.</param>
    /// <param name="formationRightDir">The vector pointing to the right of the formation.</param>
    /// <returns>The world position Vector2 for the slot.</returns>
    Vector2 CalculateFormationSlot(int index, int totalCount, Vector2 formationCenter, Vector2 formationRightDir)
    {
        // Calculate a centered index:
        // e.g., for 3 rockets (indices 0, 1, 2), centred indices are -1, 0, 1
        // e.g., for 4 rockets (indices 0, 1, 2, 3), centred indices are -1.5, -0.5, 0.5, 1.5
        float centredIndex = index - (totalCount - 1) * 0.5f;

        // Calculate the offset from the center based on spacing and return the final position
        return formationCenter + formationRightDir * centredIndex * lateralSpacing;
    }

    /// <summary>
    /// Spawns or destroys rockets to match the target rocketCount.
    /// Newly spawned rockets are placed directly into their calculated formation slot.
    /// </summary>
    void SpawnOrCull()
    {
        if (playerTransform == null)
        {
            Debug.LogError("RocketCompanionManager: Cannot spawn rockets, Player transform is null.", this);
            return;
        }
        if (rocketPrefab == null)
        {
            Debug.LogError("RocketCompanionManager: Cannot spawn rockets, Rocket Prefab is not assigned.", this);
            return;
        }


        // Cull excess rockets
        while (rockets.Count > rocketCount)
        {
            int lastIndex = rockets.Count - 1;
            var r = rockets[lastIndex];
            rockets.RemoveAt(lastIndex);
            if (r != null) // Check if it hasn't already been destroyed
            {
                // Use DestroyImmediate if called from editor context outside play mode,
                // otherwise use Destroy. Check Application.isPlaying.
                if (Application.isPlaying)
                {
                    Destroy(r.gameObject);
                }
                else
                {
                    // Be cautious with DestroyImmediate outside specific editor needs
                    // DestroyImmediate(r.gameObject);
                    Debug.LogWarning("SpawnOrCull called outside play mode. Consider using DestroyImmediate if needed for editor tools.");
                }
            }
        }

        // Spawn missing rockets
        if (rockets.Count < rocketCount) // Only calculate formation if we need to spawn
        {
            // --- Calculate formation basis for spawning ---
            // We need the formation details *at the moment of spawning*
            Vector2 currentPos = PlayerPos;
            Vector2 backDir = -PlayerFwd.normalized;
            Vector2 rightDir = new(backDir.y, -backDir.x);
            // Base center point *without* wiggle for consistent initial spawn placement
            Vector2 baseCentre = currentPos + backDir * followDistance;
            // ---

            while (rockets.Count < rocketCount)
            {
                // Calculate the specific spawn position for this new rocket
                // Its index will be the current count before adding it.
                // The total count will be the target `rocketCount`.
                int spawnIndex = rockets.Count;
                Vector2 spawnPos = CalculateFormationSlot(spawnIndex, rocketCount, baseCentre, rightDir);

                // Instantiate the rocket at the calculated formation position
                GameObject go = Instantiate(rocketPrefab, spawnPos, playerTransform.rotation); // Use player rotation initially
                RocketCompanion roc = go.GetComponent<RocketCompanion>();

                if (roc != null)
                {
                    roc.Initialise(this); // Handshake with the manager
                    rockets.Add(roc);     // Add to the list
                }
                else
                {
                    Debug.LogError($"RocketCompanionManager: Prefab '{rocketPrefab.name}' is missing the RocketCompanion script!", rocketPrefab);
                    Destroy(go); // Destroy the incorrectly configured instance
                    break; // Stop trying to spawn if the prefab is wrong
                }
            }
        }
    }
}