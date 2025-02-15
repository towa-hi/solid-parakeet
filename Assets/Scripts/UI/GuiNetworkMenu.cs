using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class GuiNetworkMenu : MenuElement
{
    public TextMeshProUGUI walletConnectedText;
    public Button connectWalletButton;
    public Button testButton;
    public Button backButton;
    
    public event Action OnConnectWalletButton;
    public event Action OnTestButton;
    public event Action OnBackButton;

    void Start()
    {
        connectWalletButton.onClick.AddListener(HandleConnectWalletButton);
        testButton.onClick.AddListener(HandleTestButton);
        backButton.onClick.AddListener(HandleBackButton);
        GameManager.instance.stellarManager.OnWalletConnected += OnWalletConnectedEvent;
    }

    void OnWalletConnectedEvent(bool success)
    {
        string message = success ? "Connected" : "Disconnected";
        walletConnectedText.text = message;
    }
    
    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        connectWalletButton.interactable = enable;
        testButton.interactable = enable;
        backButton.interactable = enable;
        
    }
    void HandleConnectWalletButton()
    {
        OnConnectWalletButton?.Invoke();
    }

    void HandleTestButton()
    {
        OnTestButton?.Invoke();
    }

    void HandleBackButton()
    {
        OnBackButton?.Invoke();
    }
}
