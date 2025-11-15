using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Stellar.Utilities;
using System.Threading.Tasks;
using Stellar;

public class NetworkMenu2 : MenuBase
{
    public ButtonExtended testnetTabButton;
    public ButtonExtended mainnetTabButton;

    public ButtonExtended sneedTabButton;
    public TMP_InputField contractField;
    public TMP_InputField sneedField;
    public TMP_InputField walletField;

    public Button connectButton;
    public Button offlineButton;
    public GuiTabs networkTabs;
    public GuiTabs connectMethodTabs;
    public ButtonExtended walletTabButton;

    public GameObject debugPanel;
    public ButtonExtended debugSetHostButton;
    public ButtonExtended debugSetGuestButton;

    public ButtonExtended createAccountButton;

    bool isTestnet = true;
    bool isWallet = false;


    private void Start()
    {
        
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        bool isDev = ResourceRoot.DefaultSettings.isDev;
        debugPanel.SetActive(isDev && !isWebGL);
        networkTabs.OnActiveTabChanged += OnNetworkTabChanged;
        connectMethodTabs.OnActiveTabChanged += OnConnectMethodTabChanged;
        createAccountButton.onClick.AddListener(HandleCreateAccount);
        connectButton.onClick.AddListener(HandleConnect);
        offlineButton.onClick.AddListener(HandleOffline);
        contractField.onValueChanged.AddListener(HandleContractFieldChanged);
        sneedField.onValueChanged.AddListener(HandleSneedFieldChanged);
        debugSetHostButton.onClick.AddListener(HandleDebugSetHost);
        debugSetGuestButton.onClick.AddListener(HandleDebugSetGuest);
        debugPanel.SetActive(false);
        OnOpened += ApplyNetworkContextToUI;
        ApplyNetworkContextToUI();
        Refresh();
    }

    void HandleCreateAccount()
    {
        _ = menuController.CreateAccountAsync();
    }

    void HandleDebugSetHost()
    {
        sneedField.text = ResourceRoot.DefaultSettings.defaultHostSneed;
    }

    void HandleDebugSetGuest()
    {
        sneedField.text = ResourceRoot.DefaultSettings.defaultGuestSneed;
    }

    void HandleContractFieldChanged(string input)
    {
        Refresh();
    }

    void HandleSneedFieldChanged(string input)
    {
        Refresh();
    }

    void OnNetworkTabChanged(int index)
    {
        isTestnet = index == 0;
        contractField.interactable = isTestnet;
        sneedTabButton.interactable = isTestnet;
        if (!isTestnet)
        {
            connectMethodTabs.SetActiveTab(0);
        }
        Refresh();
    }
    void OnConnectMethodTabChanged(int index)
    {
        isWallet = index == 0;
        if (isWallet)
        {
            UpdateWalletFieldFromContext();
        }
        else
        {
            walletField.SetTextWithoutNotify(string.Empty);
        }
        Refresh();
    }

    public void HandleConnect()
    {
        ModalConnectData data = new ModalConnectData
        {
            online = true,
            isTestnet = isTestnet, 
            contract = contractField.text, 
            sneed = sneedField.text, 
            isWallet = isWallet,
            serverUri = isTestnet ? ResourceRoot.DefaultSettings.defaultTestnetUri : ResourceRoot.DefaultSettings.defaultMainnetUri,
        };
        if (isWallet)
        {
            data.sneed = "wallet";
        }
        _ = menuController.ConnectToNetworkAsync(data).ContinueWith(task => Debug.LogException(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }

    public void HandleOffline()
    {
        ModalConnectData data = new ModalConnectData
        {
            online = false,
            isTestnet = isTestnet, 
            contract = contractField.text, 
            sneed = sneedField.text, // unused
            isWallet = isWallet,
            serverUri = "unused",
        };
        _ = menuController.ConnectToNetworkAsync(data);
    }

    public override void Refresh()
    {
        contractField.interactable = isTestnet;
        sneedTabButton.interactable = isTestnet;
        bool contractValid = StrKey.IsValidContractId(contractField.text);
        bool sneedValid = StrKey.IsValidEd25519SecretSeed(sneedField.text);
        bool connectAllowed = contractValid && (isWallet || sneedValid);
        connectButton.interactable = connectAllowed;
    }

    void ApplyNetworkContextToUI()
    {
        bool contextAvailable = StellarManager.initialized && StellarManager.networkContext.userAccount != null;
        bool useContext = contextAvailable && StellarManager.networkContext.online;
        NetworkContext context = useContext ? StellarManager.networkContext : default;

        bool targetIsTestnet = useContext ? context.isTestnet : true;
        bool targetIsWallet = useContext ? context.isWallet : true;
        if (!targetIsTestnet)
        {
            targetIsWallet = true;
        }

        if (networkTabs != null && networkTabs.tabs.Count > 0)
        {
            int desiredNetworkTab = Mathf.Clamp(targetIsTestnet ? 0 : 1, 0, networkTabs.tabs.Count - 1);
            networkTabs.SetActiveTab(desiredNetworkTab);
        }
        else
        {
            isTestnet = targetIsTestnet;
        }

        if (connectMethodTabs != null && connectMethodTabs.tabs.Count > 0)
        {
            int desiredConnectTab = Mathf.Clamp(targetIsWallet ? 0 : 1, 0, connectMethodTabs.tabs.Count - 1);
            connectMethodTabs.SetActiveTab(desiredConnectTab);
        }
        else
        {
            isWallet = targetIsWallet;
        }

        string contract = useContext && !string.IsNullOrEmpty(context.contractAddress)
            ? context.contractAddress
            : ResourceRoot.DefaultSettings.defaultTestnetContractAddress;
        contractField.SetTextWithoutNotify(contract);

        if (targetIsWallet)
        {
            if (useContext)
            {
                UpdateWalletFieldFromContext();
            }
            else
            {
                walletField.SetTextWithoutNotify(string.Empty);
            }
            sneedField.SetTextWithoutNotify(string.Empty);
        }
        else
        {
            string secretSeed = useContext && context.userAccount != null
                ? context.userAccount.SecretSeed ?? string.Empty
                : string.Empty;
            sneedField.SetTextWithoutNotify(secretSeed);
            walletField.SetTextWithoutNotify(string.Empty);
        }
        Refresh();
    }

    void UpdateWalletFieldFromContext()
    {
        string accountId = string.Empty;
        if (StellarManager.initialized && StellarManager.networkContext.online && StellarManager.networkContext.userAccount != null)
        {
            accountId = StellarManager.networkContext.userAccount.AccountId ?? string.Empty;
        }
        walletField.SetTextWithoutNotify(accountId);
    }
}



