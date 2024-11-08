using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbySetupMenu : MenuElement
{
    public Button cancelButton;
    public Button startButton;

    public event Action OnCancelButton;
    public event Action OnStartButton;
    
    void Start()
    {
        cancelButton.onClick.AddListener(HandleCancelButton);
        startButton.onClick.AddListener(HandleStartButton);
    }

    public override void EnableElement(bool enable)
    {
        cancelButton.interactable = enable;
        startButton.interactable = enable;
    }
    
    void HandleCancelButton()
    {
        OnCancelButton?.Invoke();
    }

    void HandleStartButton()
    {
        OnStartButton?.Invoke();
    }
}
