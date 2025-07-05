using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class SlowMotionAbility : MonoBehaviour
{
    [Header("Slow Motion Settings")]
    public float slowTimeScale = 0.5f;  // e.g. 0.5 = half speed for the environment
    public float slowDuration = 3f;     // seconds of slow motion (in real time)

    [Header("Point Settings")]
    public int pointValue = 10;         // points awarded after slow motion

    [Header("References (Auto-Assigned)")]
    public BackgroundMusicManager musicManager;
    public WeaponManager weaponManager;

    [Header("Reference to UniqueSquareType (for UI icon, etc.)")]
    public UniqueSquareType uniqueSquareType;

    [Header("Audio Mixer Snapshot Settings")]
    public AudioMixer musicMixer;
    public AudioMixerSnapshot normalSnapshot;
    public AudioMixerSnapshot slowMotionSnapshot;
    public float snapshotTransitionTime = 0.5f;

    // Internal tracker to prevent multiple triggers
    private bool slowMotionActive = false;

    private void Awake()
    {
        // Attempt to find managers if not assigned
        if (musicManager == null)
            musicManager = FindObjectOfType<BackgroundMusicManager>();
        if (weaponManager == null)
            weaponManager = FindObjectOfType<WeaponManager>();

        if (musicManager == null)
            Debug.LogWarning("No BackgroundMusicManager found in the scene.");
        if (weaponManager == null)
            Debug.LogWarning("No WeaponManager found in the scene.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only trigger once if not already active
        if ((other.CompareTag("Player") || other.CompareTag("Bullet")) && !slowMotionActive)
        {
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.ReportProgress("2", 1);
                AchievementManager.Instance.ReportProgress("11", 1);
            }
            slowMotionActive = true;

            // Show the slow-motion icon in the UI using slowDuration as the display time
            if (UIManager.Instance != null && uniqueSquareType != null)
            {
                UIManager.Instance.OnAbilityActivated(uniqueSquareType, slowDuration);
            }

            // Start slow motion effect via a separate controller so that it persists after this object is destroyed
            SlowMotionController.CreateSlowMotionController(slowTimeScale, slowDuration,
                                                              musicManager, weaponManager, uniqueSquareType,
                                                              pointValue, normalSnapshot, slowMotionSnapshot, snapshotTransitionTime);

            // Destroy this square immediately after triggering the ability
            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.DestroyUniqueSquare(gameObject, pointValue);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    // Nested controller to handle the slow motion effect independently of the square's lifetime
    private class SlowMotionController : MonoBehaviour
    {
        private float slowTimeScale;
        private float slowDuration;
        private BackgroundMusicManager musicManager;
        private WeaponManager weaponManager;
        private UniqueSquareType uniqueSquareType;
        private int pointValue;

        // Audio Mixer Snapshot variables
        private AudioMixerSnapshot normalSnapshot;
        private AudioMixerSnapshot slowMotionSnapshot;
        private float snapshotTransitionTime;

        // Variables to store player's original stats so we can revert them after slow motion
        private float originalWalkSpeed;
        private float originalRunSpeed;
        private Animator playerAnimator;
        private float originalAnimatorSpeed;

        public static void CreateSlowMotionController(float slowTimeScale, float slowDuration,
                                                      BackgroundMusicManager musicManager, WeaponManager weaponManager,
                                                      UniqueSquareType uniqueSquareType, int pointValue,
                                                      AudioMixerSnapshot normalSnapshot, AudioMixerSnapshot slowMotionSnapshot,
                                                      float snapshotTransitionTime)
        {
            GameObject controllerObj = new GameObject("SlowMotionController");
            DontDestroyOnLoad(controllerObj);
            SlowMotionController controller = controllerObj.AddComponent<SlowMotionController>();
            controller.slowTimeScale = slowTimeScale;
            controller.slowDuration = slowDuration;
            controller.musicManager = musicManager;
            controller.weaponManager = weaponManager;
            controller.uniqueSquareType = uniqueSquareType;
            controller.pointValue = pointValue;
            controller.normalSnapshot = normalSnapshot;
            controller.slowMotionSnapshot = slowMotionSnapshot;
            controller.snapshotTransitionTime = snapshotTransitionTime;
            controller.StartCoroutine(controller.RunSlowMotion());
        }

        private IEnumerator RunSlowMotion()
        {
            // Store original time scale and fixed delta time
            float originalTimeScale = Time.timeScale;
            float originalFixedDelta = Time.fixedDeltaTime;

            // 1) Apply slow motion to the environment
            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = originalFixedDelta * slowTimeScale;

            // 2) Transition to the SlowMotion Snapshot for the music group
            if (slowMotionSnapshot != null)
            {
                slowMotionSnapshot.TransitionTo(snapshotTransitionTime);
            }

            // 4) Wait in real-time (ignoring Time.timeScale)
            yield return new WaitForSecondsRealtime(slowDuration);

            // 5) Revert time and audio settings
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDelta;

            // Transition back to the Normal Snapshot for music
            if (normalSnapshot != null)
            {
                normalSnapshot.TransitionTo(snapshotTransitionTime);
            }

            // Remove UI icon
            if (UIManager.Instance != null && uniqueSquareType != null)
            {
                UIManager.Instance.OnAbilityDeactivated(uniqueSquareType);
            }

            // (Optional) Award points via a dedicated method if required.
            // Example: if (SpawnManager.Instance != null) { SpawnManager.Instance.AwardPoints(pointValue); }

            // Destroy this controller object
            Destroy(gameObject);
        }
    }
}
