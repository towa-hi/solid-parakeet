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
    public Button setSneedButton;
    public TextMeshProUGUI currentSneedText;
    public TextMeshProUGUI currentAddressText;
    public TextMeshProUGUI currentLobbyText;
    public Button joinLobbyButton;
    public Button makeLobbyButton;
    public Button viewLobbyButton;
    public Button cancelButton;

    public event Action<string> OnSetContractButton;
    public event Action<string> OnSetSneedButton;

    public event Action OnJoinLobbyButton;
    public event Action OnMakeLobbyButton;
    public event Action OnViewLobbyButton;
    public event Action OnCancelButton;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        contractField.onValueChanged.AddListener(OnContractFieldChanged);
        setContractButton.onClick.AddListener(() => OnSetContractButton?.Invoke(contractField.text));
        sneedField.onValueChanged.AddListener(OnSneedFieldChanged);
        setSneedButton.onClick.AddListener(() => OnSetSneedButton?.Invoke(sneedField.text));
        joinLobbyButton.onClick.AddListener(() => OnJoinLobbyButton?.Invoke());
        makeLobbyButton.onClick.AddListener(() => OnMakeLobbyButton?.Invoke());
        cancelButton.onClick.AddListener(() => OnCancelButton?.Invoke());
        viewLobbyButton.onClick.AddListener(() => OnViewLobbyButton?.Invoke());
        StellarManagerTest.OnContractAddressUpdated += OnContractAddressUpdated;
        StellarManagerTest.OnSneedUpdated += OnSneedUpdated;
        StellarManagerTest.OnCurrentUserUpdated += OnCurrentUserUpdated;
    }


    public override void Initialize()
    {
        contractField.text = string.Empty;
        sneedField.text = string.Empty;
        Refresh();
    }
    public override void Refresh()
    {
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
    
    void OnContractAddressUpdated(string contractId)
    {
        contractField.text = string.Empty;
        Refresh();
    }

    void OnSneedUpdated(string accountId)
    {
        sneedField.text = string.Empty;
        Refresh();
    }
    
    void OnCurrentUserUpdated(User? currentUser)
    {
        Refresh();
    }
}
