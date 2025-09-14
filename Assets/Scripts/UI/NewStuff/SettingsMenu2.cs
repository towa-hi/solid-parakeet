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
        return new WarmancerSettings
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
    public void HandleCheatModeToggle()
    {
        newSettings.cheatMode = newSettings.cheatMode == 0 ? 1 : 0;
        Refresh();
    }

    public void HandleFastModeToggle()
    {
        newSettings.fastMode = newSettings.fastMode == 0 ? 1 : 0;
        Refresh();
    }

    public void HandleDisplayBadgesToggle()
    {
        newSettings.displayBadges = newSettings.displayBadges == 0 ? 1 : 0;
        Refresh();
    }

    public void HandleMoveCameraToggle()
    {
        newSettings.moveCamera = newSettings.moveCamera == 0 ? 1 : 0;
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
        EmitAction(MenuAction.GotoMainMenu);
    }

    public void HandleSaveChanges()
    {
        EmitAction(MenuAction.SaveChanges, newSettings);
    }
    public override void Refresh()
    {
        cheatModeToggleButton.text.text = newSettings.cheatMode == 1 ? "Enabled" : "Disabled";
        fastModeToggleButton.text.text = newSettings.fastMode == 1 ? "Enabled" : "Disabled";
        displayBadgesToggleButton.text.text = newSettings.displayBadges == 1 ? "Enabled" : "Disabled";
        moveCameraToggleButton.text.text = newSettings.moveCamera == 1 ? "Enabled" : "Disabled";
        masterVolumeSlider.value = newSettings.masterVolume / 100f;
        musicVolumeSlider.value = newSettings.musicVolume / 100f;
        effectsVolumeSlider.value = newSettings.effectsVolume / 100f;
        masterVolumeSlider.SetValueWithoutNotify(newSettings.masterVolume / 100f);
        musicVolumeSlider.SetValueWithoutNotify(newSettings.musicVolume / 100f);
        effectsVolumeSlider.SetValueWithoutNotify(newSettings.effectsVolume / 100f);
        masterVolumeSliderLabel.text = $"Master Volume: {newSettings.masterVolume}";
        musicVolumeSliderLabel.text = $"Music Volume: {newSettings.musicVolume}";
        effectsVolumeSliderLabel.text = $"Effects Volume: {newSettings.effectsVolume}";

        saveChangesButton.interactable = newSettings.cheatMode != oldSettings.cheatMode || newSettings.fastMode != oldSettings.fastMode || newSettings.displayBadges != oldSettings.displayBadges || newSettings.moveCamera != oldSettings.moveCamera || newSettings.masterVolume != oldSettings.masterVolume || newSettings.musicVolume != oldSettings.musicVolume || newSettings.effectsVolume != oldSettings.effectsVolume;
    }
}



    struct WarmancerSettings
    {
        public int cheatMode;
        public int fastMode;
        public int displayBadges;
        public int moveCamera;
        public int masterVolume;
        public int musicVolume;
        public int effectsVolume;
    }