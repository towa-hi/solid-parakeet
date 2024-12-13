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

    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        connectButton.interactable = enable;
        offlineButton.interactable = enable;
    }
    
    void HandleConnectButton()
    {
        GameManager.instance.audioManager.PlayButtonClick();
        OnConnectButton?.Invoke();
    }

    void HandleOfflineButton()
    {
        GameManager.instance.audioManager.PlayButtonClick();
        OnOfflineButton?.Invoke();
    }
}
