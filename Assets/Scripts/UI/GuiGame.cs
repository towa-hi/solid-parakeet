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
    public MenuController injectedMenuController;

    public event Action EscapePressed;
    
    void Start()
    {
        Debug.Log("GuiGame.Start()");
    }

    void OnEnable()
    {
        isUpdating = gameObject.activeInHierarchy;
        // StellarManager.OnTaskStarted += HandleTaskStarted;
        // StellarManager.OnTaskEnded += HandleTaskEnded;
        // Reflect current busy state if we were enabled mid-task
        if (StellarManager.IsBusy)
        {
            busyCount = 1;
            ApplyBusy(true);
        }
    }

    void OnDisable()
    {
        // StellarManager.OnTaskStarted -= HandleTaskStarted;
        // StellarManager.OnTaskEnded -= HandleTaskEnded;
        busyCount = 0;
        ApplyBusy(false);
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
        // Switch active element only when the phase object changes, then forward to the active element
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
        if (injectedMenuController == null) { Debug.LogError("GuiGame: injected MenuController is null for ExitToMainMenu()"); return; }
        injectedMenuController.ExitGame();
    }

    public void SetMenuController(MenuController controller)
    {
        injectedMenuController = controller;
        gameOver.SetMenuController(controller);
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
