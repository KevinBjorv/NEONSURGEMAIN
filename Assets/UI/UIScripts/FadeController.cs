using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeController : MonoBehaviour
{
    [Tooltip("Enable or disable the fade effect.")]
    public bool enableFadeEffect = true;

    [Tooltip("Duration of fade-out in seconds.")]
    public float fadeDuration = 1f;

    private Image fadeImage;

    private void Awake()
    {
        fadeImage = GetComponent<Image>();
        if (fadeImage == null)
        {
            Debug.LogError("FadeController: No Image component found on the GameObject.");
        }
    }

    private void Start()
    {
        // If fading is disabled, simply disable the overlay
        if (!enableFadeEffect)
        {
            gameObject.SetActive(false);
            return;
        }

        // Start fully opaque
        if (fadeImage != null)
            fadeImage.color = new Color(0, 0, 0, 1);

        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color startColor = fadeImage.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f); // fully transparent

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            // Directly lerping the color from opaque to transparent
            fadeImage.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        // Ensure fully transparent at the end
        fadeImage.color = endColor;
        // Optionally deactivate the overlay after fade completes
        gameObject.SetActive(false);
    }
}
