using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiStartMenu : MenuElement
{
    public Button startButton;

    public Action OnStartButton; 
    void Start()
    {
        startButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayButtonClick();
            OnStartButton?.Invoke();
        });
    }

    public override void Refresh()
    {
        
    }
}

