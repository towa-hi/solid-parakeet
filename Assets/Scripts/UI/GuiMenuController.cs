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
// planned to be deprecated!
public class GuiMenuController: MonoBehaviour
{
	public GuiStartMenu startMenuElement;
	public GuiMainMenu mainMenuElement;
	public GuiLobbyMaker lobbyMakerElement;
	public GuiLobbyViewer lobbyViewerElement;
	public GuiLobbyJoiner lobbyJoinerElement;
	public GuiWallet walletElement;
	public GuiGallery galleryElement;
	public GuiGame gameElement;
	public TopBar topBar;
	public GameObject modalLayer;
	public GameObject modalEscapePrefab;
	public GameObject modalSettingsPrefab;
    public GameObject modalConnectPrefab;
	public GameObject modalErrorPrefab;
	// state
	public MenuElement currentElement;
	public Stack<ModalElement> modalStack;
	void Start()
	{
		modalStack = new();
		StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
		StellarManager.OnTaskStarted += ShowTopBar;
		StellarManager.OnTaskEnded += HideTopBar;
		
		startMenuElement.OnStartButton += OnStartButton;
		startMenuElement.OnStartOfflineButton += OnStartOfflineButton;
		
		mainMenuElement.OnJoinLobbyButton += GotoJoinLobby;
		mainMenuElement.OnMakeLobbyButton += GotoLobbyMaker;
		mainMenuElement.OnSettingsButton += OpenSettingsModal;
		mainMenuElement.OnViewLobbyButton += ViewLobby;
		//mainMenuElement.OnWalletButton += GotoWallet;
		mainMenuElement.OnWalletButton += GotoGallery;
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
		gameElement.resolve.OnMenuButton = OpenEscapeModal;
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
		galleryElement.ShowElement(false);
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
		Result<bool> stateRes = await StellarManager.UpdateState();
		if (stateRes.IsError && (stateRes.Code is StatusCode.NETWORK_ERROR or StatusCode.TIMEOUT))
		{
			GameManager.instance.OfflineMode();
			OpenErrorModal("Network Unavailable", "You're now in Offline Mode.");
			GotoStartMenu();
			return;
		}
		ShowMenuElement(lobbyMakerElement);
	}

	async void OnStartButton()
	{
        Debug.Log("wew");
        OpenModal(modalConnectPrefab);
        Debug.Log("OnStartButton done");
	}

	async void OnStartOfflineButton()
	{
        GameManager.instance.OfflineMode();
        GotoMainMenu();
        Debug.Log("OnStartOfflineButton done");
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
			case ModalConnect modalConnect:
				modalConnect.OnCloseButton = set ? CloseModal : null;
				modalConnect.OnConnectButton = set ? OnConnectButton : null;
				break;
			case ModalError modalError:
				modalError.OnCloseButton = set ? CloseModal : null;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(modal));

		}
	}

    async void OnConnectButton(ModalConnectData data)
    {
        Result<bool> result = await GameManager.instance.ConnectToNetwork(data);
        if (!result.IsOk)
        {
            string message = string.IsNullOrEmpty(result.Message) ? FormatStatusMessage(result.Code) : result.Message;
            OpenErrorModal("Connection Failed", message);
            return;
        }
        GotoMainMenu();
    }

	public void GotoStartMenu()
	{
		ShowMenuElement(startMenuElement);
	}
	
	void GotoMainMenu()
	{
		CloseAllModals();
		if (GameManager.instance.boardManager.initialized)
		{
			// Ensure any in-flight Stellar task is dropped before leaving the game
			StellarManager.AbortCurrentTask();
			GameManager.instance.boardManager.CloseBoardManager();
		}
		GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_INNER, false);
		ShowMenuElement(mainMenuElement);
	}

	async void GotoJoinLobby()
	{
		Result<bool> stateRes = await StellarManager.UpdateState();
		if (stateRes.IsError && (stateRes.Code is StatusCode.NETWORK_ERROR or StatusCode.TIMEOUT))
		{
			GameManager.instance.OfflineMode();
			OpenErrorModal("Network Unavailable", "You're now in Offline Mode.");
			GotoStartMenu();
			return;
		}
		ShowMenuElement(lobbyJoinerElement);
	}

	void GotoWallet()
	{
		ShowMenuElement(walletElement);
		GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_DUNGEON, false);
	}

	void GotoGallery()
	{
		ShowMenuElement(galleryElement);
		GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_DUNGEON, false);

	}
	void CheckAssets()
	{
		_ = StellarManager.GetAssets(StellarManager.GetUserAddress());
	}
	
	async void ViewLobby()
	{
		GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_ALTAR, false);
		Result<bool> stateRes = await StellarManager.UpdateState();
		if (stateRes.IsError && (stateRes.Code is StatusCode.NETWORK_ERROR or StatusCode.TIMEOUT))
		{
			GameManager.instance.OfflineMode();
			OpenErrorModal("Network Unavailable", "You're now in Offline Mode.");
			GotoStartMenu();
			return;
		}
		// In offline mode, go straight back into the game if a local lobby exists
		if (!GameManager.instance.IsOnline() && StellarManager.networkState.inLobby)
		{
			ShowMenuElement(gameElement);
			GameManager.instance.boardManager.StartBoardManager();
			return;
		}
		ShowMenuElement(lobbyViewerElement);
	}

	async void JoinLobby(LobbyId lobbyId)
	{
		var resultCode = await StellarManager.JoinLobbyRequest(lobbyId);
		await StellarManager.UpdateState();
		if (resultCode.IsOk)
		{
			ShowMenuElement(lobbyViewerElement);
		}
		else
		{
			OpenErrorModal("Join Lobby Failed", FormatStatusMessage(resultCode.Code));
		}
	}

	async void OnStartGame()
	{
		Result<bool> stateRes = await StellarManager.UpdateState();
		if (stateRes.IsError && (stateRes.Code is StatusCode.NETWORK_ERROR or StatusCode.TIMEOUT))
		{
			GameManager.instance.OfflineMode();
			OpenErrorModal("Network Unavailable", "You're now in Offline Mode.");
			GotoStartMenu();
			return;
		}
		if (StellarManager.networkState.inLobby)
		{
			ShowMenuElement(gameElement);
			GameManager.instance.boardManager.StartBoardManager();
		}
	}
	
	async void OnSubmitLobbyButton(LobbyParameters parameters)
	{
		var resultCode = await StellarManager.MakeLobbyRequest(parameters);
		if (resultCode.IsOk)
		{
			ShowMenuElement(lobbyViewerElement);
		}
		else
		{
			OpenErrorModal("Create Lobby Failed", FormatStatusMessage(resultCode.Code));
		}
	}

	
	async void StartSingleplayer(LobbyParameters parameters)
	{
		// Switch to offline mode
		GameManager.instance.OfflineMode();
		// Create local lobby (offline branch handles host make + guest join)
		await StellarManager.MakeLobbyRequest(parameters);
		// Refresh local network state
		await StellarManager.UpdateState();
		// Enter game
		if (StellarManager.networkState.inLobby)
		{
			ShowMenuElement(gameElement);
			GameManager.instance.boardManager.StartBoardManager();
		}
	}
	async void DeleteLobby()
	{
		var resultCode = await StellarManager.LeaveLobbyRequest();
		if (resultCode.IsOk)
		{
			ShowMenuElement(mainMenuElement);
		}
		else
		{
			OpenErrorModal("Leave Lobby Failed", FormatStatusMessage(resultCode.Code));
		}
	}
	
	async void RefreshNetworkState()
	{
		currentElement?.EnableInput(false);
		Result<bool> stateRes2 = await StellarManager.UpdateState();
		if (stateRes2.IsError && (stateRes2.Code is StatusCode.NETWORK_ERROR or StatusCode.TIMEOUT))
		{
			GameManager.instance.OfflineMode();
			OpenErrorModal("Network Unavailable", "You're now in Offline Mode.");
			GotoStartMenu();
			return;
		}
		currentElement?.Refresh();
	}

	public void OpenErrorModal(string title, string message)
	{
		OpenModal(modalErrorPrefab);
		if (modalStack.TryPeek(out ModalElement top))
		{
			if (top is ModalError modalError)
			{
				modalError.SetContent(title, message);
			}
		}
	}

	string FormatStatusMessage(StatusCode code)
	{
		switch (code)
		{
			case StatusCode.CONTRACT_ERROR: return "Contract error occurred.";
			case StatusCode.NETWORK_ERROR: return "Network error occurred.";
			case StatusCode.RPC_ERROR: return "RPC error occurred.";
			case StatusCode.TIMEOUT: return "The request timed out.";
			case StatusCode.OTHER_ERROR: return "An unexpected error occurred.";
			case StatusCode.SERIALIZATION_ERROR: return "Serialization error occurred.";
			case StatusCode.DESERIALIZATION_ERROR: return "Deserialization error occurred.";
			case StatusCode.TRANSACTION_FAILED: return "Transaction failed.";
			case StatusCode.TRANSACTION_NOT_FOUND: return "Transaction not found.";
			case StatusCode.TRANSACTION_TIMEOUT: return "Transaction timed out.";
			case StatusCode.ENTRY_NOT_FOUND: return "Required entry not found.";
			case StatusCode.SIMULATION_FAILED: return "Simulation failed.";
			case StatusCode.TRANSACTION_SEND_FAILED: return "Failed to send transaction.";
			default: return "Operation failed.";
		}
	}

	void ShowTopBar(TaskInfo task)
	{
		currentElement?.EnableInput(false);
		topBar.Show(true);
		string address = StellarManager.GetUserAddress();
		Color backgroundColor = Color.gray;
		if (address != null)
		{
			backgroundColor = address == StellarManager.GetHostAddress() ? Color.red : Color.blue;
		}
		topBar.SetView(backgroundColor, task.taskMessage);
	}
	
	void HideTopBar(TaskInfo task)
	{
		
		currentElement?.EnableInput(true);
		topBar.Show(false);
	}
	
	
}

// Ensure a CanvasGroup exists on all menu elements for consistent input control
[RequireComponent(typeof(CanvasGroup))]
public abstract class MenuElement: MonoBehaviour
{
	public CanvasGroup canvasGroup;

	protected virtual void Awake()
	{
		if (canvasGroup == null)
		{
			canvasGroup = GetComponent<CanvasGroup>();
		}
	}
	
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