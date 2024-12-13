using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiSettingsMenu : MenuElement
{
    public Button cancelChangesButton;
    public Button saveSettingsButton;

    public Toggle cheatModeToggle;
    public Toggle fastModeToggle;
    public Toggle displayBadgeToggle;
    public Toggle rotateCameraToggle;
    
    public event Action OnCancelChangesButton;
    public event Action OnSaveSettingsButton;
    
    bool settingsChanged;

    void Start()
    {
        cancelChangesButton.onClick.AddListener(HandleCancelChangesButton);
        saveSettingsButton.onClick.AddListener(HandleSaveSettingsButton);
    }

    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        cancelChangesButton.interactable = show;
        saveSettingsButton.interactable = show;
        bool cheatMode = PlayerPrefs.GetInt("CHEATMODE") == 1;
        bool fastMode = PlayerPrefs.GetInt("FASTMODE") == 1;
        bool displayBadge = PlayerPrefs.GetInt("DISPLAYBADGE") == 1;
        bool rotateCamera = PlayerPrefs.GetInt("ROTATECAMERA") == 1;
        cheatModeToggle.isOn = cheatMode;
        fastModeToggle.isOn = fastMode;
        displayBadgeToggle.isOn = displayBadge;
        rotateCameraToggle.isOn = rotateCamera;
    }
    
    void HandleCancelChangesButton()
    {
        OnCancelChangesButton?.Invoke();
    }

    void HandleSaveSettingsButton()
    {
        SettingsManager settingsManager = GameManager.instance.settingsManager;
        settingsManager.SetCheatMode(cheatModeToggle.isOn);
        settingsManager.SetFastMode(fastModeToggle.isOn);
        settingsManager.SetDisplayBadge(displayBadgeToggle.isOn);
        settingsManager.SetRotateCamera(rotateCameraToggle.isOn);
        OnSaveSettingsButton?.Invoke();
    }
}
