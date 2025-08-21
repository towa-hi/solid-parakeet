using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModalSettings : ModalElement
{
    public Slider masterVolumeSlider;
    public TextMeshProUGUI masterVolumeSliderLabel;
    public Slider musicVolumeSlider;
    public TextMeshProUGUI musicVolumeSliderLabel;
    public Slider effectsVolumeSlider;
    public TextMeshProUGUI effectsVolumeSliderLabel;
    public Button cheatModeButton;
    public TextMeshProUGUI cheatModeButtonText;
    public Button fastModeButton;
    public TextMeshProUGUI fastModeButtonText;
    public Button displayBadgesButton;
    public TextMeshProUGUI displayBadgesButtonText;
    public Button moveCameraButton;
    public TextMeshProUGUI moveCameraButtonText;
    Settings oldSettings;
    Settings newSettings;
    public Button backButton;
    public Button submitButton;

    public Action OnBackButton;
    public Action OnSubmitButton;
    
    struct Settings
    {
        public int cheatMode;
        public int fastMode;
        public int displayBadges;
        public int moveCamera;
        public int masterVolume;
        public int musicVolume;
        public int effectsVolume;
    }

    void Start()
    {
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        effectsVolumeSlider.onValueChanged.AddListener(OnEffectsVolumeSliderChanged);
        cheatModeButton.onClick.AddListener(OnCheatModeButton);
        fastModeButton.onClick.AddListener(OnFastModeButton);
        displayBadgesButton.onClick.AddListener(OnDisplayBadgesButton);
        moveCameraButton.onClick.AddListener(OnMoveCameraButton);
        backButton.onClick.AddListener(HandleBackButton);
        submitButton.onClick.AddListener(HandleSubmitButton);
    }
    
    public override void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
        if (focused)
        {
            oldSettings = LoadCurrentSettings();
            newSettings = oldSettings;
            Refresh(oldSettings);
            
        }
    }
    void OnEnable()
    {
        SettingsManager.OnSettingChanged += OnSettingChanged;
    }

    void OnDisable()
    {
        SettingsManager.OnSettingChanged -= OnSettingChanged;
    }

    void OnSettingChanged(SettingsKey key, int val)
    {
        if (!gameObject.activeInHierarchy) return;
        
        oldSettings = LoadCurrentSettings();
        Refresh(oldSettings);
    }
    
    Settings LoadCurrentSettings()
    {
        return new Settings
        {
            cheatMode = SettingsManager.GetPref(SettingsKey.CHEATMODE),
            fastMode = SettingsManager.GetPref(SettingsKey.FASTMODE),
            displayBadges = SettingsManager.GetPref(SettingsKey.DISPLAYBADGES),
            moveCamera = SettingsManager.GetPref(SettingsKey.MOVECAMERA),
            masterVolume = SettingsManager.GetPref(SettingsKey.MASTERVOLUME),
            musicVolume = SettingsManager.GetPref(SettingsKey.MUSICVOLUME),
            effectsVolume = SettingsManager.GetPref(SettingsKey.EFFECTSVOLUME),
        };
    }

    void Refresh(Settings settings)
    {
        cheatModeButtonText.text = settings.cheatMode == 1 ? "Enabled" : "Disabled";
        fastModeButtonText.text = settings.fastMode == 1 ? "Enabled" : "Disabled";
        displayBadgesButtonText.text = settings.displayBadges == 1 ? "Enabled" : "Disabled";
        moveCameraButtonText.text = settings.moveCamera == 1 ? "Enabled" : "Disabled";
        
        // Set slider values without triggering events
        masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
        musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeSliderChanged);
        effectsVolumeSlider.onValueChanged.RemoveListener(OnEffectsVolumeSliderChanged);
        
        masterVolumeSlider.value = settings.masterVolume / 100f;
        musicVolumeSlider.value = settings.musicVolume / 100f;
        effectsVolumeSlider.value = settings.effectsVolume / 100f;
        
        masterVolumeSliderLabel.text = $"Master Volume: {settings.masterVolume}";
        musicVolumeSliderLabel.text = $"Music Volume: {settings.musicVolume}";
        effectsVolumeSliderLabel.text = $"Effects Volume: {settings.effectsVolume}";
        
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        effectsVolumeSlider.onValueChanged.AddListener(OnEffectsVolumeSliderChanged);
    }

    void OnCheatModeButton()
    {
        newSettings.cheatMode = newSettings.cheatMode == 0 ? 1 : 0;
        AudioManager.PlaySmallButtonClick();
        Refresh(newSettings);
    }

    void OnFastModeButton()
    {
        newSettings.fastMode = newSettings.fastMode == 0 ? 1 : 0;
        AudioManager.PlaySmallButtonClick();
        Refresh(newSettings);
    }

    void OnDisplayBadgesButton()
    {
        newSettings.displayBadges = newSettings.displayBadges == 0 ? 1 : 0;
        AudioManager.PlaySmallButtonClick();
        Refresh(newSettings);
    }

    void OnMoveCameraButton()
    {
        newSettings.moveCamera = newSettings.moveCamera == 0 ? 1 : 0;
        AudioManager.PlaySmallButtonClick();
        Refresh(newSettings);
    }
    
    void OnMasterVolumeSliderChanged(float val)
    {
        newSettings.masterVolume = Mathf.RoundToInt(val * 100f);
        masterVolumeSliderLabel.text = $"Master Volume: {newSettings.masterVolume}";
    }

    void OnMusicVolumeSliderChanged(float val)
    {
        newSettings.musicVolume = Mathf.RoundToInt(val * 100f);
        musicVolumeSliderLabel.text = $"Music Volume: {newSettings.musicVolume}";
    }

    void OnEffectsVolumeSliderChanged(float val)
    {
        newSettings.effectsVolume = Mathf.RoundToInt(val * 100f);
        effectsVolumeSliderLabel.text = $"Effects Volume: {newSettings.effectsVolume}";
    }

    void HandleBackButton()
    {
        AudioManager.PlayMidButtonClick();
        OnBackButton?.Invoke();
    }

    void HandleSubmitButton()
    {
        Dictionary<SettingsKey, int> changedSettings = new();
        
        if (newSettings.cheatMode != oldSettings.cheatMode)
            changedSettings.Add(SettingsKey.CHEATMODE, newSettings.cheatMode);
        
        if (newSettings.fastMode != oldSettings.fastMode)
            changedSettings.Add(SettingsKey.FASTMODE, newSettings.fastMode);
        
        if (newSettings.displayBadges != oldSettings.displayBadges)
            changedSettings.Add(SettingsKey.DISPLAYBADGES, newSettings.displayBadges);
        
        if (newSettings.moveCamera != oldSettings.moveCamera)
            changedSettings.Add(SettingsKey.MOVECAMERA, newSettings.moveCamera);
        
        if (newSettings.masterVolume != oldSettings.masterVolume)
            changedSettings.Add(SettingsKey.MASTERVOLUME, newSettings.masterVolume);
        
        if (newSettings.musicVolume != oldSettings.musicVolume)
            changedSettings.Add(SettingsKey.MUSICVOLUME, newSettings.musicVolume);
        
        if (newSettings.effectsVolume != oldSettings.effectsVolume)
            changedSettings.Add(SettingsKey.EFFECTSVOLUME, newSettings.effectsVolume);
        
        if (changedSettings.Count > 0)
        {
            SettingsManager.SetPrefs(changedSettings);
            oldSettings = newSettings;
        }
        AudioManager.PlayMidButtonClick();
        OnSubmitButton?.Invoke();
    }

}