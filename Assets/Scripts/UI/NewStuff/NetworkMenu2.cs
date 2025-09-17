using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Stellar.Utilities;

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
    bool isTestnet = true;
    bool isWallet = false;


    private void Start()
    {
        // TODO: make this use current network settings
        networkTabs.SetActiveTab(0);
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        connectMethodTabs.SetActiveTab(isWebGL ? 0 : 1);
        isTestnet = networkTabs.activeTab == 0;
        isWallet = connectMethodTabs.activeTab == 0;
        networkTabs.OnActiveTabChanged += OnNetworkTabChanged;
        connectMethodTabs.OnActiveTabChanged += OnConnectMethodTabChanged;
        connectButton.onClick.AddListener(HandleConnect);
        offlineButton.onClick.AddListener(HandleOffline);
        contractField.onValueChanged.AddListener(HandleContractFieldChanged);
        sneedField.onValueChanged.AddListener(HandleSneedFieldChanged);
        string defaultContract = ResourceRoot.DefaultSettings.defaultContractAddress;
        contractField.SetTextWithoutNotify(defaultContract);
        string defaultSneed = ResourceRoot.DefaultSettings.defaultHostSneed;
        sneedField.SetTextWithoutNotify(defaultSneed);
        debugSetHostButton.onClick.AddListener(HandleDebugSetHost);
        debugSetGuestButton.onClick.AddListener(HandleDebugSetGuest);
        debugPanel.SetActive(false);
        Refresh();
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
    }
    void OnConnectMethodTabChanged(int index)
    {
        isWallet = index == 0;
        walletField.SetTextWithoutNotify(string.Empty);
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
        EmitAction(new ConnectToNetworkSignal(data));
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
        EmitAction(new ConnectToNetworkSignal(data));
    }

    public override void Refresh()
    {
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        contractField.interactable = isTestnet;
        sneedTabButton.interactable = isTestnet;
        bool isDev = ResourceRoot.DefaultSettings.isDev;
        debugPanel.SetActive(isDev);
    }
}



