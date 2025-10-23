using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu2 : MenuBase
{
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider effectsVolumeSlider;
    public ButtonExtended cheatModeToggleButton;
    public ButtonExtended fastModeToggleButton;
    public ButtonExtended displayBadgesToggleButton;
    public ButtonExtended moveCameraToggleButton;

    public TextMeshProUGUI masterVolumeSliderLabel;
    public TextMeshProUGUI musicVolumeSliderLabel;
    public TextMeshProUGUI effectsVolumeSliderLabel;
    public Button backButton;
    public Button saveChangesButton;
    
    WarmancerSettings oldSettings;
    WarmancerSettings newSettings;
    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
        saveChangesButton.onClick.AddListener(HandleSaveChanges);
        cheatModeToggleButton.onClick.AddListener(HandleCheatModeToggle);
        fastModeToggleButton.onClick.AddListener(HandleFastModeToggle);
        displayBadgesToggleButton.onClick.AddListener(HandleDisplayBadgesToggle);
        moveCameraToggleButton.onClick.AddListener(HandleMoveCameraToggle);
        masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeSlider);
        musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeSlider);
        effectsVolumeSlider.onValueChanged.AddListener(HandleEffectsVolumeSlider);
        oldSettings = LoadCurrentSettings();
        newSettings = oldSettings;
        Refresh();
    }

    WarmancerSettings LoadCurrentSettings()
    {
        return SettingsManager.Load();
    }
    public void HandleCheatModeToggle()
    {
        newSettings.cheatMode = !newSettings.cheatMode;
        Refresh();
    }

    public void HandleFastModeToggle()
    {
        newSettings.fastMode = !newSettings.fastMode;
        Refresh();
    }

    public void HandleDisplayBadgesToggle()
    {
        newSettings.displayBadges = !newSettings.displayBadges;
        Refresh();
    }

    public void HandleMoveCameraToggle()
    {
        newSettings.moveCamera = !newSettings.moveCamera;
        Refresh();
    }

    public void HandleMasterVolumeSlider(float value)
    {
        newSettings.masterVolume = Mathf.RoundToInt(value * 100f);
        Refresh();
    }

    public void HandleMusicVolumeSlider(float value)
    {
        newSettings.musicVolume = Mathf.RoundToInt(value * 100f);
        Refresh();
    }

    public void HandleEffectsVolumeSlider(float value)
    {
        newSettings.effectsVolume = Mathf.RoundToInt(value * 100f);
        Refresh();
    }
    public void HandleBack()
    {
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }

    public void HandleSaveChanges()
    {
        // Persist and remain on page
        SettingsManager.Save(newSettings);
    }
    public override void Refresh()
    {
        cheatModeToggleButton.text.text = newSettings.cheatMode ? "Enabled" : "Disabled";
        fastModeToggleButton.text.text = newSettings.fastMode ? "Enabled" : "Disabled";
        displayBadgesToggleButton.text.text = newSettings.displayBadges ? "Enabled" : "Disabled";
        moveCameraToggleButton.text.text = newSettings.moveCamera ? "Enabled" : "Disabled";
        masterVolumeSlider.SetValueWithoutNotify(newSettings.masterVolume / 100f);
        musicVolumeSlider.SetValueWithoutNotify(newSettings.musicVolume / 100f);
        effectsVolumeSlider.SetValueWithoutNotify(newSettings.effectsVolume / 100f);
        masterVolumeSliderLabel.text = $"Master Volume:\n{newSettings.masterVolume}";
        musicVolumeSliderLabel.text = $"Music Volume:\n{newSettings.musicVolume}";
        effectsVolumeSliderLabel.text = $"Effects Volume:\n{newSettings.effectsVolume}";

        saveChangesButton.interactable = newSettings.cheatMode != oldSettings.cheatMode || newSettings.fastMode != oldSettings.fastMode || newSettings.displayBadges != oldSettings.displayBadges || newSettings.moveCamera != oldSettings.moveCamera || newSettings.masterVolume != oldSettings.masterVolume || newSettings.musicVolume != oldSettings.musicVolume || newSettings.effectsVolume != oldSettings.effectsVolume;
    }
}



