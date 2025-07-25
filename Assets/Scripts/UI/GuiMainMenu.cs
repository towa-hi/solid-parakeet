using System;
using System.Text;
using Contract;
using Stellar;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiMainMenu : MenuElement
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
    public TextMeshProUGUI assetsText;
    public Button assetsButton;
    
    public Button joinLobbyButton;
    public Button makeLobbyButton;
    public Button viewLobbyButton;
    public Button cancelButton;
    public Button walletButton;
    

    public event Action OnJoinLobbyButton;
    public event Action OnMakeLobbyButton;
    public event Action OnOptionsButton;
    public event Action OnViewLobbyButton;
    public event Action OnWalletButton;

    public event Action OnAssetButton;
    
    void Start()
    {
        contractField.onValueChanged.AddListener(OnContractFieldChanged);
        setContractButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnSetContract();
        });
        sneedField.onValueChanged.AddListener(OnSneedFieldChanged);
        setSneedButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnSetSneed();
        });
        fillGuestSneedButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnFillGuestSneed();
        });
        fillHostSneedButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnFillHostSneed();
        });
        joinLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnJoinLobbyButton?.Invoke();
        });
        makeLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnMakeLobbyButton?.Invoke();
        });
        cancelButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnOptionsButton?.Invoke();
        });
        viewLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnViewLobbyButton?.Invoke();
        });
        walletButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnWalletButton?.Invoke();
        });
        assetsButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnAssetButton?.Invoke();
        });
        StellarManager.OnAssetsUpdated += OnAssetsUpdated;
    }

    void OnAssetsUpdated(TrustLineEntry entry)
    {
        if (entry == null)
        {
            string message = $"No SCRY could be found for account {StellarManager.GetUserAddress()}";
            assetsText.text = message;
        }
        else if (entry.asset is TrustLineAsset.AssetTypeCreditAlphanum4 asset)
        {
            long balance = entry.balance.InnerValue;
            string assetCode = Encoding.ASCII.GetString(asset.alphaNum4.assetCode.InnerValue).TrimEnd('\0');
            string message = $"{assetCode}: balance: {balance}";
            assetsText.text = message;
        }
        Refresh();
    }
    
    public override void Refresh()
    {
        Debug.Log("Refresh");
        setContractButton.interactable = StellarManager.GetContractAddress() != contractField.text && StrKey.IsValidContractId(contractField.text);
        setSneedButton.interactable = StellarManager.stellar.sneed != sneedField.text && StrKey.IsValidEd25519SecretSeed(sneedField.text);
        currentContractText.text = StellarManager.GetContractAddress();
        currentSneedText.text = StellarManager.stellar.sneed;
        if (currentSneedText.text == StellarManager.testHostSneed)
        {
            currentSneedText.text += " (Host sneed)";
        }

        if (currentSneedText.text == StellarManager.testGuestSneed)
        {
            currentSneedText.text += " (Guest sneed)";
        }
        currentAddressText.text = StellarManager.GetUserAddress();
        User? currentUser = StellarManager.networkState.user;
        joinLobbyButton.interactable = true;
        makeLobbyButton.interactable = true;
        viewLobbyButton.interactable = false;
        if (currentUser.HasValue)
        {
            User user = currentUser.Value;
            if (user.current_lobby == 0)
            {
                currentLobbyText.text = "No lobby";
                viewLobbyButton.interactable = false;
            }
            else
            {
                // user is in a lobby
                currentLobbyText.text = user.current_lobby.ToString();
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
        setContractButton.interactable = StellarManager.GetContractAddress() != contractField.text && StrKey.IsValidContractId(contractField.text);
    }
    
    void OnSneedFieldChanged(string input)
    {
        setSneedButton.interactable = StellarManager.stellar.sneed != sneedField.text && StrKey.IsValidEd25519SecretSeed(sneedField.text);
    }

    void OnFillGuestSneed()
    {
        sneedField.text = StellarManager.testGuestSneed;
    }

    void OnFillHostSneed()
    {
        sneedField.text = StellarManager.testHostSneed;
    }
    
    void OnSetSneed()
    {
        string input = sneedField.text;
        sneedField.text = string.Empty;
        _ = StellarManager.SetSneed(input);
    }
    
    void OnSetContract()
    {
        string input = contractField.text;
        contractField.text = string.Empty;
        _ = StellarManager.SetContractAddress(input);
    }
}
