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
    public GuiGameOverModal gameOverModal; // overlay, enable/disable only
    
    public CameraAnchor boardAnchor;
    bool isUpdating;
    int busyCount;

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
            if (setup.gameObject.activeSelf != showSetup) { setup.ShowElement(showSetup); }
            if (movement.gameObject.activeSelf != showMovement) { movement.ShowElement(showMovement); }
            if (resolve.gameObject.activeSelf != showResolve) { resolve.ShowElement(showResolve); }

            // Game over handling: show overlay regardless of which subpanel is active
            if (phase is FinishedPhase)
            {
                TryShowGameOver(netStateUpdated.phase.cachedNetState);
            }
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
        }
    }

    void TryShowGameOver(GameNetworkState netState)
    {
        // Find modal if not wired
        if (gameOverModal == null)
        {
            gameOverModal = GetComponentInChildren<GuiGameOverModal>(true);
        }
        if (gameOverModal == null)
        {
            Debug.LogWarning("GuiGame: gameOverModal is not assigned and could not be found in children.");
            return;
        }
		// Ensure overlay is displayed regardless of which subpanel is active
		if (!gameOverModal.transform.IsChildOf(transform))
		{
			gameOverModal.transform.SetParent(transform, false);
		}
		if (!gameObject.activeInHierarchy)
		{
			ShowElement(true);
		}
        // Compute end state
        uint endState = 4; // inconclusive default
        if (netState.lobbyInfo.phase == Phase.Finished)
        {
            // winner encoded in subphase
            Team hostTeam = netState.lobbyParameters.host_team;
            Team guestTeam = hostTeam == Team.RED ? Team.BLUE : Team.RED;
            switch (netState.lobbyInfo.subphase)
            {
                case Subphase.Host:
                    endState = hostTeam == Team.RED ? 1u : 2u;
                    break;
                case Subphase.Guest:
                    endState = guestTeam == Team.RED ? 1u : 2u;
                    break;
                case Subphase.None:
                    endState = 0u; // tie
                    break;
                default:
                    endState = 4u;
                    break;
            }
        }
        else if (netState.lobbyInfo.phase == Phase.Aborted)
        {
            endState = 4u; // inconclusive
        }

        // Always enable and initialize overlay; button returns to main menu
        if (!gameOverModal.gameObject.activeSelf)
        {
            gameOverModal.gameObject.SetActive(true);
        }
        gameOverModal.Initialize(endState, netState.userTeam, ExitToMainMenu);
    }

    void ExitToMainMenu()
    {
        var menuController = UnityEngine.Object.FindObjectOfType<MenuController>();
        if (menuController == null)
        {
            Debug.LogError("GuiGame: MenuController not found in scene for ExitToMainMenu()");
            return;
        }
        menuController.ExitGame();
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
