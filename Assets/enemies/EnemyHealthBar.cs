using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("Reference to the Slider component that displays the enemy's health.")]
    public Slider healthSlider;
    [Tooltip("Reference to the Image used for the flash (damage taken) effect.")]
    public Image flashImage;

    private float maxHealth;
    private Coroutine flashCoroutine;

    /// <summary>
    /// Initializes the health bar with the current and maximum health.
    /// </summary>
    public void Initialize(float currentHealth, float maxHealth)
    {
        this.maxHealth = maxHealth;
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }
        if (flashImage != null)
        {
            flashImage.fillAmount = currentHealth / maxHealth;
        }
    }

    /// <summary>
    /// Updates the health bar slider and triggers a flash effect for damage taken.
    /// </summary>
    public void UpdateHealth(float currentHealth, float previousHealth)
    {
        float targetValue = currentHealth / maxHealth;
        if (healthSlider != null)
        {
            healthSlider.value = targetValue;
        }

        // Trigger the red flash effect to indicate damage taken.
        if (flashImage != null)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            float previousValue = previousHealth / maxHealth;
            flashCoroutine = StartCoroutine(FlashEffect(previousValue, targetValue));
        }
    }

    private IEnumerator FlashEffect(float startValue, float endValue)
    {
        // Start with the flash image showing the previous health value.
        flashImage.fillAmount = startValue;
        yield return null;
        float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            flashImage.fillAmount = Mathf.Lerp(startValue, endValue, t);
            yield return null;
        }
        flashImage.fillAmount = endValue;
    }
}
