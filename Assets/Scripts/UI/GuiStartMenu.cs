using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiStartMenu : MenuElement
{
    public Button startButton;
    public Button startOfflineButton;

    public Action OnStartButton; 
    public Action OnStartOfflineButton;
    
    void Start()
    {
        startButton.onClick.AddListener(() =>
        {
            AudioManager.PlayButtonClick();
            OnStartButton?.Invoke();
        });
        startOfflineButton.onClick.AddListener(() =>
        {
            AudioManager.PlayButtonClick();
            OnStartOfflineButton?.Invoke();
        });
    }

    public override void Refresh()
    {
        
    }
}

