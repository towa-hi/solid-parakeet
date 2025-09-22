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
    
    void Start()
    {
        Debug.Log("GuiGame.Start()");
        gameOver.OnReturnClicked += ExitToMainMenu;
    }

    void OnEnable()
    {
        isUpdating = gameObject.activeInHierarchy;
        if (StellarManager.IsBusy)
        {
            busyCount = 1;
            ApplyBusy(true);
        }
#if USE_GAME_STORE
        ViewEventBus.OnSetupModeChanged += HandleSetupModeChanged;
#endif
    }

    void OnDisable()
    {
        busyCount = 0;
        ApplyBusy(false);
#if USE_GAME_STORE
        ViewEventBus.OnSetupModeChanged -= HandleSetupModeChanged;
#endif
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
    
    
    
    public void PhaseStateChanged(PhaseChangeSet changes)
    {
#if USE_GAME_STORE
        // In flagged builds, prefer mode-driven panel switching; PhaseChangeSet still forwarded for legacy
        if (changes.GetNetStateUpdated() is NetStateUpdated)
        {
            // Do not switch panels here; mode handler handles it
        }
        currentGameElement?.PhaseStateChanged(changes);
        return;
#endif
        if (changes.GetNetStateUpdated() is NetStateUpdated nsu)
        {
            PhaseBase phase = nsu.phase;
            GameElement desired = GetElementForPhase(phase);
            if (currentGameElement != null)
            {
                currentGameElement.ShowElement(false);
            }
            currentGameElement = desired;
            // One-time initialization hooks on enter
            if (desired == resolve)
            {
                resolve.arenaController = ArenaController.instance;
                resolve.Initialize(nsu.phase.cachedNetState);
            }
            currentGameElement.ShowElement(true);
        }
        currentGameElement?.PhaseStateChanged(changes);
    }

    GameElement GetElementForPhase(PhaseBase phase)
    {
        if (phase is SetupCommitPhase) return setup;
        if (phase is MoveCommitPhase || phase is MoveProvePhase || phase is RankProvePhase) return movement;
        if (phase is ResolvePhase) return resolve;
        if (phase is FinishedPhase) return gameOver;
        return movement;
    }

    void ExitToMainMenu()
    {
        menuController.ExitGame();
    }

#if USE_GAME_STORE
    void HandleSetupModeChanged(bool active)
    {
        // Mode-driven panel switching in flagged builds
        GameElement desired = active ? (GameElement)setup : movement;
        if (currentGameElement == desired)
        {
            return; // already showing correct panel; avoid disabling/enabling and uninitializing
        }
        if (currentGameElement != null) currentGameElement.ShowElement(false);
        currentGameElement = desired;
        currentGameElement.ShowElement(true);
    }
#endif

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
