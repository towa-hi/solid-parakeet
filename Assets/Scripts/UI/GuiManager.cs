using UnityEngine;

public class GuiManager : MonoBehaviour
{
    public static GuiManager instance;
    
    public GuiStartMenu startMenu;
    public GuiMainMenu mainMenu;
    public GuiSettingsMenu settingsMenu;
    public GuiLobbySetupMenu lobbySetupMenu;
    public GuiLobbyMenu lobbyMenu;
    
    public GuiNicknameModal nicknameModal;
    public GuiPasswordModal passwordModal;
    
    public GameObject modalPanel;

    GuiElement currentMenu;
    ModalElement currentModal;
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // start menu events
            startMenu.OnConnectButton += OnConnectButton;
            startMenu.OnOfflineButton += OnOfflineButton;
            // main menu events
            mainMenu.OnChangeNicknameButton += OnChangeNicknameButton;
            mainMenu.OnNewLobbyButton += OnNewLobbyButton;
            mainMenu.OnJoinLobbyButton += OnJoinLobbyButton;
            mainMenu.OnSettingsButton += OnSettingsButton;
            mainMenu.OnExitButton += OnExitButton;
            // settings menu events
            settingsMenu.OnCancelChangesButton += OnCancelChangesButton;
            settingsMenu.OnSaveSettingsButton += OnSaveSettingsButton;
            // lobby setup menu events
            lobbySetupMenu.OnCancelButton += OnLobbySetupCancelButton;
            lobbySetupMenu.OnStartButton += OnLobbySetupStartButton;
            // lobby menu events
            lobbyMenu.OnCancelButton += OnLobbyCancelButton;
            lobbyMenu.OnReadyButton += OnLobbyReadyButton;
            // nickname modal events
            nicknameModal.OnCancel += OnNicknameModalCancel;
            nicknameModal.OnConfirm += OnNicknameModalConfirm;
            // password modal events
            passwordModal.OnCancel += OnPasswordModalCancel;
            passwordModal.OnConfirm += OnPasswordModalConfirm;
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
    }

    void Start()
    {
        Debug.Log("showing startmenu");
        ShowMenu(startMenu);
    }

    // start menu
    
    void OnConnectButton()
    {
        Debug.Log("OnConnectButton");
        ShowMenu(mainMenu);
    }

    void OnOfflineButton()
    {
        Debug.Log("OnOfflineButton");
        ShowMenu(mainMenu);
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
        return;
    }

    void OnJoinLobbyButton()
    {
        Debug.Log("OnJoinLobbyButton");
        ShowModal(passwordModal);
        return;
    }

    void OnSettingsButton()
    {
        Debug.Log("OnSettingsButton");
        ShowMenu(settingsMenu);
        return;
    }

    void OnExitButton()
    {
        Debug.Log("OnExitButton");
        return;
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
        ShowMenu(lobbyMenu);
    }

    // lobby menu
    
    void OnLobbyCancelButton()
    {
        Debug.Log("OnLobbyCancelButton");
        ShowMenu(mainMenu);
    }

    void OnLobbyReadyButton()
    {
        Debug.Log("OnLobbyReadyButton");
        // start game
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
            // await network stuff
            // if ok close current modal
            CloseCurrentModal();
            
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
            // await network stuff
            // if ok, go to lobby menu 
            CloseCurrentModal();
            ShowMenu(lobbyMenu);
        }
    }

    void ShowMenu(MenuElement menu)
    {
        if (currentMenu != null && currentMenu != menu)
        {
            CloseCurrentMenu();
        }
        menu.ShowElement(true);
        currentMenu = menu;
    }

    void CloseCurrentMenu()
    {
        if (currentMenu == null) return;
        currentMenu.ShowElement(false);
        currentMenu = null;
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
