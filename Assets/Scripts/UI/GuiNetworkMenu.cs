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
    public Button secondTestButton;
    public Button backButton;

    public TextMeshProUGUI connectionStatusText;
    public TextMeshProUGUI userNameText;
    public TextMeshProUGUI userIdText;
    public TextMeshProUGUI userGamesPlayedText;
    public TextMeshProUGUI userCurrentLobbyText;
    
    public event Action OnConnectWalletButton;
    public event Action OnTestButton;
    public event Action OnBackButton;

    void Start()
    {
        connectWalletButton.onClick.AddListener(HandleConnectWalletButton);
        testButton.onClick.AddListener(HandleTestButton);
        secondTestButton.onClick.AddListener(HandleSecondTestButton);
        backButton.onClick.AddListener(HandleBackButton);
        GameManager.instance.stellarManager.OnCurrentUserChanged += OnCurrentUserChangedEvent;
        
    }

    void OnWalletConnectedEvent(bool success)
    {
        string message = success ? "Connected" : "Disconnected";
        walletConnectedText.text = message;
    }

    void OnCurrentUserChangedEvent()
    {
        RUser? currentUser = GameManager.instance.stellarManager.currentUser;
        if (currentUser != null)
        {
            userNameText.text = currentUser.Value.name;
            userIdText.text = currentUser.Value.user_id;
            userGamesPlayedText.text = currentUser.Value.games_played.ToString();
            userCurrentLobbyText.text = currentUser.Value.current_lobby == "" ? "no lobby" : currentUser.Value.current_lobby;
        }
        else
        {
            userNameText.text = "no user";
            userIdText.text = "";
            userGamesPlayedText.text = "";
            userCurrentLobbyText.text = "";
        }
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
        _ = GameManager.instance.RunWithEvents<bool>(GameManager.instance.stellarManager.OnConnectWallet);
        OnConnectWalletButton?.Invoke();
    }

    void HandleTestButton()
    {
        _ = GameManager.instance.stellarManager.TestFunction();
        OnTestButton?.Invoke();
    }

    void HandleBackButton()
    {
        OnBackButton?.Invoke();
    }
    
    void HandleSecondTestButton()
    {
        _ = GameManager.instance.stellarManager.SecondTestFunction();
    }
}
