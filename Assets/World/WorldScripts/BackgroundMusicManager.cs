using System;
using UnityEngine;

public class BackgroundMusicManager : MonoBehaviour
{
    [Header("Main Menu Theme")]
    public AudioClip mainMenuTheme;
    [Tooltip("Start time (in seconds) if the user opens the game from the desktop.")]
    public float mainMenuThemeStartTimeDesktop = 0f;
    [Tooltip("Start time (in seconds) if the user returns to / opens the menu from inside the game.")]
    public float mainMenuThemeStartTimeMenu = 0f;

    [Header("Main Theme Song")]
    public AudioClip mainThemeSong;
    [Tooltip("Start time (in seconds) if the user opens the game from the desktop (rare case).")]
    public float mainThemeSongStartTimeDesktop = 0f;
    [Tooltip("Start time (in seconds) if the user transitions from the main menu.")]
    public float mainThemeSongStartTimeMenu = 0f;

    [Header("Audio Source References")]
    [Tooltip("Assign two AudioSources in the Inspector (e.g., one for Main Menu, one for Main Theme).")]
    public AudioSource audioSourceMenu;
    public AudioSource audioSourceMain;

    [Header("Cross-Fade Settings")]
    [Tooltip("Duration (in seconds) to fade out one track and fade in the other.")]
    public float crossFadeDuration = 1.5f;

    [Tooltip("Time (in seconds) of silence after one track fades out, before next track fades in.")]
    public float transitionPause = 0.5f;

    [Header("Spectrum Analysis Settings")]
    [Tooltip("Number of samples to analyze in the FFT (512, 1024, etc.). Must be a power of two.")]
    public int fftSampleSize = 512;

    [Tooltip("Lower freq range (Hz) considered as 'bass' (e.g., 20 Hz).")]
    public float lowFreq = 20f;

    [Tooltip("Upper freq range (Hz) considered as 'bass' (e.g., 200 Hz).")]
    public float highFreq = 200f;

    private float[] spectrumDataMenu;
    private float[] spectrumDataMain;

    private bool isCrossFading = false;
    private float crossFadeElapsed = 0f;

    // We removed the initialVolumeMenu / initialVolumeMain logic to avoid forcing volumes!
    // We will simply rely on volume=1 for playing sources and let the AudioMixer control final volume.

    public static BackgroundMusicManager Instance { get; private set; }

    private void Awake()
    {
        // Basic singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (audioSourceMenu == null || audioSourceMain == null)
        {
            Debug.LogWarning("Please assign two AudioSource references in the inspector.");
            return;
        }

        // Ensure these AudioSources are set to loop
        audioSourceMenu.playOnAwake = false;
        audioSourceMain.playOnAwake = false;
        audioSourceMenu.loop = true;
        audioSourceMain.loop = true;

        // Allocate arrays for FFT data
        spectrumDataMenu = new float[fftSampleSize];
        spectrumDataMain = new float[fftSampleSize];

        // --- CHANGED: We'll default each AudioSource to volume = 0 or 1
        // Decide which track is playing initially. For example, if the menu theme is assigned,
        // we can start with that playing at volume=1 and the other at volume=0.
        bool menuThemeAssigned = (audioSourceMenu.clip != null || mainMenuTheme != null);
        bool mainThemeAssigned = (audioSourceMain.clip != null || mainThemeSong != null);

        if (menuThemeAssigned)
        {
            audioSourceMenu.volume = 1f; // Let the AudioMixer handle final volume
            audioSourceMenu.clip = audioSourceMenu.clip ?? mainMenuTheme;
            audioSourceMenu.Play();

            audioSourceMain.volume = 0f;
            audioSourceMain.Stop();
        }
        else if (mainThemeAssigned)
        {
            audioSourceMain.volume = 1f;
            audioSourceMain.clip = audioSourceMain.clip ?? mainThemeSong;
            audioSourceMain.Play();

            audioSourceMenu.volume = 0f;
            audioSourceMenu.Stop();
        }
        else
        {
            // No clips assigned at all
            audioSourceMenu.volume = 0f;
            audioSourceMain.volume = 0f;
            audioSourceMenu.Stop();
            audioSourceMain.Stop();
        }
    }

    private void Update()
    {
        if (mainMenuTheme == null && mainThemeSong == null) return; // No music assigned

        // Get spectrum data from each AudioSource
        audioSourceMenu.GetSpectrumData(spectrumDataMenu, 0, FFTWindow.BlackmanHarris);
        audioSourceMain.GetSpectrumData(spectrumDataMain, 0, FFTWindow.BlackmanHarris);

        // Calculate freq per bin
        float fSampleRate = AudioSettings.outputSampleRate;
        float freqPerBin = fSampleRate / 2f / fftSampleSize;

        float bassSum = 0f;
        // Sum up bins in [lowFreq, highFreq] from BOTH sources
        for (int i = 0; i < fftSampleSize; i++)
        {
            float freq = i * freqPerBin;
            if (freq >= lowFreq && freq <= highFreq)
            {
                bassSum += (spectrumDataMenu[i] + spectrumDataMain[i]);
            }
            else if (freq > highFreq)
            {
                break;
            }
        }

        // Send to EmissionPulseManager if it exists
        if (EmissionPulseManager.Instance != null)
        {
            EmissionPulseManager.Instance.SetMusicBassValue(bassSum);
        }
    }

    // -------------------------
    // Public Methods
    // -------------------------

    public void PlayMainMenuThemeDesktop()
    {
        if (mainMenuTheme == null) return;
        StopCrossFade();

        audioSourceMenu.clip = mainMenuTheme;
        audioSourceMenu.time = mainMenuThemeStartTimeDesktop;
        audioSourceMenu.volume = 1f;
        audioSourceMenu.Play();

        audioSourceMain.Stop();
        audioSourceMain.volume = 0f;
    }

    public void PlayMainMenuThemeFromMenu()
    {
        if (mainMenuTheme == null) return;
        StopCrossFade();

        audioSourceMenu.clip = mainMenuTheme;
        audioSourceMenu.time = mainMenuThemeStartTimeMenu;
        audioSourceMenu.volume = 1f;
        audioSourceMenu.Play();

        audioSourceMain.Stop();
        audioSourceMain.volume = 0f;
    }

    public void PlayMainThemeFromDesktop()
    {
        if (mainThemeSong == null) return;
        StopCrossFade();

        audioSourceMain.clip = mainThemeSong;
        audioSourceMain.time = mainThemeSongStartTimeDesktop;
        audioSourceMain.volume = 1f;
        audioSourceMain.Play();

        audioSourceMenu.Stop();
        audioSourceMenu.volume = 0f;
    }

    public void PlayMainThemeFromMenu()
    {
        if (mainThemeSong == null) return;
        StopCrossFade();

        audioSourceMain.clip = mainThemeSong;
        audioSourceMain.time = mainThemeSongStartTimeMenu;
        audioSourceMain.volume = 1f;
        audioSourceMain.Play();

        audioSourceMenu.Stop();
        audioSourceMenu.volume = 0f;
    }

    public void CrossFadeToMainTheme(float duration)
    {
        if (mainThemeSong == null) return;

        StopAllCoroutines();
        StartCoroutine(CrossFadeWithQuietGap(duration, toMainTheme: true));
    }

    public void CrossFadeToMenuTheme(float duration)
    {
        if (mainMenuTheme == null) return;

        StopAllCoroutines();
        StartCoroutine(CrossFadeWithQuietGap(duration, toMainTheme: false));
    }

    // -------------------------
    // Internal Fading Logic
    // -------------------------
    private System.Collections.IEnumerator CrossFadeWithQuietGap(float duration, bool toMainTheme)
    {
        isCrossFading = true;
        crossFadeElapsed = 0f;

        // STEP 1: Fade Out the currently-playing track
        float startVolumeMenu = audioSourceMenu.volume;
        float startVolumeMain = audioSourceMain.volume;

        while (crossFadeElapsed < duration)
        {
            crossFadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(crossFadeElapsed / duration);

            if (toMainTheme)
            {
                // Fade out menu from startVolumeMenu to 0
                audioSourceMenu.volume = Mathf.Lerp(startVolumeMenu, 0f, t);
            }
            else
            {
                // Fade out main from startVolumeMain to 0
                audioSourceMain.volume = Mathf.Lerp(startVolumeMain, 0f, t);
            }
            yield return null;
        }

        // Ensure volumes are zero
        if (toMainTheme)
        {
            audioSourceMenu.volume = 0f;
            audioSourceMenu.Stop();
        }
        else
        {
            audioSourceMain.volume = 0f;
            audioSourceMain.Stop();
        }

        // STEP 2: Optional silence gap
        if (transitionPause > 0f)
        {
            yield return new WaitForSeconds(transitionPause);
        }

        // STEP 3: Fade In the next track
        crossFadeElapsed = 0f;
        if (toMainTheme)
        {
            if (audioSourceMain.clip != mainThemeSong)
            {
                audioSourceMain.clip = mainThemeSong;
                audioSourceMain.time = mainThemeSongStartTimeMenu;
                audioSourceMain.Play();
            }
            audioSourceMain.volume = 0f;
        }
        else
        {
            if (audioSourceMenu.clip != mainMenuTheme)
            {
                audioSourceMenu.clip = mainMenuTheme;
                audioSourceMenu.time = mainMenuThemeStartTimeMenu;
                audioSourceMenu.Play();
            }
            audioSourceMenu.volume = 0f;
        }

        while (crossFadeElapsed < duration)
        {
            crossFadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(crossFadeElapsed / duration);

            if (toMainTheme)
            {
                audioSourceMain.volume = Mathf.Lerp(0f, 1f, t);
            }
            else
            {
                audioSourceMenu.volume = Mathf.Lerp(0f, 1f, t);
            }
            yield return null;
        }

        if (toMainTheme)
        {
            audioSourceMain.volume = 1f;
        }
        else
        {
            audioSourceMenu.volume = 1f;
        }

        isCrossFading = false;
    }

    private void StopCrossFade()
    {
        StopAllCoroutines();
        isCrossFading = false;
        crossFadeElapsed = 0f;
    }
}
