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

public class GuiMenuController: MonoBehaviour
{
    public GuiStartMenu startMenuElement;
    public GuiMainMenu mainMenuElement;
    public GuiLobbyMaker lobbyMakerElement;
    public GuiLobbyViewer lobbyViewerElement;
    public GuiLobbyJoiner lobbyJoinerElement;
    public GuiWallet walletElement;
    public GuiGame gameElement;
    public TopBar topBar;
    
    // state
    public TestGuiElement currentElement;
    public string currentProcedure;
    
    void Start()
    {
        StellarManager.Initialize();

        StellarManager.OnTaskStarted += ShowTopBar;
        StellarManager.OnTaskEnded += HideTopBar;
        
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
        ShowMenuElement(lobbyMakerElement, true);
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
        ShowMenuElement(lobbyJoinerElement, true);
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
        ShowMenuElement(lobbyViewerElement, true);
    }

    async void JoinLobby(LobbyId lobbyId)
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
            GameManager.instance.boardManager.StartBoardManager();
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
        // FakeServer.ins.SetFakeParameters(parameters);
        // ShowMenuElement(gameElement, false);
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

    void ShowTopBar(TaskInfo task)
    {
        topBar.Show(true);
        string address = StellarManager.GetUserAddress();
        Color backgroundColor = address == StellarManager.testHost ? Color.red : Color.blue;
        topBar.SetView(backgroundColor, task.taskMessage);
    }
    
    void HideTopBar(TaskInfo task)
    {
        topBar.Show(false);
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
    public bool isVisible;

    public void ShowElement(bool show)
    {
        isVisible = show;
        gameObject.SetActive(show);
    }
    
}