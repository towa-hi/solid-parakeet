using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Stellar.Utilities;

public class ModalConnect : ModalElement
{
    public TMP_InputField contractField;
    public TMP_InputField sneedField;
    public Button testnetButton;
    public Button mainnetButton;
    public Button walletButton;
    public Button keyButton;
    public GameObject keyPanel;

    public Button closeButton;
    public Button connectButton;

    public Action OnCloseButton;
    public Action<ModalConnectData> OnConnectButton;

    bool isTestnet = true;
    bool isWallet = true;

    void Start()
    {
        contractField.onValueChanged.AddListener(HandleContractFieldChanged);
        sneedField.onValueChanged.AddListener(HandleSneedFieldChanged);
        closeButton.onClick.AddListener(HandleCloseButton);
        connectButton.onClick.AddListener(HandleConnectButton);
        testnetButton.onClick.AddListener(HandleTestnetButton);
        mainnetButton.onClick.AddListener(HandleMainnetButton);
        walletButton.onClick.AddListener(HandleWalletButton);
        keyButton.onClick.AddListener(HandleKeyButton);
        DefaultSettings defaultSettings = ResourceRoot.DefaultSettings;
        contractField.text = defaultSettings.defaultContractAddress;
        sneedField.text = defaultSettings.defaultHostSneed;
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        if (!isWebGL)
        {
            isWallet = false;
        }
        Refresh();
    }

    public override void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
    }

    void HandleContractFieldChanged(string input)
    {
        Refresh();
    }

    void HandleSneedFieldChanged(string input)
    {
        Refresh();
    }

    void HandleCloseButton()
    {
        OnCloseButton?.Invoke();
    }

    void HandleConnectButton()
    {
        string contract = contractField.text;
        string sneed = sneedField.text;

        OnConnectButton?.Invoke(new ModalConnectData { isTestnet = isTestnet, contract = contract, sneed = sneed, isWallet = isWallet });
    }

    void HandleTestnetButton()
    {
        isTestnet = true;
        Refresh();
    }

    void HandleMainnetButton()
    {
        isTestnet = false;
        Refresh();
    }
    
    void HandleWalletButton()
    {
        isWallet = true;
        Refresh();
    }

    void HandleKeyButton()
    {
        isWallet = false;
        Refresh();
    }

    void Refresh()
    {
        // check if in editor or webgl build
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        testnetButton.interactable = !isTestnet;
        mainnetButton.interactable = isTestnet;
        if (!isWebGL)
        {
            walletButton.interactable = false;
            keyButton.interactable = false;
            keyPanel.SetActive(true);
        }
        else
        {
            walletButton.interactable = !isWallet;
            keyButton.interactable = isWallet;
            keyPanel.SetActive(!isWallet);
        }
        bool isSneedValid = false;
        if (keyPanel.activeSelf)
        {
            isSneedValid = StrKey.IsValidEd25519SecretSeed(sneedField.text);
            if (isSneedValid)
            {
                sneedField.GetComponent<Image>().color = Color.white;
            }
            else
            {
                sneedField.GetComponent<Image>().color = Color.red;
            }
        }
        bool isContractValid = StrKey.IsValidContractId(contractField.text);
        if (isContractValid)
        {
            contractField.GetComponent<Image>().color = Color.white;
        }
        else
        {
            contractField.GetComponent<Image>().color = Color.red;
        }
        if (isSneedValid || isWallet && isContractValid)
        {
            connectButton.interactable = true;
        }
        else
        {
            connectButton.interactable = false;
        }
    }
}

public struct ModalConnectData
{
    public bool online;
    public bool isTestnet;
    public string contract;
    public string sneed;
    public bool isWallet;
    public string serverUri;
}