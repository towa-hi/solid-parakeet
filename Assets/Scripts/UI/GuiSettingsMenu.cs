using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiSettingsMenu : MenuElement
{
    public Button cancelChangesButton;
    public Button saveSettingsButton;

    public event Action OnCancelChangesButton;
    public event Action OnSaveSettingsButton;
    
    bool settingsChanged;

    void Start()
    {
        cancelChangesButton.onClick.AddListener(HandleCancelChangesButton);
        saveSettingsButton.onClick.AddListener(HandleSaveSettingsButton);
    }

    void HandleCancelChangesButton()
    {
        OnCancelChangesButton?.Invoke();
    }

    void HandleSaveSettingsButton()
    {
        OnSaveSettingsButton?.Invoke();
    }
}
