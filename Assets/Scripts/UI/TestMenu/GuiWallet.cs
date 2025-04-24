using System;
using System.Text;
using Stellar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiWallet : TestGuiElement
{
    public TextMeshProUGUI walletText;
    public TextMeshProUGUI networkText;
    public TextMeshProUGUI balanceText;
    public TextMeshProUGUI assetsText;
    public TextMeshProUGUI statusText;
    public Button backButton;
    public Button connectWalletButton;
    public Button refreshButton;
    public GameObject cardArea;
    
    public event Action OnBackButton;
    //public event Action OnConnectWalletButton;
    //public event Action OnRefreshButton;

    public AccountEntry accountEntry = null;
    void Start()
    {
        backButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        connectWalletButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            HandleOnConnectWalletButton();
        });
        refreshButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            HandleRefreshButton();
        });
        StellarManagerTest.OnAssetsUpdated += OnAssetsUpdated;
        accountEntry = null;
    }

    void OnAssetsUpdated(TrustLineEntry entry)
    {
        if (!isEnabled) return;
        if (entry == null)
        {
            string message = $"No SCRY could be found for account {WalletManager.address}";
            assetsText.text = message;
        }
        else if (entry.asset is TrustLineAsset.AssetTypeCreditAlphanum4 asset)
        {
            long balance = entry.balance.InnerValue;
            string assetCode = Encoding.ASCII.GetString(asset.alphaNum4.assetCode.InnerValue).TrimEnd('\0');
            string message = $"{assetCode}: balance: {balance}";
            assetsText.text = message;
        }
    }

    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        cardArea.SetActive(inIsEnabled);
        if (inIsEnabled)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        walletText.text = string.IsNullOrEmpty(WalletManager.address) ? "Not connected" : WalletManager.address;
        networkText.text = WalletManager.networkDetails == null ? "Not connected" : WalletManager.networkDetails.networkPassphrase;
        if (accountEntry != null)
        {
            balanceText.text = accountEntry.balance.InnerValue.ToString() + " XLM";
        }
        refreshButton.interactable = WalletManager.webGL;
        connectWalletButton.interactable = WalletManager.webGL;
        if (accountEntry != null)
        {
            statusText.text = "Freighter wallet not connected. Click connect wallet to continue";
        }
        else
        {
            statusText.text = "Freighter wallet connected!";
        }
    }
    
    async void HandleOnConnectWalletButton()
    {
        bool success = await WalletManager.ConnectWallet();
        if (success)
        {
            accountEntry = await StellarManagerTest.GetAccount(WalletManager.address);
            _ = await StellarManagerTest.GetAssets(WalletManager.address);
            Refresh();
        }
    }

    async void HandleRefreshButton()
    {
        bool success = await WalletManager.ConnectWallet();
        if (success)
        {
            accountEntry = await StellarManagerTest.GetAccount(WalletManager.address);
            _ = await StellarManagerTest.GetAssets(WalletManager.address);
        }
        else
        {
            accountEntry = null;
        }
        Refresh();
    }

}