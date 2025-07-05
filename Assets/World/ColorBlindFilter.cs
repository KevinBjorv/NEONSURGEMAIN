using UnityEngine;

public enum ColorBlindMode
{
    Normal = 0,
    Protanopia = 1,
    Protanomaly = 2,
    Deuteranopia = 3,
    Deuteranomaly = 4,
    Tritanopia = 5,
    Tritanomaly = 6,
    Achromatopsia = 7,
    Achromatomaly = 8
}

[RequireComponent(typeof(Camera))]
public class ColorBlindFilter : MonoBehaviour
{
    public ColorBlindMode mode = ColorBlindMode.Normal;
    private ColorBlindMode previousMode = ColorBlindMode.Normal;

    private Material material;

    // Define the mixing matrices for each mode.
    // Each mode has three Color vectors for the _R, _G, and _B channels.
    private Color[,] RGB = new Color[9, 3];

    public static ColorBlindFilter Instance;

    private void Awake()
    {
        Instance = this;
        material = new Material(Shader.Find("Hidden/ChannelMixer"));

        // Setup mixing values for each mode
        // Normal: Identity (no change)
        RGB[(int)ColorBlindMode.Normal, 0] = new Color(1, 0, 0, 0);
        RGB[(int)ColorBlindMode.Normal, 1] = new Color(0, 1, 0, 0);
        RGB[(int)ColorBlindMode.Normal, 2] = new Color(0, 0, 1, 0);

        // Protanopia (red-blind)
        RGB[(int)ColorBlindMode.Protanopia, 0] = new Color(0.56667f, 0.43333f, 0, 0);
        RGB[(int)ColorBlindMode.Protanopia, 1] = new Color(0.55833f, 0.44167f, 0, 0);
        RGB[(int)ColorBlindMode.Protanopia, 2] = new Color(0, 0.24167f, 0.75833f, 0);

        // Protanomaly (red-weak)
        RGB[(int)ColorBlindMode.Protanomaly, 0] = new Color(0.81667f, 0.18333f, 0, 0);
        RGB[(int)ColorBlindMode.Protanomaly, 1] = new Color(0.33333f, 0.66667f, 0, 0);
        RGB[(int)ColorBlindMode.Protanomaly, 2] = new Color(0, 0.125f, 0.875f, 0);

        // Deuteranopia (green-blind)
        RGB[(int)ColorBlindMode.Deuteranopia, 0] = new Color(0.625f, 0.375f, 0, 0);
        RGB[(int)ColorBlindMode.Deuteranopia, 1] = new Color(0.70f, 0.30f, 0, 0);
        RGB[(int)ColorBlindMode.Deuteranopia, 2] = new Color(0, 0.30f, 0.70f, 0);

        // Deuteranomaly (green-weak)
        RGB[(int)ColorBlindMode.Deuteranomaly, 0] = new Color(0.80f, 0.20f, 0, 0);
        // These values are approximated from the tutorial table
        RGB[(int)ColorBlindMode.Deuteranomaly, 1] = new Color(0, 0.25833f, 0.74167f, 0);
        RGB[(int)ColorBlindMode.Deuteranomaly, 2] = new Color(0, 0.14167f, 0.85833f, 0);

        // Tritanopia (blue-blind)
        RGB[(int)ColorBlindMode.Tritanopia, 0] = new Color(0.95f, 0.05f, 0, 0);
        RGB[(int)ColorBlindMode.Tritanopia, 1] = new Color(0, 0.43333f, 0.56667f, 0);
        RGB[(int)ColorBlindMode.Tritanopia, 2] = new Color(0, 0.475f, 0.525f, 0);

        // Tritanomaly (blue-weak)
        RGB[(int)ColorBlindMode.Tritanomaly, 0] = new Color(0.96667f, 0.03333f, 0, 0);
        RGB[(int)ColorBlindMode.Tritanomaly, 1] = new Color(0, 0.73333f, 0.26667f, 0);
        RGB[(int)ColorBlindMode.Tritanomaly, 2] = new Color(0, 0.18333f, 0.81667f, 0);

        // Achromatopsia (complete color blindness) – using luminance conversion
        RGB[(int)ColorBlindMode.Achromatopsia, 0] = new Color(0.299f, 0.587f, 0.114f, 0);
        RGB[(int)ColorBlindMode.Achromatopsia, 1] = new Color(0.299f, 0.587f, 0.114f, 0);
        RGB[(int)ColorBlindMode.Achromatopsia, 2] = new Color(0.299f, 0.587f, 0.114f, 0);

        // Achromatomaly (partial) – approximated values
        RGB[(int)ColorBlindMode.Achromatomaly, 0] = new Color(0.618f, 0.32f, 0.062f, 0);
        RGB[(int)ColorBlindMode.Achromatomaly, 1] = new Color(0.163f, 0.775f, 0.062f, 0);
        RGB[(int)ColorBlindMode.Achromatomaly, 2] = new Color(0.163f, 0.32f, 0.516f, 0);
    }

    public void SetMode(ColorBlindMode newMode)
    {
        mode = newMode;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (mode == ColorBlindMode.Normal)
        {
            Graphics.Blit(source, destination);
            previousMode = mode;
            return;
        }

        // If the mode has changed, update the shader mixing values.
        if (mode != previousMode)
        {
            material.SetColor("_R", RGB[(int)mode, 0]);
            material.SetColor("_G", RGB[(int)mode, 1]);
            material.SetColor("_B", RGB[(int)mode, 2]);
            previousMode = mode;
        }
        Graphics.Blit(source, destination, material);
    }
}
