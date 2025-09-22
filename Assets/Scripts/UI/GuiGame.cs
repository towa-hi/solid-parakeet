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
    }

    void OnDestroy()
    {
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
    }

    void Update()
    {
        if (!isUpdating) return;
        if (!Application.isFocused) return;
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
        Debug.Log($"GuiGame.HandleClientModeChanged: mode={mode}");
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
        }
        else
        {
            // Clear current element pointer so re-entry will re-activate desired panel
            currentGameElement = null;
            busyCount = 0;
            ApplyBusy(false);
        }
    }

    public override void Refresh()
    {
        
    }

    void HandleTaskStarted(TaskInfo _)
    {
        busyCount++;
        ApplyBusy(true);
    }

    void HandleTaskEnded(TaskInfo _)
    {
        busyCount = Math.Max(0, busyCount - 1);
        if (busyCount == 0)
        {
            ApplyBusy(false);
        }
    }

    void ApplyBusy(bool isBusy)
    {
        Debug.Log($"applybusy {isBusy}");
        if (canvasGroup == null) return;
        canvasGroup.interactable = !isBusy;
        // Keep blocking raycasts so clicks don't pass through to world while UI is disabled
        canvasGroup.blocksRaycasts = true;
    }
}
