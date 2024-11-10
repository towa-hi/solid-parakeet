using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiStartMenu : MenuElement
{
    public Button connectButton;
    public Button offlineButton;

    public event Action OnConnectButton;
    public event Action OnOfflineButton;
    
    void Start()
    {
        connectButton.onClick.AddListener(HandleConnectButton);
        offlineButton.onClick.AddListener(HandleOfflineButton);
    }

    public override void EnableElement(bool enable)
    {
        connectButton.interactable = enable;
        offlineButton.interactable = enable;
    }
    
    void HandleConnectButton()
    {
        OnConnectButton?.Invoke();
    }

    void HandleOfflineButton()
    {
        OnOfflineButton?.Invoke();
    }
}
