using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contract;
using UnityEngine;

public class GuiTestMenuController : MenuElement
{
    public StellarManagerTest stellar;
    
    public GuiTestStartMenu startMenuElement;
    public GuiTestLobbyMaker lobbyMakerElement;
    public GuiTestInviteList inviteListElement;
    public GuiTestWaiting waitingElement;
    public GameObject blocker;
    // state
    public TestGuiElement currentElement;
    public string currentProcedure;
    
    void Start()
    {
        startMenuElement.makeInviteButton.onClick.AddListener(GotoLobbyMaker);
        lobbyMakerElement.backButton.onClick.AddListener(GotoStartMenu);
        lobbyMakerElement.makeLobbyButton.onClick.AddListener(OnMakeLobbyButton);
        lobbyMakerElement.deleteLobbyButton.onClick.AddListener(OnDeleteLobbyButton);
        stellar.Initialize();
        Initialize();
    }


    public void Initialize()
    {
        startMenuElement.SetIsEnabled(false);
        lobbyMakerElement.SetIsEnabled(false);
        inviteListElement.SetIsEnabled(false);
        waitingElement.SetIsEnabled(false);
        
        ShowElement(startMenuElement);
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

    void GotoLobbyMaker()
    {
        ShowElement(lobbyMakerElement);
    }

    void GotoStartMenu()
    {
        ShowElement(startMenuElement);
    }

    void GotoWaiting()
    {
        ShowElement(waitingElement);
    }
    
    async void OnMakeLobbyButton()
    {
        Blocker(true);
        Contract.LobbyParameters parameters = lobbyMakerElement.GetLobbyParameters();
        (int code, Lobby? lobby) = await stellar.MakeLobbyRequest(parameters);
        if (lobby != null)
        {
            Debug.Log(lobby.Value.index);
        }
        Blocker(false);
        lobbyMakerElement.OnLobbyMade(code);
    }

    async void OnDeleteLobbyButton()
    {
        Blocker(true);
        int code = await stellar.LeaveLobbyRequest();
        Debug.Log(code);
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