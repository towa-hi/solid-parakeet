using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contract;
using Stellar.Utilities;
using UnityEngine;

public class GuiTestMenuController : MenuElement
{
    public GuiTestStartMenu startMenuElement;
    public GuiTestLobbyMaker lobbyMakerElement;
    public GuiTestLobbyViewer lobbyViewerElement;
    public GuiTestLobbyJoiner lobbyJoinerElement;
    public GuiTestWaiting waitingElement;
    public GameObject blocker;
    // state
    public TestGuiElement currentElement;
    public string currentProcedure;
    
    void Start()
    {
        StellarManagerTest.Initialize();
        startMenuElement.OnSetSneedButton += OnSetSneed;
        startMenuElement.OnSetContractButton += OnSetContract;
        startMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
        startMenuElement.OnCancelButton += CloseMenu;
        startMenuElement.OnViewLobbyButton += ViewLobby;
        startMenuElement.OnJoinLobbyButton += GotoJoinLobby;

        lobbyMakerElement.OnBackButton += GotoStartMenu;
        lobbyMakerElement.OnSubmitLobbyButton += OnSubmitLobbyButton;
        
        lobbyViewerElement.OnDeleteButton += OnDeleteLobbyButton;
        lobbyViewerElement.OnBackButton += GotoStartMenu;
        lobbyViewerElement.OnRefreshButton += RefreshNetworkState;
        lobbyViewerElement.OnStartButton += OnStartGame;
        
        lobbyJoinerElement.OnBackButton += GotoStartMenu;
        lobbyJoinerElement.OnJoinButton += JoinLobby;
        startMenuElement.SetIsEnabled(false);
        lobbyMakerElement.SetIsEnabled(false);
        lobbyJoinerElement.SetIsEnabled(false);
        lobbyViewerElement.SetIsEnabled(false);
        waitingElement.SetIsEnabled(false);
    }
    
    public void Initialize()
    {
        
    }

    void OnNetworkUpdate()
    {
        
    }
    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        if (show)
        {
            startMenuElement.SetIsEnabled(false);
            lobbyMakerElement.SetIsEnabled(false);
            lobbyJoinerElement.SetIsEnabled(false);
            waitingElement.SetIsEnabled(false);
            GotoStartMenu();
        }
    }

    async Task<bool> UpdateNetworkState()
    {
        Blocker(true);
        bool success = await StellarManagerTest.UpdateState();
        Blocker(false);
        return success;
    }
    
    void ShowElement(TestGuiElement element)
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
        if (StrKey.IsValidEd25519SecretSeed(sneed))
        {
            _ = StellarManagerTest.SetSneed(sneed);
        }
    }

    void OnSetContract(string contractAddress)
    {
        if (StrKey.IsValidContractId(contractAddress))
        {
            _ = StellarManagerTest.SetContractAddress(contractAddress);
        }
    }

    void CloseMenu()
    {
        // TODO: do this later
    }
    
    async void GotoLobbyMaker()
    {
        Blocker(true);
        _ = await StellarManagerTest.UpdateState();
        Blocker(false);
        ShowElement(lobbyMakerElement);
    }

    async void GotoStartMenu()
    {
        Blocker(true);
        _ = await StellarManagerTest.UpdateState();
        Blocker(false);
        ShowElement(startMenuElement);
    }

    async void GotoJoinLobby()
    {
        Blocker(true);
        _ = await StellarManagerTest.UpdateState();
        Blocker(false);
        User? maybeUser = StellarManagerTest.currentUser;
        if (maybeUser.HasValue)
        {
            ShowElement(lobbyJoinerElement);
        }
    }

    async void ViewLobby()
    {
        Blocker(true);
        _ = await StellarManagerTest.UpdateState();
        Blocker(false);
        if (StellarManagerTest.currentLobby != null)
        {
            ShowElement(lobbyViewerElement);
        }
    }

    async void JoinLobby()
    {
        Blocker(true);
        string lobbyId = lobbyJoinerElement.GetLobbyId();
        Lobby? maybeLobby = await StellarManagerTest.GetLobby(lobbyId);
        if (maybeLobby.HasValue)
        {
            Lobby lobby = maybeLobby.Value;
            if (string.IsNullOrEmpty(lobby.guest_address))
            {
                int code = await StellarManagerTest.JoinLobbyRequest(lobby.index);
                _ = await StellarManagerTest.UpdateState();
                if (code == 0)
                {
                    ShowElement(lobbyViewerElement);
                }
                else if (code == 12)
                {
                    Debug.LogWarning($"Failed to join lobby with code {code}");
                }
            }
            else
            {
                Debug.LogWarning($"attempted to join lobby {lobby.index} but lobby.guest_address is already {lobby.guest_address}");
            }
        }
        else
        {
            Debug.LogWarning($"lobby {lobbyId} not found");
        }
        Blocker(false);
        
    }

    async void OnStartGame()
    {
        Blocker(true);
        _ = await StellarManagerTest.UpdateState();
        Lobby? maybeLobby = StellarManagerTest.currentLobby;
        if (!maybeLobby.HasValue)
        {
            Debug.LogError("no lobby");
        }
        Lobby lobby = maybeLobby.Value;
        Blocker(false);
        if (!lobby.IsLobbyStartable())
        {
            Debug.LogError("Lobby is not startable");
        }
        else
        {
            GameManager.instance.StartGame(lobby);
        }
        
        
    }
    
    void GotoWaiting()
    {
        ShowElement(waitingElement);
    }
    
    async void OnSubmitLobbyButton()
    {
        Blocker(true);
        Contract.LobbyParameters parameters = lobbyMakerElement.GetLobbyParameters();
        int code = await StellarManagerTest.MakeLobbyRequest(parameters);
        //lobbyMakerElement.OnLobbyMade(code);
        _ = await StellarManagerTest.UpdateState();
        Blocker(false);
        if (code == 0)
        {
            ShowElement(lobbyViewerElement);
        }
    }

    async void OnDeleteLobbyButton()
    {
        Blocker(true);
        int code = await StellarManagerTest.LeaveLobbyRequest();
        Debug.Log(code);
        _ = await StellarManagerTest.UpdateState();
        GotoStartMenu();
        Blocker(false);
    }

    async void RefreshNetworkState()
    {
        Blocker(true);
        _ = await StellarManagerTest.UpdateState();
        Blocker(false);
    }
    void Blocker(bool isOn)
    {
        blocker.SetActive(isOn);
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