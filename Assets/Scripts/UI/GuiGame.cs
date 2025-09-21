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
        // Centralize which Game GUI panel is visible based on the active phase type
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            // Initialize UI subsystems that depend on board parameters (e.g., arena)
            resolve.arenaController = ArenaController.instance;
            resolve.Initialize(netStateUpdated.phase.cachedNetState);
            PhaseBase phase = netStateUpdated.phase;
            bool showSetup = phase is SetupCommitPhase;
            bool showMovement = phase is MoveCommitPhase || phase is MoveProvePhase || phase is RankProvePhase;
            bool showResolve = phase is ResolvePhase;
            bool showGameOver = phase is FinishedPhase;
            if (setup.gameObject.activeSelf != showSetup) { setup.ShowElement(showSetup); }
            if (movement.gameObject.activeSelf != showMovement) { movement.ShowElement(showMovement); }
            if (resolve.gameObject.activeSelf != showResolve) { resolve.ShowElement(showResolve); }
            if (gameOver.gameObject.activeSelf != showGameOver) { gameOver.ShowElement(showGameOver); }
        }
        // Forward phase updates only to the relevant/visible panels
        if (changes.GetNetStateUpdated() is NetStateUpdated nsu)
        {
            PhaseBase phase = nsu.phase;
            bool isSetup = phase is SetupCommitPhase;
            bool isMovement = phase is MoveCommitPhase || phase is MoveProvePhase || phase is RankProvePhase;
            bool isResolve = phase is ResolvePhase;
            if (isSetup) { setup.PhaseStateChanged(changes); }
            if (isMovement) { movement.PhaseStateChanged(changes); }
            if (isResolve) { resolve.PhaseStateChanged(changes); }
        }
        else
        {
            // No phase change; forward conservatively to active panels only
            if (setup.gameObject.activeSelf) { setup.PhaseStateChanged(changes); }
            if (movement.gameObject.activeSelf) { movement.PhaseStateChanged(changes); }
            if (resolve.gameObject.activeSelf) { resolve.PhaseStateChanged(changes); }
            if (gameOver.gameObject.activeSelf) { gameOver.PhaseStateChanged(changes); }
        }
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
