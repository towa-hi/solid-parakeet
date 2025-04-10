using System;
using UnityEngine;

public class GuiManager : MonoBehaviour
{
    public GuiStartMenu startMenu;
    public GuiNetworkMenu networkMenu;
    public GuiMainMenu mainMenu;
    public GuiSettingsMenu settingsMenu;
    public GuiLobbySetupMenu lobbySetupMenu;
    public GuiLobbyMenu lobbyMenu;
    public GuiGame gameOverlay;
    public GuiNicknameModal nicknameModal;
    public GuiPasswordModal passwordModal;
    public GuiTestMenuController testMenuController;
    
    public GameObject modalPanel;

    [SerializeField] GuiElement currentMenu;
    [SerializeField] ModalElement currentModal;

    public event Action<MenuElement> OnShowMenu;

    public bool isTransitioning;
    public void Initialize()
    {
        InitializeUIEvents();
        Debug.Log("GuiManager: Showing start menu.");
        ShowMenu(startMenu);
    }

    // Initialize UI event handlers
    void InitializeUIEvents()
    {
        // Start menu events
        startMenu.OnConnectButton += OnConnectButton;
        startMenu.OnOfflineButton += OnOfflineButton;
        // Network menu events
        networkMenu.OnConnectWalletButton += OnConnectWalletButton;
        networkMenu.OnTestButton += OnTestButton;
        networkMenu.OnBackButton += OnNetworkBackButton;
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

    
    public void OnRegisterClientResponse(Response<string> response)
    {
        if (response.success)
        {
            ShowMenu(mainMenu);
        }
    }

    public void OnDisconnect(Response<string> response)
    {
        ShowMenu(startMenu);
    }

    public void OnErrorResponse(ResponseBase response)
    {
        ShowMenu(startMenu);
    }

    public void OnRegisterNicknameResponse(Response<string> response)
    {
        if (response.success)
        {
            if (currentModal == nicknameModal)
            {
                CloseCurrentModal();
            }
            mainMenu.RefreshNicknameText();
        }
    }

    public void OnGameLobbyResponse(Response<SLobby> response)
    {
        if (response.success)
        {
            SLobby lobby = response.data;
            lobbyMenu.SetLobby(lobby);
            ShowMenu(lobbyMenu);
        }
    }

    public void OnLeaveGameLobbyResponse(Response<string> response)
    {
        ShowMenu(mainMenu);
    }

    public void OnReadyLobbyResponse(Response<SLobby> response)
    {
        lobbyMenu.SetLobby(response.data);
    }

    public void OnDemoStartedResponse(Response<SLobbyParameters> response)
    {
        Debug.Log("GuiManager: OnDemoStarted");
        ShowMenu(gameOverlay);
    }
    
    // start menu
    
    void OnConnectButton()
    {
        Debug.Log("OnConnectButton");
        ShowMenu(testMenuController);
    }
    
    void OnOfflineButton()
    {
        Debug.Log("OnOfflineButton");
        GameManager.instance.SetOfflineMode();
        GameManager.instance.Lightning();
    }
    
    // network menu

    void OnConnectWalletButton()
    {
        Debug.Log("OnConnectWalletButton");
    }

    void OnTestButton()
    {
        Debug.Log("OnTestButton");
    }

    void OnNetworkBackButton()
    {
        Debug.Log("OnNetworkBackButton");
        ShowMenu(startMenu);
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

    void OnLobbySetupStartButton(LobbyParameters lobbyParameters)
    {
        Debug.Log("OnLobbySetupStartButton");
        GameManager.instance.client.SendGameLobby(new SLobbyParameters(lobbyParameters));
        
    }

    // lobby menu
    
    void OnLobbyCancelButton()
    {
        Debug.Log("OnLobbyCancelButton");
        GameManager.instance.client.SendGameLobbyLeaveRequest();
    }

    void OnLobbyReadyButton(bool ready)
    {
        Debug.Log("OnLobbyReadyButton");
        GameManager.instance.client.SendGameLobbyReadyRequest(ready);
    }

    void OnLobbyDemoButton()
    {
        Debug.Log("OnLobbyDemoButton");
        GameManager.instance.client.SendStartGameDemoRequest();
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

    MenuElement nextMenu;

    public void OnTransitionFinished()
    {
        if (nextMenu != null)
        {
            currentMenu = nextMenu;
            currentMenu.ShowElement(true);
            Debug.Log($"{currentMenu} is shown");
        }
        nextMenu = null;
        isTransitioning = false;
    }
    
    public void ShowMenu(MenuElement menu)
    {
        if (currentMenu != null)
        {
            currentMenu.ShowElement(false);
            Debug.Log($"{currentMenu} is hidden");
        }
        else
        {
            currentMenu = menu;
            currentMenu.ShowElement(true);
            Debug.Log($"Started with {currentMenu}");
        }
        nextMenu = menu;
        isTransitioning = true;
        OnShowMenu?.Invoke(menu);
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
