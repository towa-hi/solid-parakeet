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
    public GameObject modalLayer;
    public GameObject modalEscapePrefab;
    public GameObject modalSettingsPrefab;
    // state
    public MenuElement currentElement;
    public Stack<ModalElement> modalStack;
    void Start()
    {
        modalStack = new();
        StellarManager.Initialize();
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
        StellarManager.OnTaskStarted += ShowTopBar;
        StellarManager.OnTaskEnded += HideTopBar;
        
        startMenuElement.OnStartButton += GotoMainMenu;
        
        mainMenuElement.OnJoinLobbyButton += GotoJoinLobby;
        mainMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
        mainMenuElement.OnSettingsButton += OpenSettingsModal;
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

        gameElement.movement.OnMenuButton = OpenEscapeModal;
        gameElement.EscapePressed += OpenEscapeModal;
    }

    void OnNetworkStateUpdated()
    {
        Debug.Log("GuiMenuController OnNetworkStateUpdated");
        currentElement?.Refresh();
    }

    public void Initialize()
    {
        startMenuElement.ShowElement(false);
        mainMenuElement.ShowElement(false);
        lobbyMakerElement.ShowElement(false);
        lobbyJoinerElement.ShowElement(false);
        lobbyViewerElement.ShowElement(false);
        gameElement.ShowElement(false);
        walletElement.ShowElement(false);
        GotoStartMenu();
    }
    
    void ShowMenuElement(MenuElement element)
    {
        if (currentElement != null)
        {
            currentElement.ShowElement(false);
        }
        currentElement = element;
        currentElement.ShowElement(true);
        currentElement.Refresh();
    }

    async void GotoLobbyMaker()
    {
        GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_ALTAR, false);
        await StellarManager.UpdateState();
        ShowMenuElement(lobbyMakerElement);
    }

    void OpenSettingsModal()
    {
        OpenModal(modalSettingsPrefab);
        
        
    }

    void OpenEscapeModal()
    {
        if (modalStack.TryPeek(out ModalElement topModal))
        {
            CloseModal();
        }
        else
        {
            OpenModal(modalEscapePrefab);
        }
    }

    void ResignGame()
    {
        
    }

    void OpenModal(GameObject modalPrefab)
    {
        foreach (ModalElement element in modalStack)
        {
            element.OnFocus(false);
        }
        ModalElement topModal = Instantiate(modalPrefab, modalLayer.transform).GetComponent<ModalElement>();
        modalStack.Push(topModal);
        topModal.OnFocus(true);
        SetModalEvents(topModal, true);
    }
    void CloseModal()
    {
        if (modalStack.Count > 0)
        {
            ModalElement modal = modalStack.Pop();
            modal.OnFocus(false);
            SetModalEvents(modal, false);
            Destroy(modal.gameObject);
            if (modalStack.TryPeek(out ModalElement topModal))
            {
                topModal.OnFocus(true);
                SetModalEvents(topModal, true);
            }
        }
    }

    void CloseAllModals()
    {
        while (modalStack.Count > 0)
        {
            CloseModal();
        }
    }
    void SetModalEvents(ModalElement modal, bool set)
    {
        switch (modal)
        {
            case ModalEscape modalEscape:
                modalEscape.OnSettingsButton = set ? OpenSettingsModal : null;
                modalEscape.OnBackButton = set ? CloseModal : null;
                modalEscape.OnMainMenuButton = set ? GotoMainMenu : null;
                modalEscape.OnResignButton = set ? ResignGame : null;
                break;
            case ModalSettings modalSettings:
                modalSettings.OnBackButton = set ? CloseModal : null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(modal));

        }
    }
    void GotoStartMenu()
    {
        ShowMenuElement(startMenuElement);
    }
    
    async void GotoMainMenu()
    {
        CloseAllModals();
        if (GameManager.instance.boardManager.initialized)
        {
            // Ensure any in-flight Stellar task is dropped before leaving the game
            StellarManager.AbortCurrentTask();
            GameManager.instance.boardManager.CloseBoardManager();
        }
        GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_INNER, false);
        await StellarManager.UpdateState();
        ShowMenuElement(mainMenuElement);
    }

    async void GotoJoinLobby()
    {
        await StellarManager.UpdateState();
        ShowMenuElement(lobbyJoinerElement);
    }

    void GotoWallet()
    {
        ShowMenuElement(walletElement);
        GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_DUNGEON, false);
    }

    void CheckAssets()
    {
        _ = StellarManager.GetAssets(StellarManager.GetUserAddress());
    }
    
    async void ViewLobby()
    {
        GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_ALTAR, false);
        await StellarManager.UpdateState();
        ShowMenuElement(lobbyViewerElement);
    }

    async void JoinLobby(LobbyId lobbyId)
    {
        int code = await StellarManager.JoinLobbyRequest(lobbyId);
        await StellarManager.UpdateState();
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement);
        }
    }

    async void OnStartGame()
    {
        await StellarManager.UpdateState();
        if (StellarManager.networkState.inLobby)
        {
            ShowMenuElement(gameElement);
            GameManager.instance.boardManager.StartBoardManager();
        }
    }
    
    async void OnSubmitLobbyButton(LobbyParameters parameters)
    {
        int code = await StellarManager.MakeLobbyRequest(parameters);
        if (code == 0)
        {
            ShowMenuElement(lobbyViewerElement);
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
        ShowMenuElement(mainMenuElement);
    }
    
    async void RefreshNetworkState()
    {
        currentElement?.EnableInput(false);
        await StellarManager.UpdateState();
        currentElement?.Refresh();
    }

    void ShowTopBar(TaskInfo task)
    {
        currentElement?.EnableInput(false);
        topBar.Show(true);
        string address = StellarManager.GetUserAddress();
        Color backgroundColor = address == StellarManager.GetHostAddress() ? Color.red : Color.blue;
        topBar.SetView(backgroundColor, task.taskMessage);
        
    }
    
    void HideTopBar(TaskInfo task)
    {
        
        currentElement?.EnableInput(true);
        topBar.Show(false);
    }
    
    
}

public abstract class MenuElement: MonoBehaviour
{
    public CanvasGroup canvasGroup;
    
    public virtual void ShowElement(bool show)
    {
        gameObject.SetActive(show);
    }

    public virtual void EnableInput(bool input)
    {
        canvasGroup.interactable = input;
    }

    public abstract void Refresh();
}

public class GameElement: MonoBehaviour
{

    public virtual void ShowElement(bool show)
    {
        gameObject.SetActive(show);
    }
    
}

public class ModalElement : MonoBehaviour
{
    public GameObject blockerPanel;
    public CanvasGroup canvasGroup;
    
    public virtual void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
    }
}