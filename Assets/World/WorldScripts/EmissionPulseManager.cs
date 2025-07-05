using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A global manager that pulses squares' emission (and optional scale) 
/// based on bass hits and gunshots. Aims for a smooth, satisfying effect.
/// </summary>
public class EmissionPulseManager : MonoBehaviour
{
    public static EmissionPulseManager Instance { get; private set; }

    // ------------------------------------------------------------------------
    // MUSIC PULSE SETTINGS
    // ------------------------------------------------------------------------
    [Header("Music Bass Pulse")]
    [Tooltip("Scales how strong the pulse is for a given bass amplitude.")]
    [Range(0f, 20f)]
    public float bassPulseIntensity = 8.0f;

    [Tooltip("If bass amplitude is below this, ignore it (no pulse).")]
    public float bassThreshold = 0.001f;

    [Tooltip("How long (seconds) the pulse lasts after a strong bass hit.")]
    public float pulseDecayTime = 0.4f;

    [Tooltip("How quickly the pulse 'ramps up' or 'down' for music (larger = faster).")]
    [Range(0f, 10f)]
    public float musicSmoothSpeed = 3f;

    // ------------------------------------------------------------------------
    // GUNSHOT PULSE SETTINGS
    // ------------------------------------------------------------------------
    [Header("Gunshot Pulse")]
    [Tooltip("How strong a gunshot pulse is.")]
    [Range(0f, 20f)]
    public float gunShotPulseIntensity = 10.0f;

    [Tooltip("Duration of the gunshot pulse in seconds.")]
    public float gunShotPulseDuration = 0.3f;

    // ------------------------------------------------------------------------
    // COLOR SATURATION SETTINGS
    // ------------------------------------------------------------------------
    [Header("Color / Brightness Control")]
    [Tooltip("Global clamp on brightness to avoid oversaturation. " +
             "1 = allow full brightness, <1 = clamp more heavily.")]
    [Range(0.1f, 2f)]
    public float maxBrightness = 1.2f;

    [Tooltip("How much the pulsed color deviates from the base color (0=none,1=full).")]
    [Range(0f, 1f)]
    public float colorBlendStrength = 0.8f;

    // ------------------------------------------------------------------------
    // OPTIONAL SCALE PULSE
    // ------------------------------------------------------------------------
    [Header("Optional Scale Pulse")]
    public bool enableScalePulse = true;

    [Tooltip("Max scale pulse factor. e.g. 1.1 means 10% bigger at peak.")]
    [Range(1f, 2f)]
    public float maxScaleFactor = 1.1f;

    // ------------------------------------------------------------------------
    // INTERNALS
    // ------------------------------------------------------------------------
    private List<PassiveSquare> squares = new List<PassiveSquare>();

    // The "target" pulse level for music (based on bass hits).
    private float targetMusicPulse = 0f;
    // The "current" music pulse we use, smoothed over time.
    private float currentMusicPulse = 0f;
    // Timer for gunshot pulse
    private float gunShotTimer = 0f;

    // --- NEW: Variables for MaterialPropertyBlock ---
    private MaterialPropertyBlock pulsePropBlock; // Reuse one block for efficiency
    // Cache Shader Property ID for performance
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // --- NEW: Initialize the block ---
        pulsePropBlock = new MaterialPropertyBlock();
        // ---

        //DontDestroyOnLoad(gameObject); // Keep commented unless needed
    }

    private void Update()
    {
        // 1) Smooth the music pulse
        currentMusicPulse = Mathf.Lerp(currentMusicPulse, targetMusicPulse, Time.deltaTime * musicSmoothSpeed);

        // 2) Decay the targetMusicPulse
        targetMusicPulse = Mathf.MoveTowards(targetMusicPulse, 0f, Time.deltaTime * (targetMusicPulse / pulseDecayTime));

        // 3) Gunshot pulse factor
        float gunShotFactor = 1f;
        if (gunShotTimer > 0f)
        {
            gunShotTimer -= Time.deltaTime;
            float normalized = 1f - (gunShotTimer / gunShotPulseDuration);
            gunShotFactor = 1f + Mathf.Sin(normalized * Mathf.PI) * gunShotPulseIntensity;
        }

        // 4) Combine final pulse factor
        float finalPulseFactor = 1f + currentMusicPulse + (gunShotFactor - 1f);

        // --- MODIFIED: Update loop ---
        // 5) Update all squares using MaterialPropertyBlock
        for (int i = squares.Count - 1; i >= 0; i--) // Iterate backwards if removing items
        {
            PassiveSquare square = squares[i];

            // Skip if square was destroyed or is inactive
            if (square == null || !square.gameObject.activeInHierarchy)
            {
                // Optional: Clean up list if needed, though Unregister should handle it
                // if (square == null) squares.RemoveAt(i);
                continue;
            }

            Renderer rend = square.CachedRenderer; // Use cached renderer
            if (rend == null) continue;

            // Get Base color via the new getter
            Color baseColor = square.BaseColor;

            // Pulsed color calculation (same as before)
            Color pulsedColor = baseColor * finalPulseFactor;
            Color finalColor = Color.Lerp(baseColor, pulsedColor, colorBlendStrength);
            finalColor = ClampColorBrightness(finalColor, maxBrightness);

            // --- USE MATERIAL PROPERTY BLOCK ---
            // Get the existing block data from the renderer (preserves other properties)
            rend.GetPropertyBlock(pulsePropBlock);
            // Set ONLY the emission color property
            pulsePropBlock.SetColor(EmissionColorID, finalColor);
            // Apply the modified block back to this specific renderer's instance data
            rend.SetPropertyBlock(pulsePropBlock);
            // ---

            // Optional: scale pulse (same as before)
            if (enableScalePulse)
            {
                float scale = 1f + (currentMusicPulse + (gunShotFactor - 1f)) * (maxScaleFactor - 1f);
                square.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
        // --- End MODIFIED Update loop ---
    }

    /// <summary>
    /// Receives the bass sum from BackgroundMusicManager each frame.
    /// If it’s above bassThreshold, we bump up targetMusicPulse.
    /// The bigger the sum, the stronger the pulse.
    /// </summary>
    public void SetMusicBassValue(float bassValue)
    {
        if (bassValue > bassThreshold)
        {
            // The stronger the bassValue, the bigger the pulse:
            float newPulse = bassValue * bassPulseIntensity;
            // If new pulse is bigger than current target, we override
            if (newPulse > targetMusicPulse)
            {
                targetMusicPulse = newPulse;
            }
        }
    }

    /// <summary>
    /// Call when a gunshot happens.
    /// </summary>
    public void OnGunShotPulse()
    {
        gunShotTimer = gunShotPulseDuration;
    }

    public void RegisterSquare(PassiveSquare square)
    {
        if (!squares.Contains(square))
            squares.Add(square);
    }

    public void UnregisterSquare(PassiveSquare square)
    {
        if (squares.Contains(square))
            squares.Remove(square);
    }

    /// <summary>
    /// Clamps a color's brightness so it doesn’t exceed 'maxBrightness'.
    /// Simple approach: measure approximate luminance and scale down if needed.
    /// </summary>
    private Color ClampColorBrightness(Color c, float maxBright)
    {
        // Approximate luminance (Rec.709)
        float luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        if (luminance > maxBright)
        {
            float scale = maxBright / luminance;
            c.r *= scale;
            c.g *= scale;
            c.b *= scale;
        }
        return c;
    }
}
