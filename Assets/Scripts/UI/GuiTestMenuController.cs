using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contract;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GuiTestMenuController: MonoBehaviour
{
    public GuiStartMenu startMenuElement;
    public GuiMainMenu mainMenuElement;
    public GuiTestLobbyMaker lobbyMakerElement;
    public GuiTestLobbyViewer lobbyViewerElement;
    public GuiTestLobbyJoiner lobbyJoinerElement;
    public GuiWallet walletElement;
    public GuiTestGame gameElement;
    public GameObject blockerObject;
    public TextMeshProUGUI blockerText;
    static GameObject blocker;
    static Image blockerImage;
    
    // state
    public TestGuiElement currentElement;
    public string currentProcedure;
    
    void Start()
    {
        blocker = blockerObject;
        blockerImage = blockerObject.GetComponent<Image>();
        StellarManagerTest.Initialize();

        StellarManagerTest.OnTaskStarted += EnableBlocker;
        StellarManagerTest.OnTaskEnded += DisableBlocker;

        startMenuElement.OnStartButton += GotoMainMenu;
        
        mainMenuElement.OnJoinLobbyButton += GotoJoinLobby;
        mainMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
        mainMenuElement.OnOptionsButton += OptionsModal;
        mainMenuElement.OnViewLobbyButton += ViewLobby;
        mainMenuElement.OnWalletButton += GotoWallet;
        mainMenuElement.OnAssetButton += CheckAssets;
        
        lobbyMakerElement.OnBackButton += GotoMainMenu;
        lobbyMakerElement.OnSinglePlayerButton += StartSingleplayer;
        lobbyMakerElement.OnSubmitLobbyButton += OnSubmitLobbyButton;
        
        lobbyViewerElement.OnBackButton += GotoMainMenu;
        lobbyViewerElement.OnDeleteButton += DeleteLobby;
        lobbyViewerElement.OnRefreshButton += RefreshNetworkState;
        lobbyViewerElement.OnStartButton += OnStartGame;
        
        lobbyJoinerElement.OnBackButton += GotoMainMenu;
        lobbyJoinerElement.OnJoinButton += JoinLobby;
        
        walletElement.OnBackButton += GotoMainMenu;

    }
    
    public void Initialize()
    {
        startMenuElement.SetIsEnabled(false, false);
        mainMenuElement.SetIsEnabled(false, false);
        lobbyMakerElement.SetIsEnabled(false, false);
        lobbyJoinerElement.SetIsEnabled(false, false);
        lobbyViewerElement.SetIsEnabled(false, false);
        gameElement.SetIsEnabled(false, false);
        walletElement.SetIsEnabled(false, false);
        GotoStartMenu();
    }
    
    void ShowMenuElement(TestGuiElement element, bool networkUpdated)
    {
        if (currentElement != null)
        {
            currentElement.SetIsEnabled(false, networkUpdated);
        }
        currentElement = element;
        currentElement.SetIsEnabled(true, networkUpdated);
    }
    
    async void GotoLobbyMaker()
    {
        await StellarManagerTest.UpdateState();
        if (!StellarManagerTest.networkState.lobbyInfo.HasValue)
        {
            ShowMenuElement(lobbyMakerElement, true);
        }
    }

    void OptionsModal()
    {
        
    }

    void GotoStartMenu()
    {
        ShowMenuElement(startMenuElement, false);
    }
    
    async void GotoMainMenu()
    {
        await StellarManagerTest.UpdateState();
        ShowMenuElement(mainMenuElement, true);
    }

    async void GotoJoinLobby()
    {
        await StellarManagerTest.UpdateState();
        if (!StellarManagerTest.networkState.lobbyInfo.HasValue)
        {
            ShowMenuElement(lobbyJoinerElement, true);
        }
    }

    void GotoWallet()
    {
        ShowMenuElement(walletElement, false);
    }

    void CheckAssets()
    {
        _ = StellarManagerTest.GetAssets(StellarManagerTest.GetUserAddress());
    }
    
    async void ViewLobby()
    {
        await StellarManagerTest.UpdateState();
        if (StellarManagerTest.networkState.lobbyInfo.HasValue)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    async void JoinLobby(uint lobbyId)
    {
        int code = await StellarManagerTest.JoinLobbyRequest(lobbyId);
        await StellarManagerTest.UpdateState();
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    async void OnStartGame()
    {
        await StellarManagerTest.UpdateState();
        if (StellarManagerTest.networkState.inLobby)
        {
            ShowMenuElement(gameElement, true);
        }
    }
    
    async void OnSubmitLobbyButton(LobbyParameters parameters)
    {
        int code = await StellarManagerTest.MakeLobbyRequest(parameters);
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    
    void StartSingleplayer(LobbyParameters parameters)
    {
        FakeServer.ins.SetFakeParameters(parameters);
        ShowMenuElement(gameElement, false);
    }
    async void DeleteLobby()
    {
        int code = await StellarManagerTest.LeaveLobbyRequest();
        if (code == 0)
        {
            ShowMenuElement(mainMenuElement, true);
        }
    }
    
    async void RefreshNetworkState()
    {
        await StellarManagerTest.UpdateState();
    }

    void EnableBlocker(TaskInfo task)
    {
        blocker.SetActive(true);
        blockerText.text = task.taskMessage;
        string address = StellarManagerTest.GetUserAddress();
        if (address == StellarManagerTest.testHost)
        {
            Color color = Color.red;
            color.a = 0.5f;
            blockerImage.color = color;
        }
        else if (address == StellarManagerTest.testGuest)
        {
            Color color = Color.blue;
            color.a = 0.5f;
            blockerImage.color = color;
        }
        else
        {
            Color color = Color.white;
            color.a = 0.5f;
            blockerImage.color = color;
        }
    }
    
    void DisableBlocker(TaskInfo task)
    {
        blocker.SetActive(false);
        blockerText.text = "";
    }
}

public class TestGuiElement: MonoBehaviour
{
    protected bool isEnabled;
    
    public virtual void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        isEnabled = inIsEnabled;
        gameObject.SetActive(inIsEnabled);
    }
}

public class GameElement: MonoBehaviour
{
    bool isEnabled;
    TestBoardManager bm;
    
    public void SetIsEnabled(bool inIsEnabled)
    {
        isEnabled = inIsEnabled;
        gameObject.SetActive(inIsEnabled);
    }

    public virtual void Initialize(TestBoardManager boardManager, GameNetworkState networkState)
    {
        bm = boardManager;
    }
}