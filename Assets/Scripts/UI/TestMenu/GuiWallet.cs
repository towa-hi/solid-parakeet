using System;
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
    
    public WalletManager walletManager;

    
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
        _ = await walletManager.OnConnectWallet();
        walletText.text = WalletManager.address;
    }

    void HandleRefreshButton()
    {
        
    }

}