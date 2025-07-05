using UnityEngine;
using Cinemachine; // Make sure Cinemachine namespace is included

/// <summary>
/// Controls a Cinemachine Virtual Camera's Orthographic Size and Lookahead based on PlayerMovement state.
/// Attach this script to the same GameObject as your ORTHOGRAPHIC CinemachineVirtualCamera.
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CinemachineCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Orthographic Virtual Camera controlled by this script.")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    // No need for explicit PlayerMovement reference if using the singleton

    [Header("Orthographic Size Settings")]
    [Tooltip("Base Orthographic Size when idle or walking (Smaller = More Zoomed In).")]
    [SerializeField] private float baseOrthoSize = 5f; // Adjust default as needed
    [Tooltip("Orthographic Size when running.")]
    [SerializeField] private float runOrthoSize = 6f; // Slightly larger = Zoomed Out
    [Tooltip("Orthographic Size when dashing.")]
    [SerializeField] private float dashOrthoSize = 7.5f; // Even larger = More Zoomed Out
    [Tooltip("How quickly the Orthographic Size changes (higher value = faster change).")]
    [SerializeField] private float sizeChangeSpeed = 5f; // Renamed from fovChangeSpeed

    [Header("Lookahead Settings (Framing Transposer)")]
    [Tooltip("How far the camera looks ahead in the direction of movement (requires Framing Transposer Body).")]
    [SerializeField] private float lookaheadTime = 0.15f;
    [Tooltip("Smoothing time for the lookahead movement.")]
    [SerializeField] private float lookaheadSmoothing = 1.0f;

    // Private variables
    private CinemachineFramingTransposer framingTransposer;
    private PlayerMovement playerMovementInstance; // Cached singleton instance
    private float targetOrthoSize; // Renamed from targetFOV

    void Awake()
    {
        // Get the Virtual Camera component if not assigned
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        // Ensure we have a virtual camera
        if (virtualCamera == null)
        {
            Debug.LogError("[CinemachineCameraController] No CinemachineVirtualCamera found on this GameObject!", this);
            this.enabled = false; // Disable script if no camera
            return;
        }

        // --- CRITICAL: Ensure the Camera is Orthographic ---
        if (!virtualCamera.m_Lens.Orthographic)
        {
            Debug.LogWarning($"[CinemachineCameraController] The Virtual Camera '{virtualCamera.name}' is NOT set to Orthographic. This script controls Orthographic Size. Switching projection...", this);
            virtualCamera.m_Lens.Orthographic = true;
        }
        // ---

        // Try to get the Framing Transposer component from the camera's Body settings
        framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framingTransposer == null)
        {
            Debug.LogWarning("[CinemachineCameraController] No Framing Transposer found on the Virtual Camera. Lookahead will not function.", this);
        }
        else
        {
            // Apply initial lookahead settings
            framingTransposer.m_LookaheadTime = lookaheadTime;
            framingTransposer.m_LookaheadSmoothing = lookaheadSmoothing;
            framingTransposer.m_LookaheadIgnoreY = true; // Usually desired for top-down/2.5D
        }

        // Initialize target Orthographic Size
        targetOrthoSize = baseOrthoSize;
    }

    void Start()
    {
        // Get the PlayerMovement instance (using singleton)
        playerMovementInstance = PlayerMovement.Instance;

        if (playerMovementInstance == null)
        {
            Debug.LogError("[CinemachineCameraController] PlayerMovement Instance not found! Make sure the Player object with PlayerMovement script exists and is active.", this);
            this.enabled = false;
            return;
        }

        // Set initial Orthographic Size
        virtualCamera.m_Lens.OrthographicSize = baseOrthoSize;
    }

    void Update()
    {
        // Ensure PlayerMovement instance is still valid
        if (playerMovementInstance == null) return;

        // --- Orthographic Size Control ---
        // Determine the target Orthographic Size based on player state
        // Remember: Larger size means more zoomed out
        if (playerMovementInstance.isDashing) // Ensure 'isDashing' is accessible
        {
            targetOrthoSize = dashOrthoSize;
        }
        else if (playerMovementInstance.isRunning)
        {
            targetOrthoSize = runOrthoSize;
        }
        else
        {
            targetOrthoSize = baseOrthoSize;
        }

        // Smoothly interpolate the camera's actual Orthographic Size towards the target
        virtualCamera.m_Lens.OrthographicSize = Mathf.Lerp(
            virtualCamera.m_Lens.OrthographicSize,
            targetOrthoSize,
            Time.deltaTime * sizeChangeSpeed
        );

        // --- Lookahead Control ---
        if (framingTransposer != null)
        {
            // Update settings in case they are changed in the inspector during runtime
            framingTransposer.m_LookaheadTime = lookaheadTime;
            framingTransposer.m_LookaheadSmoothing = lookaheadSmoothing;

            // Optional dynamic adjustment example (same as before)
            // float speedFactor = Mathf.Clamp01(playerMovementInstance.CurrentVelocity.magnitude / playerMovementInstance.runSpeed);
            // framingTransposer.m_LookaheadTime = Mathf.Lerp(0.05f, lookaheadTime, speedFactor);
        }
    }

    // Optional: Expose a method to handle Orthographic Size adjustment for upgrades
    // Call this from your upgrade system
    public void SetBaseOrthoSize(float newBaseOrthoSize)
    {
        baseOrthoSize = newBaseOrthoSize;
        // If not currently running or dashing, update target immediately
        if (playerMovementInstance != null && !playerMovementInstance.isRunning && !playerMovementInstance.isDashing)
        {
            targetOrthoSize = baseOrthoSize;
        }
    }
}