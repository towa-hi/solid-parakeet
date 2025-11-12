using System;
using System.Text;
using Stellar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//deprecated
public class GuiWallet : MenuElement
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
        // backButton.onClick.AddListener(() =>
        // {
        //     OnBackButton?.Invoke();
        // });
        // connectWalletButton.onClick.AddListener(() =>
        // {
        //     HandleOnConnectWalletButton();
        // });
        // refreshButton.onClick.AddListener(() =>
        // {
        //     HandleRefreshButton();
        // });
        // StellarManager.OnAssetsUpdated += OnAssetsUpdated;
        // accountEntry = null;
    }

    void OnAssetsUpdated(TrustLineEntry entry)
    {
        // if (!gameObject.activeSelf) return;
        // if (entry == null)
        // {
        //     string message = $"No SCRY could be found for account {WalletManager.address}";
        //     assetsText.text = message;
        // }
        // else if (entry.asset is TrustLineAsset.AssetTypeCreditAlphanum4 asset)
        // {
        //     long balance = entry.balance.InnerValue;
        //     string assetCode = Encoding.ASCII.GetString(asset.alphaNum4.assetCode.InnerValue).TrimEnd('\0');
        //     string message = $"{assetCode}: balance: {balance}";
        //     assetsText.text = message;
        // }
    }

    public override void Refresh()
    {
        // walletText.text = string.IsNullOrEmpty(WalletManager.address) ? "Not connected" : WalletManager.address;
        // networkText.text = WalletManager.networkDetails == null ? "Not connected" : WalletManager.networkDetails.networkPassphrase;
        // refreshButton.interactable = WalletManager.webGL;
        // connectWalletButton.interactable = WalletManager.webGL;
        // if (accountEntry != null)
        // {
        //     statusText.text = "Freighter wallet connected!";
        //     //balanceText.text = accountEntry.balance.InnerValue.ToString() + " XLM";
        // }
        // else
        // {
        //     statusText.text = "Freighter wallet not connected. Click connect wallet to continue";
        // }
    }
    
    async void HandleOnConnectWalletButton()
    {
        // Result<WalletManager.WalletConnection> resultConn = await WalletManager.ConnectWallet();
        // if (resultConn.IsOk)
        // {
        //     WalletManager.address = resultConn.Value.address;
        //     WalletManager.networkDetails = resultConn.Value.networkDetails;
        //     var result = await StellarManager.GetAccount(WalletManager.address);
        //     if (result.IsOk)
        //     {
        //         accountEntry = result.Value;
        //     }
        //     else
        //     {
        //         accountEntry = null;
        //     }
        //     if (accountEntry != null)
        //     {
        //         await StellarManager.GetAssets(WalletManager.address);
        //     }
        // }
        // else
        // {
        //     statusText.text = FormatStatusMessage(resultConn.Code);
        // }
        // Refresh();
    }

    async void HandleRefreshButton()
    {
        // Result<WalletManager.WalletConnection> resultConn2 = await WalletManager.ConnectWallet();
        // if (resultConn2.IsOk)
        // {
        //     WalletManager.address = resultConn2.Value.address;
        //     WalletManager.networkDetails = resultConn2.Value.networkDetails;
        //     var result = await StellarManager.GetAccount(WalletManager.address);
        //     if (result.IsOk)
        //     {
        //         accountEntry = result.Value;
        //     }
        //     else
        //     {
        //         accountEntry = null;
        //     }
        //     _ = await StellarManager.GetAssets(WalletManager.address);
        // }
        // else
        // {
        //     accountEntry = null;
        //     statusText.text = FormatStatusMessage(resultConn2.Code);
        // }
        // Refresh();
    }

    // string FormatStatusMessage(StatusCode code)
    // {
    //     switch (code)
    //     {
    //         case StatusCode.CONTRACT_ERROR: return "Contract error occurred.";
    //         case StatusCode.NETWORK_ERROR: return "Network error occurred.";
    //         case StatusCode.RPC_ERROR: return "RPC error occurred.";
    //         case StatusCode.TIMEOUT: return "The request timed out.";
    //         case StatusCode.OTHER_ERROR: return "An unexpected error occurred.";
    //         case StatusCode.SERIALIZATION_ERROR: return "Serialization error occurred.";
    //         case StatusCode.DESERIALIZATION_ERROR: return "Deserialization error occurred.";
    //         case StatusCode.TRANSACTION_FAILED: return "Transaction failed.";
    //         case StatusCode.TRANSACTION_NOT_FOUND: return "Transaction not found.";
    //         case StatusCode.TRANSACTION_TIMEOUT: return "Transaction timed out.";
    //         case StatusCode.ENTRY_NOT_FOUND: return "Required entry not found.";
    //         case StatusCode.SIMULATION_FAILED: return "Simulation failed.";
    //         case StatusCode.TRANSACTION_SEND_FAILED: return "Failed to send transaction.";
    //         case StatusCode.WALLET_ERROR: return "Wallet error occurred.";
    //         case StatusCode.WALLET_NOT_AVAILABLE: return "Wallet not available.";
    //         case StatusCode.WALLET_ADDRESS_MISSING: return "Wallet address missing.";
    //         case StatusCode.WALLET_NETWORK_DETAILS_ERROR: return "Failed to retrieve wallet network details.";
    //         case StatusCode.WALLET_PARSING_ERROR: return "Failed to parse wallet response.";
    //         case StatusCode.SUCCESS: return "Success.";
    //         default: return "Operation failed.";
    //     }
    // }
}