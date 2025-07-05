using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering.PostProcessing;
using System.Runtime.InteropServices;

public class SettingsManager : MonoBehaviour
{
    public enum SettingsCaller
    {
        StartMenu,
        PauseMenu
    }

    [Header("Caller Info")]
    [Tooltip("Tracks who opened the settings panel.")]
    public SettingsCaller currentCaller = SettingsCaller.StartMenu;
    [Tooltip("Reference to the Start Menu panel GameObject (to show when returning from settings).")]
    public GameObject startMenuPanel;
    [Tooltip("Reference to the Pause Menu canvas GameObject (to show when returning from settings).")]
    public GameObject pauseMenuCanvas;

    [Header("Settings Panel Root")]
    [Tooltip("Reference to the root GameObject of the settings panel (typically the same GameObject this script is attached to).")]
    public GameObject settingsPanelRoot;

    [Header("FPS Display")]
    public Toggle fpsToggle;
    public GameObject fpsCounter;
    public TextMeshProUGUI fpsCountTMP;

    [Header("Graphic Settings")]
    public TMP_Dropdown fpsDropdown;
    public Toggle vSyncToggle;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown fullscreenModeDropdown;

    [Header("Volume Sliders")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    [Header("Post Processing")]
    public Toggle bloomToggle;
    public PostProcessVolume postProcessVolume;
    public float bloomOnIntensity = 17f;
    public float bloomOffIntensity = 10f;

    [Header("Colorblind Mode")]
    public TMP_Dropdown colorblindDropdown; // Dropdown for selecting colorblind mode

    [Header("Screen Shake")]
    [Tooltip("Toggle for enabling/disabling screen shake.")]
    public Toggle screenShakeToggle;

    [Header("Save Button")]
    public Button saveButton;

    [Header("UI Buttons")]
    // Note: The settingsButton is kept if you want to use it,
    // but now the return button is renamed to be generic.
    public Button settingsButton;
    public Button returnButton; // formerly "returnToStartMenuButton"

    [Header("FPS Update Settings")]
    public float fpsUpdateInterval = 0.5f;
    private float fpsTimer = 0f;

    private Resolution[] availableResolutions;
    private readonly int[] possibleFpsValues = { 30, 60, 90, 120, 144, 165, 240, -1 };

    // Store the original screen shake intensity from WeaponManager at build time.
    private float defaultScreenShakeIntensity = 0f;

    public PhoneUIManager phoneUIManager;

    private void Awake()
    {
        if(phoneUIManager.isMobile)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Screen.currentResolution.refreshRate;
        }
    }

    private void Start()
    {
        // Ensure the settings panel is inactive initially.
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(false);

        // Cache the original screen shake intensity from the WeaponManager.
        if (WeaponManager.Instance != null)
        {
            defaultScreenShakeIntensity = WeaponManager.Instance.cameraShakeIntensity;
        }

        if(!phoneUIManager.isMobile)
        {
            PopulateResolutionDropdown();
            PopulateFullscreenModeDropdown();
            PopulateFpsDropdown();
            PopulateColorblindDropdown();
        }

        if (fpsToggle) fpsToggle.onValueChanged.AddListener(OnFpsToggleChanged);
        if (vSyncToggle) vSyncToggle.onValueChanged.AddListener(OnVsyncToggleChanged);
        if (masterVolumeSlider) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        if (bloomToggle) bloomToggle.onValueChanged.AddListener(OnBloomToggleChanged);
        if (screenShakeToggle) screenShakeToggle.onValueChanged.AddListener(OnScreenShakeToggleChanged);

        if (saveButton) saveButton.onClick.AddListener(OnSaveButtonClicked);
        if (settingsButton) settingsButton.onClick.AddListener(() => { PopulateResolutionDropdown(); });
        if (returnButton) returnButton.onClick.AddListener(OnReturnButtonClicked);

        var sdm = SaveDataManager.Instance;
        if (sdm != null && !sdm.fileExisted)
        {
            SetDefaultSettings(sdm);
        }
        LoadSettingsFromSaveData();
        ApplyAllSettingsImmediate();
    }

    private void Update()
    {
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= fpsUpdateInterval)
        {
            fpsTimer = 0f;
            if (fpsCountTMP != null && fpsCounter != null && fpsCounter.activeSelf)
            {
                float currentFps = 1f / Time.unscaledDeltaTime;
                fpsCountTMP.text = Mathf.RoundToInt(currentFps).ToString();
            }
        }
    }

    // --- New Public Methods for Opening Settings ---
    public void OpenSettingsFromStartMenu()
    {
        currentCaller = SettingsCaller.StartMenu;
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(true);
        if (startMenuPanel != null)
            startMenuPanel.SetActive(false);
    }

    public void OpenSettingsFromPauseMenu()
    {
        currentCaller = SettingsCaller.PauseMenu;
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(true);
        if (pauseMenuCanvas != null)
            pauseMenuCanvas.SetActive(false);
    }

    // --- Updated Return Button Behavior ---
    public void OnReturnButtonClicked()
    {
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(false);
        // Depending on who opened settings, re-show that menu.
        if (currentCaller == SettingsCaller.StartMenu)
        {
            if (startMenuPanel != null)
                startMenuPanel.SetActive(true);
        }
        else if (currentCaller == SettingsCaller.PauseMenu)
        {
            if (pauseMenuCanvas != null)
                pauseMenuCanvas.SetActive(true);
        }
    }

    private void SetDefaultSettings(SaveDataManager sdm)
    {
        if (sdm == null) return;

        availableResolutions = Screen.resolutions;
        Resolution currentRes = Screen.currentResolution;
        int defaultResIndex = 0;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            if (availableResolutions[i].width == currentRes.width &&
                availableResolutions[i].height == currentRes.height &&
                GetActualRefreshRate(availableResolutions[i]) == GetActualRefreshRate(currentRes))
            {
                defaultResIndex = i;
                break;
            }
        }
        sdm.persistentData.fullscreenModeIndex = 1;
        sdm.persistentData.resolutionIndex = defaultResIndex;
        sdm.persistentData.masterVolume = 0.5f;
        sdm.persistentData.musicVolume = 0.8f;
        sdm.persistentData.sfxVolume = 1f;
        sdm.persistentData.bloomOn = true;
        sdm.persistentData.showFps = false;
        sdm.persistentData.vsyncOn = false;
        sdm.persistentData.fpsIndex = 1;
        sdm.persistentData.colorblindModeIndex = 0;
        sdm.persistentData.screenShakeOn = true;
        sdm.SaveGame();
    }

    private void LoadSettingsFromSaveData()
    {
        var sdm = SaveDataManager.Instance;
        if (sdm == null) return;

        bool showFps = sdm.persistentData.showFps;
        bool vsyncOn = sdm.persistentData.vsyncOn;
        float masterVol = sdm.persistentData.masterVolume;
        float musicVol = sdm.persistentData.musicVolume;
        float sfxVol = sdm.persistentData.sfxVolume;
        bool bloomOn = sdm.persistentData.bloomOn;
        int resIndex = sdm.persistentData.resolutionIndex;
        int fsModeIndex = sdm.persistentData.fullscreenModeIndex;
        int fpsIndex = sdm.persistentData.fpsIndex;
        int colorblindModeIndex = sdm.persistentData.colorblindModeIndex;
        bool screenShakeOn = sdm.persistentData.screenShakeOn;

        if (fpsToggle) fpsToggle.isOn = showFps;
        if (vSyncToggle) vSyncToggle.isOn = vsyncOn;
        if (masterVolumeSlider) masterVolumeSlider.value = masterVol;
        if (musicVolumeSlider) musicVolumeSlider.value = musicVol;
        if (sfxVolumeSlider) sfxVolumeSlider.value = sfxVol;
        if (bloomToggle) bloomToggle.isOn = bloomOn;
        if (screenShakeToggle) screenShakeToggle.isOn = screenShakeOn;

        if (resolutionDropdown && resolutionDropdown.options.Count > 0)
        {
            resolutionDropdown.value = Mathf.Clamp(resIndex, 0, resolutionDropdown.options.Count - 1);
            resolutionDropdown.RefreshShownValue();
        }
        if (fullscreenModeDropdown && fullscreenModeDropdown.options.Count > 0)
        {
            fullscreenModeDropdown.value = Mathf.Clamp(fsModeIndex, 0, fullscreenModeDropdown.options.Count - 1);
            fullscreenModeDropdown.RefreshShownValue();
        }
        if (fpsDropdown && fpsDropdown.options.Count > 0)
        {
            fpsDropdown.value = Mathf.Clamp(fpsIndex, 0, fpsDropdown.options.Count - 1);
            fpsDropdown.RefreshShownValue();
        }
        if (colorblindDropdown && colorblindDropdown.options.Count > 0)
        {
            colorblindDropdown.value = Mathf.Clamp(colorblindModeIndex, 0, colorblindDropdown.options.Count - 1);
            colorblindDropdown.RefreshShownValue();
        }
    }

    private void ApplyAllSettingsImmediate()
    {
        if (fpsToggle) OnFpsToggleChanged(fpsToggle.isOn);
        if (vSyncToggle) OnVsyncToggleChanged(vSyncToggle.isOn);
        if (masterVolumeSlider) OnMasterVolumeChanged(masterVolumeSlider.value);
        if (musicVolumeSlider) OnMusicVolumeChanged(musicVolumeSlider.value);
        if (sfxVolumeSlider) OnSfxVolumeChanged(sfxVolumeSlider.value);
        if (bloomToggle) OnBloomToggleChanged(bloomToggle.isOn);
        if (screenShakeToggle) OnScreenShakeToggleChanged(screenShakeToggle.isOn);
        if (resolutionDropdown) OnResolutionChanged(resolutionDropdown.value);
        if (fullscreenModeDropdown) OnFullscreenModeChanged(fullscreenModeDropdown.value);
        if (fpsDropdown) OnFpsDropdownChanged(fpsDropdown.value);

        // Update the ColorBlindFilter with the selected mode.
        if (colorblindDropdown)
        {
            Debug.Log($"[APPLY] dropdown value = {colorblindDropdown.value}");
            ColorBlindMode mode = (ColorBlindMode)colorblindDropdown.value;
            if (ColorBlindFilter.Instance == null)
            {
                Debug.Log("[APPLY] ColorBlindFilter.Instance is NULL!");
            } else
            {
                ColorBlindFilter.Instance.SetMode(mode);
            }
        }
    }

    private void PopulateResolutionDropdown()
    {
        if (!resolutionDropdown) return;
        resolutionDropdown.ClearOptions();
        availableResolutions = Screen.resolutions;
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;
        Resolution currentRes = Screen.currentResolution;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution res = availableResolutions[i];
            string optionStr = $"{res.width} x {res.height} @{GetActualRefreshRate(res)}Hz";
            options.Add(optionStr);
            if (res.width == currentRes.width && res.height == currentRes.height && GetActualRefreshRate(res) == GetActualRefreshRate(currentRes))
                currentResolutionIndex = i;
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void PopulateFullscreenModeDropdown()
    {
        if (!fullscreenModeDropdown) return;
        fullscreenModeDropdown.ClearOptions();
        List<string> modeOptions = new List<string>()
        {
            "Fullscreen (Exclusive)",
            "Windowed Borderless",
            "Windowed",
            "Maximized Window"
        };
        fullscreenModeDropdown.AddOptions(modeOptions);
        fullscreenModeDropdown.value = 1;
        fullscreenModeDropdown.RefreshShownValue();
    }

    private void PopulateFpsDropdown()
    {
        if (!fpsDropdown) return;
        fpsDropdown.ClearOptions();
        List<string> fpsOptions = new List<string>();
        foreach (var fps in possibleFpsValues)
        {
            fpsOptions.Add(fps <= 0 ? "Unlimited" : fps.ToString());
        }
        fpsDropdown.AddOptions(fpsOptions);
        fpsDropdown.value = 1;
        fpsDropdown.RefreshShownValue();
    }

    private void PopulateColorblindDropdown()
    {
        if (!colorblindDropdown) return;
        colorblindDropdown.ClearOptions();
        List<string> options = new List<string>()
        {
            "Normal",
            "Protanopia",
            "Protanomaly",
            "Deuteranopia",
            "Deuteranomaly",
            "Tritanopia",
            "Tritanomaly",
            "Achromatopsia",
            "Achromatomaly"
        };
        colorblindDropdown.AddOptions(options);
        colorblindDropdown.value = 0;
        colorblindDropdown.RefreshShownValue();
    }

    private void OnFpsToggleChanged(bool isOn)
    {
        if (fpsCounter) fpsCounter.SetActive(isOn);
    }

    private void OnVsyncToggleChanged(bool isOn)
    {
        if (phoneUIManager.isMobile) return;
        QualitySettings.vSyncCount = isOn ? 1 : 0;
        if (isOn) Application.targetFrameRate = -1;
    }

    private void OnMasterVolumeChanged(float val)
    {
        if (audioMixer) audioMixer.SetFloat("MasterVolume", ConvertToDecibels(val));
    }

    private void OnMusicVolumeChanged(float val)
    {
        if (audioMixer) audioMixer.SetFloat("MusicVolume", ConvertToDecibels(val));
    }

    public void ReapplyMusicVolume()
    {
        OnMusicVolumeChanged(musicVolumeSlider.value);
    }

    private void OnSfxVolumeChanged(float val)
    {
        if (audioMixer) audioMixer.SetFloat("SfxVolume", ConvertToDecibels(val));
    }

    private void OnBloomToggleChanged(bool isOn)
    {
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            if (!postProcessVolume.profile.TryGetSettings<UnityEngine.Rendering.PostProcessing.Bloom>(out var bloom))
                bloom = postProcessVolume.profile.AddSettings<UnityEngine.Rendering.PostProcessing.Bloom>();
            bloom.intensity.value = isOn ? bloomOnIntensity : bloomOffIntensity;
        }
    }

    private void OnScreenShakeToggleChanged(bool isOn)
    {
        if (WeaponManager.Instance != null)
        {
            // If screen shake is enabled, restore the original intensity;
            // if disabled, set the intensity to zero.
            WeaponManager.Instance.cameraShakeIntensity = isOn ? defaultScreenShakeIntensity : 0f;
        }
    }

    private void OnResolutionChanged(int index)
    {
        if (phoneUIManager.isMobile) return;
        if (index < 0 || index >= availableResolutions.Length) return;
        Resolution r = availableResolutions[index];
        FullScreenMode fsMode = FullScreenMode.ExclusiveFullScreen;
        if (fullscreenModeDropdown)
            fsMode = GetFullScreenModeFromIndex(fullscreenModeDropdown.value);
        Screen.SetResolution(r.width, r.height, fsMode, GetActualRefreshRate(r));
    }

    private void OnFullscreenModeChanged(int index)
    {
        if (phoneUIManager.isMobile) return;
        FullScreenMode fsMode = GetFullScreenModeFromIndex(index);
        if (resolutionDropdown)
        {
            int resIdx = resolutionDropdown.value;
            if (resIdx >= 0 && resIdx < availableResolutions.Length)
            {
                Resolution r = availableResolutions[resIdx];
                Screen.SetResolution(r.width, r.height, fsMode, GetActualRefreshRate(r));
            }
        }
    }

    private FullScreenMode GetFullScreenModeFromIndex(int i)
    {
        switch (i)
        {
            case 0: return FullScreenMode.ExclusiveFullScreen;
            case 1: return FullScreenMode.FullScreenWindow;
            case 2: return FullScreenMode.Windowed;
            case 3: return FullScreenMode.MaximizedWindow;
        }
        return FullScreenMode.FullScreenWindow;
    }

    private void OnFpsDropdownChanged(int index)
    {
        if (phoneUIManager.isMobile) return;
        if (vSyncToggle && vSyncToggle.isOn) return;
        if (index < 0 || index >= possibleFpsValues.Length) return;
        int fpsVal = possibleFpsValues[index];
        Application.targetFrameRate = fpsVal <= 0 ? 9999 : fpsVal;
    }

    private float ConvertToDecibels(float sliderValue)
    {
        return sliderValue < 0.001f ? -80f : 20f * Mathf.Log10(sliderValue);
    }

    private int GetActualRefreshRate(Resolution r)
    {
        return r.refreshRate;
    }

    private void OnSaveButtonClicked()
    {
        Debug.Log($"[SAVE‑CLICK] {name}  caller={currentCaller}");
        ApplyAllSettingsImmediate();
        var sdm = SaveDataManager.Instance;
        if (sdm != null)
        {
            sdm.SaveSettingsToPermanentData(
                fpsToggle ? fpsToggle.isOn : false,
                vSyncToggle ? vSyncToggle.isOn : false,
                masterVolumeSlider ? masterVolumeSlider.value : 1f,
                musicVolumeSlider ? musicVolumeSlider.value : 1f,
                sfxVolumeSlider ? sfxVolumeSlider.value : 1f,
                bloomToggle ? bloomToggle.isOn : false,
                resolutionDropdown ? resolutionDropdown.value : 0,
                fullscreenModeDropdown ? fullscreenModeDropdown.value : 1,
                fpsDropdown ? fpsDropdown.value : 1,
                colorblindDropdown ? colorblindDropdown.value : 0,
                screenShakeToggle ? screenShakeToggle.isOn : true  // New parameter for screen shake
            );
        }
        Debug.Log("[SettingsManager] All changes applied & saved.");
    }
}
