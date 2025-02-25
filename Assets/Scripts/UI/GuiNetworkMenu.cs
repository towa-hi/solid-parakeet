using System;
using ContractTypes;
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
    public Button oldContractButton;
    public Button newContractButton;
    public Button toggleFiltersButton;
    
    public Button backButton;

    public TextMeshProUGUI connectionStatusText;
    public TextMeshProUGUI userNameText;
    public TextMeshProUGUI userIdText;
    public TextMeshProUGUI userGamesPlayedText;
    public TextMeshProUGUI userCurrentLobbyText;
    public TextMeshProUGUI contractText;
    public TextMeshProUGUI toggleFiltersText;
    public TMP_InputField guestInputField;
    public TMP_InputField contractInputField;
    public event Action OnConnectWalletButton;
    public event Action OnTestButton;
    public event Action OnBackButton;

    void Start()
    {
        connectWalletButton.onClick.AddListener(HandleConnectWalletButton);
        testButton.onClick.AddListener(HandleTestButton);
        secondTestButton.onClick.AddListener(HandleSecondTestButton);
        newContractButton.onClick.AddListener(HandleNewContractButton);
        toggleFiltersButton.onClick.AddListener(HandleToggleFiltersButton);
        backButton.onClick.AddListener(HandleBackButton);
        GameManager.instance.stellarManager.OnCurrentUserChanged += OnCurrentUserChangedEvent;
        GameManager.instance.stellarManager.OnContractChanged += OnContractChangedEvent;
        OnContractChangedEvent(GameManager.instance.stellarManager.contract);
        
    }

    void OnWalletConnectedEvent(bool success)
    {
        string message = success ? "Connected" : "Disconnected";
        walletConnectedText.text = message;
    }

    void OnContractChangedEvent(string contract)
    {
        contractText.text = contract;
    }
    
    void OnCurrentUserChangedEvent()
    {
        User? currentUser = GameManager.instance.stellarManager.currentUser;
        Debug.Log("OnCurrentUserChangedEvent");
        if (currentUser != null)
        {
            Debug.Log("setting currentUser");
            userNameText.text = currentUser.Value.name;
            userIdText.text = currentUser.Value.user_id;
            userGamesPlayedText.text = currentUser.Value.games_played.ToString();
            userCurrentLobbyText.text = currentUser.Value.current_lobby == "" ? "no lobby" : currentUser.Value.current_lobby;
        }
        else
        {
            Debug.Log("clearing currentUser");
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
        _ = GameManager.instance.stellarManager.SecondTestFunction(guestInputField.text);
    }

    void HandleNewContractButton()
    {
        GameManager.instance.stellarManager.SetContract(contractInputField.text);
    }

    bool filterOn;
    void HandleToggleFiltersButton()
    {
        if (!filterOn)
        {
            filterOn = true;
            toggleFiltersText.text = "event filter enabled";
        }
        else
        {
            filterOn = false;
            toggleFiltersText.text = "event filter disabled";
        }
    }
}
