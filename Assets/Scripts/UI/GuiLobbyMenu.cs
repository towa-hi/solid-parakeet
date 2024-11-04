using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class GuiLobbyMenu : MenuElement
{
    public Button cancelButton;
    public Button readyButton;
    
    // chat is handled in it's own component
    
    public event Action OnCancelButton;
    public event Action OnReadyButton;

    void Start()
    {
        cancelButton.onClick.AddListener(HandleCancelButton);
        readyButton.onClick.AddListener(HandleReadyButton);
    }
    
    void HandleCancelButton()
    {
        OnCancelButton?.Invoke();
    }

    void HandleReadyButton()
    {
        OnReadyButton?.Invoke();
    }
}
