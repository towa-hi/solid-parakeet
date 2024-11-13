using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

public class GuiManager : MonoBehaviour
{
    public GuiStartMenu startMenu;
    public GuiMainMenu mainMenu;
    public GuiSettingsMenu settingsMenu;
    public GuiLobbySetupMenu lobbySetupMenu;
    public GuiLobbyMenu lobbyMenu;
    public GuiGame gameOverlay;
    public GuiNicknameModal nicknameModal;
    public GuiPasswordModal passwordModal;
    
    public GameObject modalPanel;

    [SerializeField] GuiElement currentMenu;
    [SerializeField] ModalElement currentModal;
    
    public void Initialize()
    {
        InitializeUIEvents();
        Debug.Log("GuiManager: Showing start menu.");
        GameManager.instance.OnClientChanged += OnClientChanged;
        ShowMenu(startMenu);
    }

    // Initialize UI event handlers
    void InitializeUIEvents()
    {
        // Start menu events
        startMenu.OnConnectButton += OnConnectButton;
        startMenu.OnOfflineButton += OnOfflineButton;
        // Main menu events
        mainMenu.OnChangeNicknameButton += OnChangeNicknameButton;
        mainMenu.OnNewLobbyButton += OnNewLobbyButton;
        mainMenu.OnJoinLobbyButton += OnJoinLobbyButton;
        mainMenu.OnSettingsButton += OnSettingsButton;
        mainMenu.OnExitButton += OnExitButton;
        // Settings menu events
        settingsMenu.OnCancelChangesButton += OnCancelChangesButton;
        settingsMenu.OnSaveSettingsButton += OnSaveSettingsButton;
        // Lobby setup menu events
        lobbySetupMenu.OnCancelButton += OnLobbySetupCancelButton;
        lobbySetupMenu.OnStartButton += OnLobbySetupStartButton;
        // Lobby menu events
        lobbyMenu.OnCancelButton += OnLobbyCancelButton;
        lobbyMenu.OnReadyButton += OnLobbyReadyButton;
        lobbyMenu.OnDemoButton += OnLobbyDemoButton;
        // Nickname modal events
        nicknameModal.OnCancel += OnNicknameModalCancel;
        nicknameModal.OnConfirm += OnNicknameModalConfirm;
        // Password modal events
        passwordModal.OnCancel += OnPasswordModalCancel;
        passwordModal.OnConfirm += OnPasswordModalConfirm;
    }

    void OnClientChanged(IGameClient oldClient, IGameClient newClient)
    {
        // Unsubscribe from previous client's events if necessary
        if (oldClient != null)
        {
            UnsubscribeClientEvents(oldClient);
        }
        SubscribeClientEvents(newClient);
    }

    void SubscribeClientEvents(IGameClient client)
    {
        client.OnRegisterClientResponse += OnRegisterClientResponse;
        client.OnDisconnect += OnDisconnect;
        client.OnErrorResponse += OnErrorResponse;
        client.OnRegisterNicknameResponse += OnRegisterNicknameResponse;
        client.OnGameLobbyResponse += OnGameLobbyResponse;
        client.OnLeaveGameLobbyResponse += OnLeaveGameLobbyResponse;
        client.OnReadyLobbyResponse += OnReadyLobbyResponse;
        client.OnDemoStarted += OnDemoStartedResponse;
    }

    void UnsubscribeClientEvents(IGameClient client)
    {
        client.OnRegisterClientResponse -= OnRegisterClientResponse;
        client.OnDisconnect -= OnDisconnect;
        client.OnErrorResponse -= OnErrorResponse;
        client.OnRegisterNicknameResponse -= OnRegisterNicknameResponse;
        client.OnGameLobbyResponse -= OnGameLobbyResponse;
        client.OnLeaveGameLobbyResponse -= OnLeaveGameLobbyResponse;
        client.OnReadyLobbyResponse -= OnReadyLobbyResponse;
        client.OnDemoStarted -= OnDemoStartedResponse;
    }
    
    void OnRegisterClientResponse(Response<string> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            startMenu.EnableElement(true);
            if (response.success)
            {
                ShowMenu(mainMenu);
            }
        });
    }

    void OnDisconnect(Response<string> response)
    {
        // reset the menu state 
    }

    void OnErrorResponse(ResponseBase response)
    {
        // reset the menu state 
    }

    void OnRegisterNicknameResponse(Response<string> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            mainMenu.EnableElement(true);
            if (response.success)
            {
                if (currentModal == nicknameModal)
                {
                    CloseCurrentModal();
                }
                mainMenu.RefreshNicknameText();
            }
        });
    }

    void OnGameLobbyResponse(Response<SLobby> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            mainMenu.EnableElement(true);
            if (response.success)
            {
                SLobby lobby = response.data;
                lobbyMenu.SetLobby(lobby);
                ShowMenu(lobbyMenu);
            }
        });
        
    }

    void OnLeaveGameLobbyResponse(Response<string> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            lobbyMenu.EnableElement(true);
            ShowMenu(mainMenu);
        });
        
    }

    void OnReadyLobbyResponse(Response<SLobby> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            lobbyMenu.EnableElement(true);
            lobbyMenu.SetLobby(response.data);
        });
    }

    void OnDemoStartedResponse()
    {
        lobbyMenu.EnableElement(true);
        Debug.Log("OnDemoStarted");
        ShowMenu(null);
        // this should be a fully fledged response
        SetupParameters setupParameters = new SetupParameters();
        GameManager.instance.boardManager.StartBoardSetup(Player.RED, setupParameters);
        gameOverlay.ShowElement(true);
        gameOverlay.InitializeSetup(setupParameters);
    }
    
    // start menu
    
    void OnConnectButton()
    {
        Debug.Log("OnConnectButton");
        startMenu.EnableElement(false);
        GameManager.instance.SetOfflineMode(false);
        _ = GameManager.instance.client.ConnectToServer();
    }
    
    void OnOfflineButton()
    {
        Debug.Log("OnOfflineButton");
        startMenu.EnableElement(false);
        GameManager.instance.SetOfflineMode(true);
        _ = GameManager.instance.client.ConnectToServer();
    }
    
    // main menu
    
    void OnChangeNicknameButton()
    {
        Debug.Log("OnChangeNicknameButton");
        ShowModal(nicknameModal);
    }

    void OnNewLobbyButton()
    {
        Debug.Log("OnNewLobbyButton");
        ShowMenu(lobbySetupMenu);
    }

    void OnJoinLobbyButton()
    {
        Debug.Log("OnJoinLobbyButton");
        ShowModal(passwordModal);
    }

    void OnSettingsButton()
    {
        Debug.Log("OnSettingsButton");
        ShowMenu(settingsMenu);
    }

    void OnExitButton()
    {
        Debug.Log("OnExitButton");
        GameManager.instance.QuitGame();
    }

    // settings menu
    
    void OnCancelChangesButton()
    {
        Debug.Log("OnCancelChangesButton");
        ShowMenu(mainMenu);
    }
    void OnSaveSettingsButton()
    {
        Debug.Log("OnSaveSettingsButton");
        ShowMenu(mainMenu);
    }

    // lobby setup menu
    
    void OnLobbySetupCancelButton()
    {
        Debug.Log("OnLobbySetupCancelButton");
        ShowMenu(mainMenu);
    }

    void OnLobbySetupStartButton()
    {
        Debug.Log("OnLobbySetupStartButton");
        mainMenu.EnableElement(false);
        _ = GameManager.instance.client.SendGameLobby();
        
    }

    // lobby menu
    
    void OnLobbyCancelButton()
    {
        Debug.Log("OnLobbyCancelButton");
        lobbyMenu.EnableElement(false);
        _ = GameManager.instance.client.SendGameLobbyLeaveRequest();
    }

    void OnLobbyReadyButton(bool ready)
    {
        Debug.Log("OnLobbyReadyButton");
        lobbyMenu.EnableElement(false);
        _ = GameManager.instance.client.SendGameLobbyReadyRequest(ready);
    }

    void OnLobbyDemoButton()
    {
        Debug.Log("OnLobbyDemoButton");
        lobbyMenu.EnableElement(false);
        _ = GameManager.instance.client.StartGameDemoRequest();
    }
    
    // nickname modal
    
    void OnNicknameModalCancel()
    {
        Debug.Log("OnNicknameModalCancel");
        if (currentModal == nicknameModal)
        {
            CloseCurrentModal();
        }
    }
    
    void OnNicknameModalConfirm()
    {
        Debug.Log("OnNicknameModalConfirm");
        if (currentModal == nicknameModal)
        {
            nicknameModal.EnableElement(false);
            GameManager.instance.client.SendRegisterNickname(nicknameModal.GetNickname());
        }
    }
    
    // password modal

    void OnPasswordModalCancel()
    {
        Debug.Log("OnPasswordModalCancel");
        if (currentModal == passwordModal)
        {
            CloseCurrentModal();
        }
    }

    void OnPasswordModalConfirm()
    {
        Debug.Log("OnPasswordModalConfirm");
        if (currentModal == passwordModal)
        {
            // network send request
            CloseCurrentModal();
            ShowMenu(lobbyMenu);
        }
    }

    void ShowMenu(MenuElement menu)
    {
        if (currentMenu != null)
        {
            currentMenu.ShowElement(false);
            Debug.Log($"{currentMenu} is hidden");
        }
        currentMenu = menu;
        if (currentMenu != null)
        {
            menu.ShowElement(true);
        }
    }
    
    void ShowModal(ModalElement modal)
    {
        if (currentModal != null && currentModal != modal)
        {
            CloseCurrentModal();
        }
        modalPanel.SetActive(true);
        modal.ShowElement(true);
        currentModal = modal;
    }

    void CloseCurrentModal()
    {
        if (currentModal == null) return;
        currentModal.ShowElement(false);
        modalPanel.SetActive(false);
        currentModal = null;
    }

}
