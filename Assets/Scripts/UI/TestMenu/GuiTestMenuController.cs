using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contract;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

public class GuiTestMenuController : MenuElement
{
    public GuiTestStartMenu startMenuElement;
    public GuiTestLobbyMaker lobbyMakerElement;
    public GuiTestLobbyViewer lobbyViewerElement;
    public GuiTestLobbyJoiner lobbyJoinerElement;
    public GuiWallet walletElement;
    public GuiTestGame gameElement;
    public GameObject blockerObject;
    public TextMeshProUGUI blockerText;
    static GameObject blocker;
    
    // state
    public TestGuiElement currentElement;
    public string currentProcedure;
    
    void Start()
    {
        blocker = blockerObject;
        StellarManagerTest.Initialize();

        StellarManagerTest.OnTaskStarted += EnableBlocker;
        StellarManagerTest.OnTaskEnded += DisableBlocker;
        
        startMenuElement.OnJoinLobbyButton += GotoJoinLobby;
        startMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
        startMenuElement.OnCancelButton += CloseMenu;
        startMenuElement.OnViewLobbyButton += ViewLobby;
        startMenuElement.OnWalletButton += GotoWallet;
        
        lobbyMakerElement.OnBackButton += GotoStartMenu;
        lobbyMakerElement.OnSubmitLobbyButton += OnSubmitLobbyButton;
        
        lobbyViewerElement.OnBackButton += GotoStartMenu;
        lobbyViewerElement.OnDeleteButton += DeleteLobby;
        lobbyViewerElement.OnRefreshButton += RefreshNetworkState;
        lobbyViewerElement.OnStartButton += OnStartGame;
        
        lobbyJoinerElement.OnBackButton += GotoStartMenu;
        lobbyJoinerElement.OnJoinButton += JoinLobby;
        
        walletElement.OnBackButton += GotoStartMenu;



        startMenuElement.SetIsEnabled(false, false);
        lobbyMakerElement.SetIsEnabled(false, false);
        lobbyJoinerElement.SetIsEnabled(false, false);
        lobbyViewerElement.SetIsEnabled(false, false);
        gameElement.SetIsEnabled(false, false);
        walletElement.SetIsEnabled(false, false);
    }
    
    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        if (show)
        {
            GotoStartMenu();
        }
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

    void CloseMenu()
    {
        // TODO: do this later
    }
    
    async void GotoLobbyMaker()
    {
        await StellarManagerTest.UpdateState();
        if (StellarManagerTest.currentLobby.HasValue)
        {
            ShowMenuElement(lobbyMakerElement, true);
        }
    }

    async void GotoStartMenu()
    {
        await StellarManagerTest.UpdateState();
        ShowMenuElement(startMenuElement, true);
    }

    async void GotoJoinLobby()
    {
        await StellarManagerTest.UpdateState();
        ShowMenuElement(lobbyJoinerElement, true);
    }

    void GotoWallet()
    {
        ShowMenuElement(walletElement, false);
    }
    
    async void ViewLobby()
    {
        _ = await StellarManagerTest.UpdateState();
        if (StellarManagerTest.currentLobby.HasValue)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    async void JoinLobby(string lobbyId)
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
        if (StellarManagerTest.currentLobby.HasValue)
        {
            ShowMenuElement(gameElement, true);
        }
    }
    
    async void OnSubmitLobbyButton(Contract.LobbyParameters parameters)
    {
        int code = await StellarManagerTest.MakeLobbyRequest(parameters);
        await StellarManagerTest.UpdateState();
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement, true);
        }
    }

    async void DeleteLobby()
    {
        int code = await StellarManagerTest.LeaveLobbyRequest();
        await StellarManagerTest.UpdateState();
        if (code == 0)
        {
            ShowMenuElement(startMenuElement, true);
        }
    }
    
    async void RefreshNetworkState()
    {
        _ = await StellarManagerTest.UpdateState();
    }

    void EnableBlocker(TaskInfo task)
    {
        blocker.SetActive(true);
        Debug.Log("Enabled blocker");
        blockerText.text = task.taskMessage;
    }
    
    void DisableBlocker(TaskInfo task)
    {
        blocker.SetActive(false);
        Debug.Log("Disabled blocker");
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
    
    public void SetIsEnabled(bool inIsEnabled)
    {
        isEnabled = inIsEnabled;
        gameObject.SetActive(inIsEnabled);
    }

    public virtual void Refresh(TestBoardManager boardManager)
    {
        
    }

    public virtual void Initialize(TestBoardManager boardManager)
    {
        
    }
}