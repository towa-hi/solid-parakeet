using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiGame : MenuElement
{
    public GuiSetup setup;
    public GuiMovement movement;
    public GuiResolve resolve;
    public ArenaController arenaController; // injected from BoardManager or scene
    public GuiGameOver gameOver; // overlay element like others (expecting GuiGameOver)
    
    public CameraAnchor boardAnchor;
    bool isUpdating;
    int busyCount;
    
    // Single active game element (state machine style)
    GameElement currentGameElement;

    // Injected reference from new MenuController pipeline
    public MenuController menuController;

    public event Action EscapePressed;
    
    protected override void Awake()
    {
        base.Awake();
        gameOver.OnReturnClicked += ExitToMainMenu;
        ViewEventBus.OnClientModeChanged += HandleClientModeChanged;
		// Drive interactivity from store-driven UI state updates
		ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    void OnDestroy()
    {
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
		ViewEventBus.OnStateUpdated -= HandleStateUpdated;
    }

    void Update()
    {
        if (!isUpdating) return;
        if (!Application.isFocused) return;
        if (WalletManager.IsWalletBusy) return;
        bool pressed = Globals.InputActions.Game.Escape.WasPressedThisFrame();
        if (pressed)
        {
            EscapePressed?.Invoke();
        }
    }
    
    void ExitToMainMenu()
    {
        menuController.ExitGame();
    }

    void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        Debug.Log($"[GuiGame] Begin HandleClientModeChanged mode={mode}");
        GameElement desired = mode switch
        {
            ClientMode.Setup => setup,
            ClientMode.Move => movement,
            ClientMode.Resolve => resolve,
            ClientMode.Finished or ClientMode.Aborted => gameOver,
            _ => movement,
        };
        if (currentGameElement == desired)
        {
            Debug.Log("[GuiGame] Early return; panel unchanged");
            return;
        }
        if (currentGameElement != null) currentGameElement.ShowElement(false);
        currentGameElement = desired;
        currentGameElement.ShowElement(true);
        // Initialize panel deterministically on mode change
        if (desired == setup)
        {
            setup.InitializeFromState(net, ui);
        }
        else if (desired == movement)
        {
            movement.InitializeFromState(net, ui);
        }
        else if (desired == resolve)
        {
            resolve.Initialize(net);
        }
        // Enable polling only during Setup and Move modes
        //StellarManager.SetPolling(mode == ClientMode.Setup || mode == ClientMode.Move);
        Debug.Log("[GuiGame] End HandleClientModeChanged");
    }

    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        isUpdating = show;
        if (show)
        {
            GameManager.instance.cameraManager.enableCameraMovement = true;
            GameManager.instance.cameraManager.MoveCameraTo(Area.BOARD, false);
            setup.ShowElement(false);
            movement.ShowElement(false);
            resolve.ShowElement(false);
            gameOver.ShowElement(false);
            Debug.Log("GuiGame.ShowElement(true): hiding subpanels by default (resolve hidden)");
            // Panel will be activated by ClientModeChanged shortly after; keep all hidden now
			// Initialize to interactable; upcoming state updates will adjust if waiting
			ApplyBusy(false);
        }
        else
        {
            // Clear current element pointer so re-entry will re-activate desired panel
            currentGameElement = null;
            ApplyBusy(false);
        }
    }

    public override void Refresh()
    {
        
    }

	void HandleStateUpdated(GameSnapshot snapshot)
	{
		Debug.Log($"[GuiGame] HandleStateUpdated begin mode={snapshot?.Mode} checkpoint={(snapshot?.Ui?.Checkpoint)} waiting={(snapshot?.Ui?.WaitingForResponse != null)}");
		bool waiting = snapshot?.Ui?.WaitingForResponse != null;
		ApplyBusy(waiting);
		Debug.Log("[GuiGame] HandleStateUpdated end");
	}

    void ApplyBusy(bool isBusy)
    {
        if (canvasGroup == null) return;
        bool old = canvasGroup.interactable;
        canvasGroup.interactable = !isBusy;
        if (old != canvasGroup.interactable)
        {
            Debug.Log($"ApplyBusy: interactable changed to {canvasGroup.interactable}");
        }
        // Keep blocking raycasts so clicks don't pass through to world while UI is disabled
        canvasGroup.blocksRaycasts = true;
    }
}
