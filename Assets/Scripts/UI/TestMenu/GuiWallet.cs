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

    public event Action OnBackButton;
    public event Action OnConnectWalletButton;
    public event Action OnRefreshButton;
    
    
    void Start()
    {
        backButton.onClick.AddListener(() => { OnBackButton?.Invoke(); });
        connectWalletButton.onClick.AddListener(HandleOnConnectWalletButton);
        refreshButton.onClick.AddListener(HandleRefreshButton);

        Refresh();
    }

    void Refresh()
    {
        // connectWalletButton.interactable = WalletManager.webGL;
        // refreshButton.interactable = WalletManager.webGL;
    }
    async void HandleOnConnectWalletButton()
    {
        bool connected = await StellarManagerTest.ConnectWallet();
        if (!connected)
        {
            walletText.text = "Not connected";
            networkText.text = "Not connected";
            return;
        }
        walletText.text = WalletManager.address;
        networkText.text = WalletManager.networkDetails.networkPassphrase;
        AccountEntry accountEntry = await StellarManagerTest.GetAccount(WalletManager.address);
        balanceText.text = accountEntry.balance.ToString();
        
    }

    void HandleRefreshButton()
    {
        
    }

}