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
        
        startMenuElement.OnSetSneedButton += OnSetSneed;
        startMenuElement.OnSetContractButton += OnSetContract;
        startMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
        startMenuElement.OnCancelButton += CloseMenu;
        startMenuElement.OnViewLobbyButton += ViewLobby;
        startMenuElement.OnJoinLobbyButton += GotoJoinLobby;
        startMenuElement.OnWalletButton += GotoWallet;
        lobbyMakerElement.OnBackButton += GotoStartMenu;
        lobbyMakerElement.OnSubmitLobbyButton += OnSubmitLobbyButton;
        
        lobbyViewerElement.OnDeleteButton += OnDeleteLobbyButton;
        lobbyViewerElement.OnBackButton += GotoStartMenu;
        lobbyViewerElement.OnRefreshButton += RefreshNetworkState;
        lobbyViewerElement.OnStartButton += OnStartGame;
        
        lobbyJoinerElement.OnBackButton += GotoStartMenu;
        lobbyJoinerElement.OnJoinButton += JoinLobby;
        
        walletElement.OnBackButton += GotoStartMenu;
        
        
        
        startMenuElement.SetIsEnabled(false);
        lobbyMakerElement.SetIsEnabled(false);
        lobbyJoinerElement.SetIsEnabled(false);
        lobbyViewerElement.SetIsEnabled(false);
        gameElement.SetIsEnabled(false);
        walletElement.SetIsEnabled(false);
    }
    
    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        if (show)
        {
            GotoStartMenu();
        }
    }
    
    void ShowMenuElement(TestGuiElement element)
    {
        if (currentElement != null)
        {
            currentElement.SetIsEnabled(false);
        }
        currentElement = element;
        currentElement.SetIsEnabled(true);
        currentElement.Initialize();
    }

    void OnSetSneed(string sneed)
    {
        StellarManagerTest.SetSneed(sneed);
    }

    void OnSetContract(string contractAddress)
    {
        StellarManagerTest.SetContractAddress(contractAddress);
    }

    void CloseMenu()
    {
        // TODO: do this later
    }
    
    async void GotoLobbyMaker()
    {
        if (await StellarManagerTest.UpdateState())
        {
            ShowMenuElement(lobbyMakerElement);
        }
    }

    async void GotoStartMenu()
    {
        if (await StellarManagerTest.UpdateState())
        {
            ShowMenuElement(startMenuElement);
        }
    }

    async void GotoJoinLobby()
    {
        if (await StellarManagerTest.UpdateState())
        {
            ShowMenuElement(lobbyJoinerElement);
        }
    }

    void GotoWallet()
    {
        ShowMenuElement(walletElement);
    }
    
    async void ViewLobby()
    {
        if (await StellarManagerTest.UpdateState())
        {
            ShowMenuElement(lobbyViewerElement);
        }
    }

    async void JoinLobby(string lobbyId)
    {
        int code = await StellarManagerTest.JoinLobbyRequest(lobbyId);
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement);
        }
    }

    async void OnStartGame()
    {
        _ = await StellarManagerTest.UpdateState();
        Assert.IsTrue(StellarManagerTest.currentUser.HasValue);
        Assert.IsTrue(StellarManagerTest.currentLobby.HasValue);
        Lobby lobby = StellarManagerTest.currentLobby.GetValueOrDefault();
        User user = StellarManagerTest.currentUser.GetValueOrDefault();
        if (lobby.IsLobbyStartable())
        {
            Debug.Log("Starting game");
            GameManager.instance.testBoardManager.StartGame(lobby, user);
            ShowMenuElement(gameElement);
        }
        else
        {
            Debug.LogError("Lobby is not startable");
        }
    }
    
    async void OnSubmitLobbyButton()
    {
        Contract.LobbyParameters parameters = lobbyMakerElement.GetLobbyParameters();
        int code = await StellarManagerTest.MakeLobbyRequest(parameters);
        //lobbyMakerElement.OnLobbyMade(code);
        _ = await StellarManagerTest.UpdateState();
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement);
        }
    }

    async void OnDeleteLobbyButton()
    {
        int code = await StellarManagerTest.LeaveLobbyRequest();
        Debug.Log(code);
        _ = await StellarManagerTest.UpdateState();
        GotoStartMenu();
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
    bool isEnabled;
    
    public void SetIsEnabled(bool inIsEnabled)
    {
        isEnabled = inIsEnabled;
        gameObject.SetActive(inIsEnabled);
    }

    public virtual void Refresh()
    {
        
    }

    public virtual void Initialize()
    {
        
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