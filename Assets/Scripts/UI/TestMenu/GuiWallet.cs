using System;
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

    public Button backButton;
    public Button connectWalletButton;
    public Button refreshButton;
    public GameObject cardArea;
    
    public event Action OnBackButton;
    public event Action OnConnectWalletButton;
    public event Action OnRefreshButton;

    public AccountEntry accountEntry = null;
    void Start()
    {
        backButton.onClick.AddListener(() => { OnBackButton?.Invoke(); });
        connectWalletButton.onClick.AddListener(HandleOnConnectWalletButton);
        refreshButton.onClick.AddListener(HandleRefreshButton);
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
        // refreshButton.interactable = WalletManager.webGL;
    }
    
    async void HandleOnConnectWalletButton()
    {
        accountEntry = await StellarManagerTest.GetAccount(WalletManager.address);
        Refresh();
    }

    void HandleRefreshButton()
    {
        Refresh();
    }

}