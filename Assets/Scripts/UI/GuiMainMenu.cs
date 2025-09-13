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
    public Button settingsButton;
    public Button walletButton;
    

    public event Action OnJoinLobbyButton;
    public event Action OnMakeLobbyButton;
    public event Action OnSettingsButton;
    public event Action OnViewLobbyButton;
    public event Action OnWalletButton;

    public event Action OnAssetButton;
    
    void Start()
    {
        contractField.onValueChanged.AddListener(OnContractFieldChanged);
        setContractButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnSetContract();
        });
        sneedField.onValueChanged.AddListener(OnSneedFieldChanged);
        setSneedButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnSetSneed();
        });
        fillGuestSneedButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnFillGuestSneed();
        });
        fillHostSneedButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnFillHostSneed();
        });
        joinLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnJoinLobbyButton?.Invoke();
        });
        makeLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnMakeLobbyButton?.Invoke();
        });
        settingsButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnSettingsButton?.Invoke();
        });
        viewLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnViewLobbyButton?.Invoke();
        });
        walletButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnWalletButton?.Invoke();
        });
        assetsButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
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
    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        if (show)
        {
            AudioManager.PlayMusic(MusicTrack.MAIN_MENU_MUSIC);
        }
    }
    public override void Refresh()
    {
        string currentContract = StellarManager.GetContractAddress();
        string currentSneed = StellarManager.GetCurrentSneed() ?? string.Empty;
        string currentAddress = StellarManager.GetUserAddress();

        setContractButton.interactable = StrKey.IsValidContractId(contractField.text) && contractField.text != currentContract;
        setSneedButton.interactable = StrKey.IsValidEd25519SecretSeed(sneedField.text) && sneedField.text != currentSneed;

        currentContractText.text = currentContract;
        currentSneedText.text = currentSneed;
        if (!string.IsNullOrEmpty(currentSneed))
        {
            DefaultSettings ds = ResourceRoot.DefaultSettings;
            if (currentSneed == ds.defaultHostSneed)
            {
                currentSneedText.text += " (Host sneed)";
            }
            else if (currentSneed == ds.defaultGuestSneed)
            {
                currentSneedText.text += " (Guest sneed)";
            }
        }
        currentAddressText.text = string.IsNullOrEmpty(currentAddress) ? "No address" : currentAddress;

        // Offline gating
        bool isOnline = GameManager.instance.IsOnline();
        if (!isOnline)
        {
            currentLobbyText.text = "Offline";
            joinLobbyButton.interactable = false;
            makeLobbyButton.interactable = true;
            viewLobbyButton.interactable = FakeServer.fakeLobbyInfo.HasValue;
            var viewText = viewLobbyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (viewText != null)
            {
                viewText.text = FakeServer.fakeLobbyInfo.HasValue ? "Re-enter Game" : "View Lobby";
            }
            walletButton.interactable = false;
            assetsButton.interactable = false;
            return;
        }

        User? currentUser = StellarManager.networkState.user;
        joinLobbyButton.interactable = true;
        makeLobbyButton.interactable = true;
        viewLobbyButton.interactable = false;
        var onlineViewText = viewLobbyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (onlineViewText != null)
        {
            onlineViewText.text = "View Lobby";
        }
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
        setSneedButton.interactable = StellarManager.GetCurrentSneed() != sneedField.text && StrKey.IsValidEd25519SecretSeed(sneedField.text);
    }

    void OnFillGuestSneed()
    {
        sneedField.text = ResourceRoot.DefaultSettings.defaultGuestSneed;
    }

    void OnFillHostSneed()
    {
        sneedField.text = ResourceRoot.DefaultSettings.defaultHostSneed;
    }
    
    void OnSetSneed()
    {
        string input = sneedField.text;
        sneedField.text = string.Empty;
        StellarManager.SetSneed(input);
    }
    
    void OnSetContract()
    {
        string input = contractField.text;
        contractField.text = string.Empty;
        StellarManager.SetContractAddress(input);
    }
}
