using UnityEngine;
using System.Collections.Generic;

public class OrbiterSpawning : MonoBehaviour
{
    public static OrbiterSpawning Instance { get; private set; }

    [Header("Orbiter Spawning Settings")]
    [Tooltip("Minimum time (in seconds) that must elapse before orbiter spawning is enabled.")]
    public float minimumSpawnTime = 30f;

    [Tooltip("Early warning time (in seconds) before the trigger time that the player is warned to move.")]
    public float earlyWarningTime = 3f;

    [Tooltip("The base minimum distance the player must move during the trigger period to avoid spawning an orbiter.")]
    public float minimumMoveDistance = 5f;

    [Tooltip("The time window (in seconds) during which the player's movement is monitored.")]
    public float triggerTime = 10f;

    [Tooltip("The rate at which the minimum move distance increases over time.")]
    public float moveDistanceIncreaseRate = 0.1f;

    [Tooltip("How far away from the player the orbiter should spawn (offset from the player's position).")]
    public float spawnDistanceOffset = 10f;

    [Tooltip("Maximum number of orbiters that can be active at once.")]
    public int maxActiveOrbiters = 3;

    [Header("Orbiter Data")]
    [Tooltip("The OrbiterEnemyType ScriptableObject containing orbiter settings and a prefab reference.")]
    public OrbiterEnemyType orbiterType;

    [Header("Audio")]
    [Tooltip("AudioSource to play the warning sound. The AudioSource should have the warning clip assigned.")]
    public AudioSource warningAudioSource;

    [Header("Debug Info")]
    [Tooltip("Current effective minimum move distance (after increasing over time).")]
    public float debugEffectiveMinimumMoveDistance;

    private Transform playerTransform;
    private float moveTimer = 0f;
    private Vector2 initialPlayerPosition;
    private bool warningGiven = false;

    // Track active orbiters
    public List<GameObject> activeOrbiters = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (PlayerHealth.Instance != null)
        {
            playerTransform = PlayerHealth.Instance.transform;
            initialPlayerPosition = playerTransform.position;
        }
        else
        {
            Debug.LogError("PlayerHealth instance not found! Orbiter spawning cannot track the player's position.");
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // Wait until minimumSpawnTime has passed
        if (UIManager.Instance == null || UIManager.Instance.GetCurrentTime() < minimumSpawnTime)
        {
            moveTimer = 0f;
            initialPlayerPosition = playerTransform.position;
            warningGiven = false;
            return;
        }

        moveTimer += Time.deltaTime;
        float movedDistance = Vector2.Distance(playerTransform.position, initialPlayerPosition);
        debugEffectiveMinimumMoveDistance = minimumMoveDistance + (moveDistanceIncreaseRate * UIManager.Instance.GetCurrentTime());

        // Clean up destroyed orbiters
        activeOrbiters.RemoveAll(item => item == null);

        // Stop warning if already at max orbiters
        if (activeOrbiters.Count >= maxActiveOrbiters)
        {
            if (warningAudioSource != null && warningAudioSource.isPlaying)
            {
                warningAudioSource.Stop();
                warningAudioSource.loop = false;
            }
            warningGiven = false;
        }

        // Early warning
        if (moveTimer >= triggerTime - earlyWarningTime && !warningGiven && activeOrbiters.Count < maxActiveOrbiters)
        {
            Debug.Log("Warning: Move now to avoid penalty!");
            if (warningAudioSource != null)
            {
                warningAudioSource.loop = true;
                warningAudioSource.Play();
            }
            warningGiven = true;
        }

        // Reset if player moved enough
        if (movedDistance >= debugEffectiveMinimumMoveDistance)
        {
            moveTimer = 0f;
            initialPlayerPosition = playerTransform.position;
            if (warningAudioSource != null && warningAudioSource.isPlaying)
            {
                warningAudioSource.Stop();
                warningAudioSource.loop = false;
            }
            warningGiven = false;
        }

        // Spawn orbiter if needed
        if (moveTimer >= triggerTime && movedDistance < debugEffectiveMinimumMoveDistance)
        {
            if (activeOrbiters.Count < maxActiveOrbiters)
            {
                if (orbiterType != null && orbiterType.hostileEnemyPrefab != null)
                {
                    // Spawn the new orbiter (initially at zero)
                    GameObject orbiter = Instantiate(
                        orbiterType.hostileEnemyPrefab,
                        Vector2.zero,
                        Quaternion.identity
                    );
                    orbiter.tag = "Orbiter";
                    activeOrbiters.Add(orbiter);

                    // Reposition ALL orbiters evenly around the player
                    int count = activeOrbiters.Count;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = i * Mathf.PI * 2f / count;
                        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnDistanceOffset;
                        activeOrbiters[i].transform.position = (Vector2)playerTransform.position + offset;
                    }

                    Debug.Log("Orbiter spawned and orbiters repositioned.");
                }
                else
                {
                    Debug.LogError("OrbiterEnemyType or its prefab is not assigned in OrbiterSpawning!");
                }
            }
            else
            {
                Debug.Log("Maximum active orbiters reached. No new orbiter spawned.");
            }

            // Reset cycle
            moveTimer = 0f;
            initialPlayerPosition = playerTransform.position;
            if (warningAudioSource != null && warningAudioSource.isPlaying)
            {
                warningAudioSource.Stop();
                warningAudioSource.loop = false;
            }
            warningGiven = false;
        }
    }

    public void RemoveOrbiter(GameObject orbiter)
    {
        if (activeOrbiters.Contains(orbiter))
        {
            activeOrbiters.Remove(orbiter);
        }
    }
}
