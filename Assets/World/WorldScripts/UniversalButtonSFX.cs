using UnityEngine;
using UnityEngine.UI;

public class UniversalButtonSFX : MonoBehaviour
{
    [Header("Button Audio Source")]
    [Tooltip("AudioSource that plays the button click sound. This source should have the desired clip set as its default clip.")]
    public AudioSource buttonAudioSource;

    private void Awake()
    {
        // Ensure we have an AudioSource assigned. If not, try to get one on this GameObject or add one.
        if (buttonAudioSource == null)
        {
            buttonAudioSource = GetComponent<AudioSource>();
            if (buttonAudioSource == null)
            {
                buttonAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void Start()
    {
        // Find all Button components in the scene.
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button btn in buttons)
        {
            // Add a listener to each button's onClick event.
            btn.onClick.AddListener(PlayButtonSFX);
        }
    }

    /// <summary>
    /// Plays the button click sound using the assigned AudioSource.
    /// </summary>
    private void PlayButtonSFX()
    {
        if (buttonAudioSource != null && buttonAudioSource.clip != null)
        {
            buttonAudioSource.PlayOneShot(buttonAudioSource.clip);
        } else
        {
            Debug.LogWarning("No button audio source or clip assigned");
        }
    }
}
