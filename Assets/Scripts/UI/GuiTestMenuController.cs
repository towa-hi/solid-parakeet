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
        StellarManager.Initialize();

        StellarManager.OnTaskStarted += EnableBlocker;
        StellarManager.OnTaskEnded += DisableBlocker;

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
        await StellarManager.UpdateState();
        if (!StellarManager.networkState.lobbyInfo.HasValue)
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
        await StellarManager.UpdateState();
        ShowMenuElement(mainMenuElement, true);
    }

    async void GotoJoinLobby()
    {
        await StellarManager.UpdateState();
        if (!StellarManager.networkState.lobbyInfo.HasValue)
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
        _ = StellarManager.GetAssets(StellarManager.GetUserAddress());
    }
    
    async void ViewLobby()
    {
        await StellarManager.UpdateState();
        if (StellarManager.networkState.lobbyInfo.HasValue)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    async void JoinLobby(uint lobbyId)
    {
        int code = await StellarManager.JoinLobbyRequest(lobbyId);
        await StellarManager.UpdateState();
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    async void OnStartGame()
    {
        await StellarManager.UpdateState();
        if (StellarManager.networkState.inLobby)
        {
            ShowMenuElement(gameElement, true);
        }
    }
    
    async void OnSubmitLobbyButton(LobbyParameters parameters)
    {
        int code = await StellarManager.MakeLobbyRequest(parameters);
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
        int code = await StellarManager.LeaveLobbyRequest();
        if (code == 0)
        {
            ShowMenuElement(mainMenuElement, true);
        }
    }
    
    async void RefreshNetworkState()
    {
        await StellarManager.UpdateState();
    }

    void EnableBlocker(TaskInfo task)
    {
        blocker.SetActive(true);
        blockerText.text = task.taskMessage;
        string address = StellarManager.GetUserAddress();
        if (address == StellarManager.testHost)
        {
            Color color = Color.red;
            color.a = 0.5f;
            blockerImage.color = color;
        }
        else if (address == StellarManager.testGuest)
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