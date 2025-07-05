using UnityEngine;

/// <summary>
/// Generates a noise texture at runtime and assigns it to a material.
/// </summary>
public class NoiseTextureGenerator : MonoBehaviour
{
    [Header("Material & Shader Property")]
    [Tooltip("Assign the Material that uses the 'Unlit/DynamicNoiseBackground' Shader.")]
    public Material noiseMaterial;

    [Header("Noise Texture Settings")]
    public int textureWidth = 256;
    public int textureHeight = 256;
    [Tooltip("Scale factor for Perlin noise.")]
    public float noiseScale = 20f;

    [Header("Wrapping and Seams")]
    [Tooltip("If you want to tile the noise, set to Repeat.")]
    public TextureWrapMode wrapMode = TextureWrapMode.Repeat;

    void Start()
    {
        // Safety check
        if (noiseMaterial == null)
        {
            Debug.LogError("NoiseTextureGenerator: No material assigned.");
            return;
        }

        // Create a new Texture2D
        Texture2D noiseTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

        // Fill the texture with Perlin noise
        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                float xCoord = (float)x / textureWidth * noiseScale;
                float yCoord = (float)y / textureHeight * noiseScale;
                float sample = Mathf.PerlinNoise(xCoord, yCoord);

                // PerlinNoise returns values in [0,1], so use that as grayscale
                Color c = new Color(sample, sample, sample, 1f);
                noiseTexture.SetPixel(x, y, c);
            }
        }

        // Apply changes to the texture
        noiseTexture.Apply();

        // Set the wrap mode
        noiseTexture.wrapMode = wrapMode;

        // Assign our generated noise to the shader property "_NoiseTex"
        noiseMaterial.SetTexture("_NoiseTex", noiseTexture);
    }
}
