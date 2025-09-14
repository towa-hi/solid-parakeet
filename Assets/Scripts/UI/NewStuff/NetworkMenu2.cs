using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Stellar.Utilities;

public class NetworkMenu2 : MenuBase
{
    public TMP_InputField contractField;
    public TMP_InputField sneedField;
    public Button testnetButton;
    public Button mainnetButton;
    public Button walletButton;
    public Button keyButton;
    public GameObject keyPanel;

    public Button connectButton;
    public Button offlineButton;

    bool isTestnet = true;
    bool isWallet = true;

    private void Start()
    {
        contractField.onValueChanged.AddListener(HandleContractFieldChanged);
        sneedField.onValueChanged.AddListener(HandleSneedFieldChanged);

        testnetButton.onClick.AddListener(HandleTestnetButton);
        mainnetButton.onClick.AddListener(HandleMainnetButton);
        walletButton.onClick.AddListener(HandleWalletButton);
        keyButton.onClick.AddListener(HandleKeyButton);

        connectButton.onClick.AddListener(HandleConnect);
        offlineButton.onClick.AddListener(HandleOffline);

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

    void HandleContractFieldChanged(string input)
    {
        Refresh();
    }

    void HandleSneedFieldChanged(string input)
    {
        Refresh();
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

    public void HandleConnect()
    {
        Emit(new ConnectToNetworkCommand(isTestnet, contractField.text, sneedField.text, isWallet));
    }

    public void HandleOffline()
    {
        EmitAction(MenuAction.GoOffline);
    }

    public override void Refresh()
    {
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;

        // Network target selection
        testnetButton.interactable = !isTestnet;
        mainnetButton.interactable = isTestnet;

        // Wallet vs Key selection
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
            var sneedImage = sneedField.GetComponent<Image>();
            if (sneedImage != null)
            {
                sneedImage.color = isSneedValid ? Color.white : Color.red;
            }
        }

        bool isContractValid = StrKey.IsValidContractId(contractField.text);
        var contractImage = contractField.GetComponent<Image>();
        if (contractImage != null)
        {
            contractImage.color = isContractValid ? Color.white : Color.red;
        }

        if (isSneedValid || (isWallet && isContractValid))
        {
            connectButton.interactable = true;
        }
        else
        {
            connectButton.interactable = false;
        }
    }
}


