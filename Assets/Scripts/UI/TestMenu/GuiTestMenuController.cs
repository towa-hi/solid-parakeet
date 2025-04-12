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
        startMenuElement.makeLobbyButton.onClick.AddListener(GotoLobbyMaker);
        startMenuElement.joinLobbyButton.onClick.AddListener(GotoJoinLobby);
        startMenuElement.OnSetSneedButton += OnSetSneed;
        startMenuElement.OnSetContractButton += OnSetContract;
        startMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
        startMenuElement.OnCancelButton += CloseMenu;
        startMenuElement.OnViewLobbyButton += ViewLobby;
        lobbyMakerElement.backButton.onClick.AddListener(GotoStartMenu);
        lobbyMakerElement.makeLobbyButton.onClick.AddListener(OnMakeLobbyButton);
        lobbyViewerElement.OnDeleteButton += OnDeleteLobbyButton;
        lobbyViewerElement.OnBackButton += GotoStartMenu;
        lobbyViewerElement.OnRefreshButton += RefreshNetworkState;
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
            ShowElement(startMenuElement);
            _ = UpdateNetworkState();
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

    void GotoStartMenu()
    {
        ShowElement(startMenuElement);
    }

    void GotoJoinLobby()
    {
        ShowElement(lobbyJoinerElement);
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
    void GotoLobbyViewer()
    {
        ShowElement(lobbyViewerElement);
    }
    
    void GotoWaiting()
    {
        ShowElement(waitingElement);
    }
    
    async void OnMakeLobbyButton()
    {
        Blocker(true);
        Contract.LobbyParameters parameters = lobbyMakerElement.GetLobbyParameters();
        (int code, Lobby? lobby) = await StellarManagerTest.MakeLobbyRequest(parameters);
        if (lobby != null)
        {
            Debug.Log(lobby.Value.index);
        }
        lobbyMakerElement.OnLobbyMade(code);
        _ = await StellarManagerTest.UpdateState();
        if (code == 0)
        {
            GotoLobbyViewer();
        }
        Blocker(false);
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