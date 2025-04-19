using System;
using Contract;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestStartMenu : TestGuiElement
{

    public TMP_InputField contractField;
    public Button setContractButton;
    public TextMeshProUGUI currentContractText;
    public TMP_InputField sneedField;
    public Button fillGuestSneedButton;
    public Button fillHostSneedButton;
    public Button setSneedButton;
    public TextMeshProUGUI currentSneedText;
    public TextMeshProUGUI currentAddressText;
    public TextMeshProUGUI currentLobbyText;
    public Button joinLobbyButton;
    public Button makeLobbyButton;
    public Button viewLobbyButton;
    public Button cancelButton;
    public Button walletButton;
    

    public event Action OnJoinLobbyButton;
    public event Action OnMakeLobbyButton;
    public event Action OnCancelButton;
    public event Action OnViewLobbyButton;
    public event Action OnWalletButton;

    
    void Start()
    {
        contractField.onValueChanged.AddListener(OnContractFieldChanged);
        setContractButton.onClick.AddListener(OnSetContract);
        sneedField.onValueChanged.AddListener(OnSneedFieldChanged);
        setSneedButton.onClick.AddListener(OnSetSneed);
        fillGuestSneedButton.onClick.AddListener(OnFillGuestSneed);
        fillHostSneedButton.onClick.AddListener(OnFillHostSneed);
        joinLobbyButton.onClick.AddListener(() => OnJoinLobbyButton?.Invoke());
        makeLobbyButton.onClick.AddListener(() => OnMakeLobbyButton?.Invoke());
        cancelButton.onClick.AddListener(() => OnCancelButton?.Invoke());
        viewLobbyButton.onClick.AddListener(() => OnViewLobbyButton?.Invoke());
        walletButton.onClick.AddListener(() => OnWalletButton?.Invoke());
        StellarManagerTest.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }
    
    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        if (isEnabled && networkUpdated)
        {
            sneedField.text = string.Empty;
            currentContractText.text = string.Empty;
            OnNetworkStateUpdated();
        }
    }
    
    void OnNetworkStateUpdated()
    {
        if (!isEnabled) return;
        Refresh();
    }
    
    void Refresh()
    {
        Debug.Log("Refresh");
        setContractButton.interactable = StellarManagerTest.GetContractAddress() != contractField.text && StrKey.IsValidContractId(contractField.text);
        setSneedButton.interactable = StellarManagerTest.stellar.sneed != sneedField.text && StrKey.IsValidEd25519SecretSeed(sneedField.text);
        currentContractText.text = StellarManagerTest.GetContractAddress();
        currentSneedText.text = StellarManagerTest.stellar.sneed;
        currentAddressText.text = StellarManagerTest.GetUserAddress();
        User? currentUser = StellarManagerTest.currentUser;
        joinLobbyButton.interactable = true;
        makeLobbyButton.interactable = true;
        viewLobbyButton.interactable = false;
        if (currentUser.HasValue)
        {
            User user = currentUser.Value;
            if (string.IsNullOrEmpty(user.current_lobby))
            {
                currentLobbyText.text = "No lobby";
                viewLobbyButton.interactable = false;
            }
            else
            {
                // user is in a lobby
                currentLobbyText.text = user.current_lobby;
                joinLobbyButton.interactable = false;
                makeLobbyButton.interactable = false;
                viewLobbyButton.interactable = true;
            }
        }
        else
        {
            currentLobbyText.text = "No user";
            viewLobbyButton.interactable = false;
        }
    }

    void OnContractFieldChanged(string input)
    {
        Refresh();
    }
    
    void OnSneedFieldChanged(string input)
    {
        Refresh();
    }

    void OnFillGuestSneed()
    {
        sneedField.text = StellarManagerTest.testGuestSneed;
    }

    void OnFillHostSneed()
    {
        sneedField.text = StellarManagerTest.testHostSneed;
    }
    
    void OnSetSneed()
    {
        _ = StellarManagerTest.SetSneed(sneedField.text);
    }
    
    void OnSetContract()
    {
        _ = StellarManagerTest.SetContractAddress(contractField.text);
    }
}
