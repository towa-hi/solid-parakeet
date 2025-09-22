using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
using JetBrains.Annotations;
using UnityEngine;
using Board = Contract.Board;

public class BoardManager : MonoBehaviour
{
    public bool initialized;
    
    // assigned by inspector
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public ClickInputManager clickInputManager;
    public Vortex vortex;
    public Transform cameraBounds;
    public Transform waveOrigin1;
    public Transform waveOrigin2;

    // UI stuff generally gets done in the phase
    public GuiGame guiGame;
    // master references to board objects passed to state
    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly Dictionary<PawnId, PawnView> pawnViews = new();
    
    public ArenaController arenaController;
    
    PhaseBase currentPhase;
    
#if USE_GAME_STORE
    GameStore store;
    
#endif
    
    
    public void StartBoardManager()
    {
        Debug.Log("BoardManager.StartBoardManager: begin");
        GameNetworkState netState = new(StellarManager.networkState);
        Debug.Log($"BoardManager.StartBoardManager: snapshot lobbyPhase={netState.lobbyInfo.phase} sub={netState.lobbyInfo.subphase} turn={netState.gameState.turn} security={netState.lobbyParameters.security_mode}");
        Initialize(netState);
        Debug.Log("BoardManager.StartBoardManager: seeding initial OnGameStateBeforeApplied");
        OnGameStateBeforeApplied(netState, default); // seed once on start
    }

    public void CloseBoardManager()
    {
        guiGame.ShowElement(false);
        // Stop polling immediately
        StellarManager.SetPolling(false);
        
        // Unsubscribe from events
        StellarManager.OnGameStateBeforeApplied -= OnGameStateBeforeApplied;
        
        // Cancel any in-flight Stellar task to avoid Task-is-already-set on menu navigation
        StellarManager.AbortCurrentTask();
        
        // Clean up current phase
        currentPhase?.ExitState(clickInputManager, guiGame);
        currentPhase = null;
        
        // Clear existing tileviews and replace
        foreach (TileView tile in tileViews.Values)
        {
            tile.DetachSubscriptions();
            Destroy(tile.gameObject);
        }
        tileViews.Clear();
        
        // Clear any existing pawnviews and replace
        foreach (PawnView pawnView in pawnViews.Values)
        {
            pawnView.DetachSubscriptions();
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();
        
        clickInputManager.SetUpdating(false);
#if USE_GAME_STORE
        // Reset store and event subscriptions to avoid stale UI/state between games
        if (store != null)
        {
            store.EventsEmitted -= OnStoreEvents;
            store = null;
        }
        // Mode change handled by NetworkReducer; nothing to emit here
        // Detach GUI subscriptions to avoid duplicate handlers on next game
        if (guiGame != null)
        {
            if (guiGame.setup != null) guiGame.setup.DetachSubscriptions();
            if (guiGame.movement != null) guiGame.movement.DetachSubscriptions();
            if (guiGame.resolve != null) guiGame.resolve.DetachSubscriptions();
        }
#endif
        initialized = false;
    }
    
    void Initialize(GameNetworkState netState)
    {
        Debug.Log("BoardManager.Initialize: begin");
        CloseBoardManager();
        Debug.Log("BoardManager.Initialize: showing GuiGame and enabling ClickInputManager");
        guiGame.ShowElement(true);
        clickInputManager.SetUpdating(true);
        Debug.Log($"BoardManager.Initialize: cache init? security_mode={netState.lobbyParameters.security_mode}");
        // Initialize cache only in secure mode; single-player/offline and non-secure should not touch cache
        if (netState.lobbyParameters.security_mode)
        {
            CacheManager.Initialize(netState.address, netState.lobbyInfo.index);
        }
        GameLogger.Initialize(netState);
        // Initialize store (flagged)
#if USE_GAME_STORE
        Debug.Log("BoardManager.Initialize: creating GameStore (reducers/effects)");
        IGameReducer[] reducers = new IGameReducer[] { new NetworkReducer(), new ResolveReducer(), new UiReducer() };
        IGameEffect[] effects = new IGameEffect[] { new NetworkEffects() };
        store = new GameStore(
            new GameSnapshot { Net = netState, Mode = ModeDecider.DecideClientMode(netState, default) },
            reducers,
            effects
        );
        store.EventsEmitted += OnStoreEvents;
        ClientMode initMode = ModeDecider.DecideClientMode(netState, default);
        Debug.Log($"BoardManager.Initialize: initial ClientMode={initMode}");
        // Do not emit initial mode here; let OnGameStateBeforeApplied and reducer drive it to avoid races
        // Wire GUI once per game start (flagged path)
        guiGame.setup.OnClearButton = () => store.Dispatch(new SetupClearAll());
        guiGame.setup.OnAutoSetupButton = () => store.Dispatch(new SetupAutoFill());
        guiGame.setup.OnRefreshButton = () => store.Dispatch(new RefreshRequested());
        guiGame.setup.OnSubmitButton = () => store.Dispatch(new SetupSubmit());
        guiGame.setup.OnEntryClicked = (rank) => store.Dispatch(new SetupSelectRank(rank));
        guiGame.setup.AttachSubscriptions();
        guiGame.movement.AttachSubscriptions();
        guiGame.resolve.AttachSubscriptions();
        // Do not initialize panels directly; ClientModeChanged will initialize the active one
        // Immediately request a fresh network snapshot to avoid showing stale state on new games
        store.Dispatch(new RefreshRequested());
#endif
        // set up board and pawns
        Board board = netState.lobbyParameters.board;
        Debug.Log($"BoardManager.Initialize: building board hex={board.hex} tiles={board.tiles.Length} pawns={netState.gameState.pawns.Length}");
        grid.SetBoard(board.hex);
        foreach (TileState tile in board.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(tile, board.hex);
            tileView.AttachSubscriptions();
            tileViews.Add(tile.pos, tileView);
        }
        foreach (PawnState pawn in netState.gameState.pawns)
        {
            GameObject pawnObject = Instantiate(pawnPrefab, transform);
            PawnView pawnView = pawnObject.GetComponent<PawnView>();
            pawnView.Initialize(pawn, tileViews[pawn.pos]);
            pawnView.AttachSubscriptions();
            pawnViews.Add(pawn.pawn_id, pawnView);
        }
        // Expose a resolver so views can map positions to TileViews (for arrows, etc.)
        ViewEventBus.TileViewResolver = (Vector2Int pos) => tileViews.TryGetValue(pos, out TileView tv) ? tv : null;
        Debug.Log("BoardManager.Initialize: finished creating views; starting music");
        AudioManager.PlayMusic(MusicTrack.BATTLE_MUSIC);
        
        StellarManager.OnGameStateBeforeApplied += OnGameStateBeforeApplied;
        
        initialized = true;
        // Initialize arena once per game load
        Debug.Log("BoardManager.Initialize: end (initialized=true)");
        ArenaController.instance?.Initialize(netState.lobbyParameters.board.hex);
    }
    
    void OnGameStateBeforeApplied(GameNetworkState netState, NetworkDelta delta)
    {
        if (!initialized)
        {
            return;
        }
        
        // Check if we're being destroyed or closed
        if (!gameObject || !enabled)
        {
            return;
        }
        Debug.Log($"BoardManager.OnGameStateBeforeApplied: turn={netState.gameState.turn} phase={netState.lobbyInfo.phase} sub={netState.lobbyInfo.subphase} delta: phaseChanged={delta.PhaseChanged} turnChanged={delta.TurnChanged} hasResolve={(delta.TurnResolve.HasValue)}");
#if USE_GAME_STORE
        // Feed the store; views still driven by existing flow for now
        store?.Dispatch(new NetworkStateChanged(netState, delta));
#endif
        // Compute client mode (read-only for now)
        ClientMode clientMode =
#if USE_GAME_STORE
            ModeDecider.DecideClientMode(netState, delta, store?.State.Ui ?? LocalUiState.Empty);
#else
            ModeDecider.DecideClientMode(netState, delta);
#endif
        Debug.Log($"BoardManager.OnGameStateBeforeApplied: ClientMode={clientMode}");
        // Emit mode every update to let UI swap lazily if reducer skipped (e.g., first frame)
#if USE_GAME_STORE
        LocalUiState currentUi = store?.State.Ui ?? LocalUiState.Empty;
#else
        LocalUiState currentUi = LocalUiState.Empty;
#endif
        ViewEventBus.RaiseClientModeChanged(clientMode, netState, currentUi);
        // Mode changes are emitted by NetworkReducer
        
        bool shouldPoll = !netState.IsMySubphase();
        // No local task references to reset; StellarManager governs busy state
        // Legacy FinishedPhase routing not needed under the new system; ClientMode router will show game over UI

        // Check if mode actually changed and use ModeDecider/ModePhaseFactory to map
        Phase newPhase = netState.lobbyInfo.phase;
        bool isInitialPhase = currentPhase == null; // intent: no phase has been set yet
        // Switch when: initial, server says phase changed, or a resolve payload is present.
        // Also: if we are currently in ResolvePhase and the server advanced the turn without a TurnResolve,
        // switch back to the server-declared phase (usually MoveCommit) to avoid getting stuck in resolve.
        bool turnAdvancedNoResolve = delta.TurnChanged && !delta.TurnResolve.HasValue;
        if (turnAdvancedNoResolve)
        {
            Debug.LogWarning($"BoardManager: Turn advanced without resolve. Current phase: {currentPhase.GetType().Name}");
        }
        bool stuckInResolve = currentPhase is ResolvePhase && turnAdvancedNoResolve;
        if (stuckInResolve)
        {
            Debug.LogWarning($"BoardManager: Stuck in ResolvePhase. Current phase: {currentPhase.GetType().Name}");
        }
        bool shouldSwitchPhase = isInitialPhase || (delta.TurnChanged && delta.TurnResolve.HasValue) || delta.PhaseChanged || stuckInResolve; // intent: we need to change local phase now
        if (shouldSwitchPhase)
        {
#if USE_GAME_STORE
            // In flagged builds, avoid SetupCommitPhase entirely; input is routed by mode
            ClientMode mode = ModeDecider.DecideClientMode(netState, delta);
            PhaseBase nextPhase = ModePhaseFactory.CreatePhase(mode, netState, delta);
#else
            bool shouldSwitchToResolvePhase = delta.TurnResolve.HasValue; // enter resolve whenever resolve payload exists
            PhaseBase nextPhase = newPhase switch
            {
                Phase.SetupCommit => new SetupCommitPhase(),
                // Enter ResolvePhase only when TurnResolve is present in this delta
                Phase.MoveCommit => shouldSwitchToResolvePhase ? new ResolvePhase(delta.TurnResolve.Value) : new MoveCommitPhase(),
                Phase.MoveProve => new MoveProvePhase(),
                Phase.RankProve => new RankProvePhase(),
                Phase.Finished or Phase.Aborted => shouldSwitchToResolvePhase ? new ResolvePhase(delta.TurnResolve.Value) : new FinishedPhase(),
                Phase.Lobby => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(),
            };
#endif
            // set phase
            currentPhase?.ExitState(clickInputManager, guiGame);
            currentPhase = nextPhase;
#if USE_GAME_STORE
            currentPhase.SetActionDispatcher(store != null ? new System.Action<GameAction>(store.Dispatch) : null);
#endif
            Debug.Log($"BoardManager: Switched to phase type {currentPhase.GetType().Name} (serverPhase={newPhase}, clientMode={clientMode})");
            currentPhase.EnterState(PhaseStateChanged, CallContract, GetNetworkState, clickInputManager, tileViews, pawnViews, guiGame);
        }
        
        // Always update the network state
        currentPhase.UpdateNetworkState(netState, delta);
        GameLogger.RecordNetworkState(netState);
#if USE_GAME_STORE
        // Under the new system, broadcast PhaseChangeSet only for Resolve/Finished to reuse legacy animation paths.
        if (clientMode == ClientMode.Resolve || clientMode == ClientMode.Finished || clientMode == ClientMode.Aborted)
        {
            PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(currentPhase)));
        }
#else
        PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(currentPhase)));
#endif
        // Manage polling: suspend while in ResolvePhase and when game is finished/aborted
        if (currentPhase is ResolvePhase || currentPhase is FinishedPhase)
        {
            shouldPoll = false;
        }
#if !USE_GAME_STORE
        StellarManager.SetPolling(shouldPoll);
#endif
#if USE_GAME_STORE
        // Route input by mode: in Setup mode, bypass phase and use mode-driven router
        ClientMode routeMode = ModeDecider.DecideClientMode(netState, delta);
        if (routeMode == ClientMode.Setup)
        {
            Debug.Log("BoardManager: Routing ClickInputManager to dispatch hover/click actions");
            clickInputManager.OnMouseInput = (pos, clicked) =>
            {
                store.Dispatch(new SetupHoverAction(pos));
                if (clicked && !StellarManager.IsBusy)
                {
                    store.Dispatch(new SetupClickAt(pos));
                }
            };
        }
        else
        {
            Debug.Log("BoardManager: Routing ClickInputManager to movement actions");
            clickInputManager.OnMouseInput = (pos, clicked) =>
            {
                store.Dispatch(new MoveHoverAction(pos));
                if (clicked && !StellarManager.IsBusy)
                {
                    store.Dispatch(new MoveClickAt(pos));
                }
            };
            // Wire movement GUI buttons to store
            guiGame.movement.OnRefreshButton = () => store.Dispatch(new RefreshRequested());
            guiGame.movement.OnSubmitMoveButton = () => store.Dispatch(new MoveSubmit());
        }
#endif
    }

#if USE_GAME_STORE
    void SetupModeInputRouter(Vector2Int hoveredPos, bool clicked)
    {
        if (store == null) return;
        // Always update hover first so UiReducer computes tool
        Debug.Log($"SetupModeInputRouter: hover pos={hoveredPos} clicked={clicked}");
        store.Dispatch(new SetupHoverAction(hoveredPos));
        if (!clicked) return;
        if (StellarManager.IsBusy) return;
        var mode = store.State.Mode;
        var tool = (store.State.Ui ?? LocalUiState.Empty).SetupTool;
        Debug.Log($"SetupModeInputRouter: dispatch click at {hoveredPos} mode={mode} tool={tool}");
        store.Dispatch(new SetupClickAt(hoveredPos));
    }
#endif
    
    // passed to currentPhase
    void PhaseStateChanged(PhaseChangeSet changes)
    {
        if (true) {
            Debug.Log("BoardManager.PhaseStateChanged: ignored");
            return;
        }
        // Central callback - receives all operations and broadcasts to views
        // boardmanager handles its own stuff first
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupHoverChanged setupHoverChanged:
                    CursorController.UpdateCursor(setupHoverChanged.phase.setupInputTool);
                    break;
                case MoveHoverChanged moveHoverChanged:
                    CursorController.UpdateCursor(moveHoverChanged.phase.moveInputTool);
                    break;
                case ResolveDone resolveDone:
                    // special case for resolvedone to get into movecommit phase without a network update
                    GameNetworkState cachedNetworkState = currentPhase.cachedNetState;
                    NetworkDelta delta = currentPhase.lastDelta;
                    currentPhase.ExitState(clickInputManager, guiGame);
                    currentPhase = cachedNetworkState.lobbyInfo.phase switch
                    {
                        Phase.Aborted or Phase.Finished => new FinishedPhase(),
                        Phase.MoveCommit => new MoveCommitPhase(),
                        _ => throw new ArgumentOutOfRangeException(),
                    };
                    
#if USE_GAME_STORE
                    currentPhase.SetActionDispatcher(store != null ? new System.Action<GameAction>(store.Dispatch) : null);
#endif
                    currentPhase.EnterState(PhaseStateChanged, CallContract, GetNetworkState, clickInputManager, tileViews, pawnViews, guiGame);
                    currentPhase.UpdateNetworkState(cachedNetworkState, delta);
                    PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(currentPhase)));
                    //early return
                    return;
            }
        }
        // update tiles and pawns
        foreach (TileView tileView in tileViews.Values)
        {
            tileView.PhaseStateChanged(changes);
        }
        foreach (PawnView pawnView in pawnViews.Values)
        {
            pawnView.PhaseStateChanged(changes);
        }
        // update gui
        guiGame.PhaseStateChanged(changes);
    }
    
    // passed to currentPhase
    bool GetNetworkState()
    {
        // Don't start new tasks if we're not initialized
        if (!initialized)
        {
            return false;
        }
        
        if (StellarManager.IsBusy)
        {
            Debug.LogWarning("GetNetworkState already has a task in progress");
            return false;
        }
        // Stop polling while network request is in progress
        StellarManager.SetPolling(false);
#if USE_GAME_STORE
        store?.Dispatch(new RefreshRequested());
#else
        _ = StellarManager.UpdateState();
#endif
        // Don't reset here - let OnNetworkStateUpdated handle cleanup
        return true;
    }
    
    // passed to currentPhase
    async Task<bool> CallContract(IReq req, [CanBeNull] IReq req2 = null)
    {
        // Don't start new tasks if we're not initialized
        if (!initialized)
        {
            return false;
        }
        
        if (StellarManager.IsBusy)
        {
            Debug.LogWarning("CallContract already has a task in progress");
            return false;
        }
        // Stop polling while contract call is in progress
        StellarManager.SetPolling(false);
#if USE_GAME_STORE
        // Ensure an await on flagged path to avoid async-without-await warning
        await System.Threading.Tasks.Task.Yield();
        switch (req)
        {
            case CommitSetupReq setup:
                store?.Dispatch(new CommitSetupAction(setup));
                return true;
            case CommitMoveReq commit when req2 is ProveMoveReq prove:
                store?.Dispatch(new CommitMoveAndProveAction(commit, prove));
                return true;
            case ProveMoveReq proveMove:
                store?.Dispatch(new ProveMoveAction(proveMove));
                return true;
            case ProveRankReq proveRank:
                store?.Dispatch(new ProveRankAction(proveRank));
                return true;
            case JoinLobbyReq:
            case MakeLobbyReq:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(req));
        }
#else
        // just for now
        GameNetworkState gnsFromStellarManager = new(StellarManager.networkState);
        LobbyInfo lobbyInfo = gnsFromStellarManager.lobbyInfo;
        LobbyParameters lobbyParameters = gnsFromStellarManager.lobbyParameters;
        AccountAddress userAddress = gnsFromStellarManager.address;
        Result<bool> operationResult = Result<bool>.Ok(true);
        switch (req)
        {
            case CommitSetupReq commitSetupReq:
                operationResult = await StellarManager.CommitSetupRequest(commitSetupReq);
                break;
            case CommitMoveReq commitMoveReq:
                if (req2 is not ProveMoveReq followUpProveMoveReq)
                {
                    throw new ArgumentNullException();
                }
                operationResult = await StellarManager.CommitMoveRequest(commitMoveReq, followUpProveMoveReq, userAddress, lobbyInfo, lobbyParameters);
                break;
            case ProveMoveReq proveMoveReq:
                operationResult=  await StellarManager.ProveMoveRequest(proveMoveReq, userAddress, lobbyInfo, lobbyParameters);
                break;
            case ProveRankReq proveRankReq:
                operationResult = await StellarManager.ProveRankRequest(proveRankReq);
                break;
            case JoinLobbyReq:
            case MakeLobbyReq:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(req));
        }

        if (operationResult.IsError)
        {
            string message = string.IsNullOrEmpty(operationResult.Message) ? "You're now in Offline Mode." : operationResult.Message;
            // Ensure polling stays paused on fatal error
            StellarManager.SetPolling(false);
            // Use new MenuController modal system and exit game
            MenuController menuController = UnityEngine.Object.FindFirstObjectByType<MenuController>();
            if (menuController != null)
            {
                menuController.OpenMessageModal($"Network Unavailable\n{message}");
                menuController.ExitGame();
            }
            else
            {
                Debug.LogError($"MenuController not found. Error: {message}");
            }
            return false;
        }
        Result<bool> code = await StellarManager.UpdateState();
        if (code.IsError)
        {
            string message = string.IsNullOrEmpty(code.Message) ? "You're now in Offline Mode." : code.Message;
            // Ensure polling stays paused on fatal error
            StellarManager.SetPolling(false);
            // Use new MenuController modal system and exit game
            MenuController menuController = UnityEngine.Object.FindFirstObjectByType<MenuController>();
            if (menuController != null)
            {
                menuController.OpenMessageModal($"Network Unavailable\n{message}");
                menuController.ExitGame();
            }
            else
            {
                Debug.LogError($"MenuController not found. Error: {message}");
            }
            return false;
        }
        return true;
#endif
    }

#if USE_GAME_STORE
    void OnStoreEvents(System.Collections.Generic.IReadOnlyList<GameEvent> events)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case SetupRankSelectedEvent sel:
                    // Update cursor based on whether a rank is selected
                    if (currentPhase is SetupCommitPhase scp)
                    {
                        CursorController.UpdateCursor(scp.setupInputTool);
                    }
                    break;
                case SetupPendingChangedEvent pend:
                    // No additional broadcast; views use OnSetupPendingChanged
                    break;
            }
        }
    }
#endif
    
}

public abstract class PhaseBase
{
    public GameNetworkState cachedNetState;
    public NetworkDelta lastDelta;
    public Vector2Int hoveredPos;
    
    public Dictionary<Vector2Int, TileView> tileViews;
    public Dictionary<PawnId, PawnView> pawnViews;
    
    Action<PhaseChangeSet> OnPhaseStateChanged;
    Func<IReq, IReq, Task<bool>> OnCallContract;
    Func<bool> OnGetNetworkState;
    protected System.Action<GameAction> DispatchAction;
    
    protected PhaseBase()
    {
        //cachedNetState = netState;
    }
    
    public void EnterState(
        Action<PhaseChangeSet> inOnPhaseStateChanged, 
        Func<IReq, IReq, Task<bool>> inOnCallContract, 
        Func<bool> inOnGetNetworkState, 
        ClickInputManager clickInputManager, 
        Dictionary<Vector2Int, TileView> bmTileViews, 
        Dictionary<PawnId, PawnView> bmPawnViews, 
        GuiGame guiGame)
    {
        OnPhaseStateChanged = inOnPhaseStateChanged;
        OnCallContract = inOnCallContract;
        OnGetNetworkState = inOnGetNetworkState;
        tileViews = bmTileViews;
        pawnViews = bmPawnViews;
        clickInputManager.OnMouseInput = OnMouseInput;
        SetGui(guiGame, true);
    }

    public void SetActionDispatcher(System.Action<GameAction> dispatcher)
    {
        DispatchAction = dispatcher;
    }

    protected abstract void SetGui(GuiGame guiGame, bool set);
    
    public virtual void UpdateNetworkState(GameNetworkState netState)
    {
        cachedNetState = netState;
    }

    public void UpdateNetworkState(GameNetworkState netState, NetworkDelta delta)
    {
        lastDelta = delta;
        UpdateNetworkState(netState);
    }
    
    public void ExitState(ClickInputManager clickInputManager, GuiGame guiGame)
    {
        OnPhaseStateChanged = null;
        OnCallContract = null;
        OnGetNetworkState = null;
        clickInputManager.OnMouseInput = null;
        SetGui(guiGame, false);
    }
    
    protected bool InvokeOnGetNetworkState()
    {
        return OnGetNetworkState.Invoke();
    }

    protected async Task<bool> InvokeOnCallContract(IReq req1, IReq req2 = null)
    {
        return await OnCallContract.Invoke(req1, req2);
    }

    protected void InvokeOnPhaseStateChanged(PhaseChangeSet changes)
    {
        OnPhaseStateChanged.Invoke(changes);
    }
    
    public virtual void AfterOnGameStateBeforeApplied()
    {

    }
    protected abstract void OnMouseInput(Vector2Int hoveredPos, bool clicked);
}
public class SetupCommitPhase : PhaseBase
{
    public Dictionary<PawnId, Rank?> pendingCommits = null;
    public Rank? selectedRank = null;
    public SetupInputTool setupInputTool = SetupInputTool.NONE;

    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.setup.OnClearButton = set ? (System.Action)(() =>
        {
#if USE_GAME_STORE
            DispatchAction?.Invoke(new SetupClearAll());
#else
            OnClear();
#endif
        }) : null;
        guiGame.setup.OnAutoSetupButton = set ? (System.Action)(() =>
        {
#if USE_GAME_STORE
            DispatchAction?.Invoke(new SetupAutoFill());
#else
            OnAutoSetup();
#endif
        }) : null;
        guiGame.setup.OnRefreshButton = set ? (System.Action)(() =>
        {
#if USE_GAME_STORE
            DispatchAction?.Invoke(new RefreshRequested());
#else
            OnRefresh();
#endif
        }) : null;
        guiGame.setup.OnSubmitButton = set ? (System.Action)(() =>
        {
#if USE_GAME_STORE
            DispatchAction?.Invoke(new SetupSubmit());
#else
            OnSubmit();
#endif
        }) : null;
        guiGame.setup.OnEntryClicked = set ? (System.Action<Rank>)((clickedRank) =>
        {
#if USE_GAME_STORE
            DispatchAction?.Invoke(new SetupSelectRank(clickedRank));
#else
            OnEntryClicked(clickedRank);
#endif
        }) : null;

    }

    
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        pendingCommits = new();
        if (cachedNetState.IsMySubphase())
        {
            
        }
        else
        {
            foreach (PawnState pawn in cachedNetState.gameState.pawns.Where(p => p.GetTeam() == cachedNetState.userTeam))
            {
                if (CacheManager.GetHiddenRankAndProof(pawn.pawn_id) is not CachedRankProof rankProof)
                {
                    throw new Exception($"cachemanager could not find pawn {pawn.pawn_id}");
                }
                pendingCommits[pawn.pawn_id] = rankProof.hidden_rank.rank;
            }
        }
    }
    

    
    void OnClear()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        foreach (PawnId pawnId in pendingCommits.Keys.ToList())
        {
            pendingCommits[pawnId] = null;
        }
#if !USE_GAME_STORE
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
#endif
    }

    void OnAutoSetup()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        Dictionary<Vector2Int, Rank> autoCommitments = cachedNetState.AutoSetup(cachedNetState.userTeam);
        foreach ((Vector2Int pos, Rank rank) in autoCommitments)
        {
            PawnState pawn = cachedNetState.GetAlivePawnFromPosUnchecked(pos);
            pendingCommits[pawn.pawn_id] = rank;
        }
#if !USE_GAME_STORE
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
#endif
    }
    
    void OnRefresh()
    {
        InvokeOnGetNetworkState();
    }

    void OnSubmit()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        List<HiddenRank> hiddenRanks = new();
        List<SetupCommit> commits = new();
        foreach ((PawnId pawnId, Rank? mRank) in pendingCommits)
        {
            if (mRank is not Rank rank)
            {
                throw new InvalidOperationException("not all pawns are committed");
            }
            PawnState pawn = cachedNetState.GetPawnFromId(pawnId);
            HiddenRank hiddenRank = new()
            {
                pawn_id = pawn.pawn_id,
                rank = rank,
                salt = Globals.RandomSalt(),
            };
            hiddenRanks.Add(hiddenRank);
            commits.Add(new()
            {
                hidden_rank_hash = SCUtility.Get16ByteHash(hiddenRank),
                pawn_id = pawn.pawn_id,
            });
        }
        List<byte[]> leaves = new();
        foreach (SetupCommit commit in commits)
        {
            leaves.Add(commit.hidden_rank_hash);
        }
        (byte[] root, MerkleTree tree) = MerkleTree.BuildMerkleTree(leaves.ToArray());
        List<CachedRankProof> ranksAndProofs = new();
        for (int i = 0; i < commits.Count; i++)
        {
            HiddenRank hiddenRank = hiddenRanks[i];
            MerkleProof merkleProof = tree.GenerateProof((uint)i);
            ranksAndProofs.Add(new() {hidden_rank = hiddenRank, merkle_proof = merkleProof});
        }

        if (cachedNetState.lobbyParameters.security_mode)
        {
            CacheManager.StoreHiddenRanksAndProofs(ranksAndProofs, cachedNetState.address, cachedNetState.lobbyInfo.index);
        }
        
        CommitSetupReq req = new()
        {
            lobby_id = cachedNetState.lobbyInfo.index,
            rank_commitment_root = root,
            zz_hidden_ranks = cachedNetState.lobbyParameters.security_mode ? new HiddenRank[]{} : hiddenRanks.ToArray(),
        };
#if USE_GAME_STORE
        // In flagged mode, submission is handled by SetupSubmit effect; do not invoke legacy call.
        return;
#else
        _ = InvokeOnCallContract(req, null);
#endif
    }

    void OnEntryClicked(Rank clickedRank)
    {
        Rank? oldSelectedRank = selectedRank;
        if (selectedRank is Rank rank && rank == clickedRank)
        {
            selectedRank = null;
        }
        else
        {
            selectedRank = clickedRank;
        }
#if USE_GAME_STORE
        DispatchAction?.Invoke(new SetupSelectRank(selectedRank));
#else
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new SetupRankSelected(oldSelectedRank, this)));
#endif
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        Debug.Log($"SetupCommitPhase.OnMouseInput hovered={inHoveredPos} clicked={clicked} busy={StellarManager.IsBusy}");
        List<GameOperation> operations = new();
        Vector2Int oldHoveredPos = hoveredPos;
        hoveredPos = inHoveredPos;
#if USE_GAME_STORE
        // First update hover so UiReducer can compute the correct tool synchronously
        Debug.Log($"SetupCommitPhase: dispatch SetupHoverAction {hoveredPos}");
        DispatchAction?.Invoke(new SetupHoverAction(hoveredPos));
#endif
        if (clicked && !StellarManager.IsBusy)
        {
            switch (setupInputTool)
            {
                case SetupInputTool.NONE:
                    Debug.Log("SetupCommitPhase: tool NONE");
                    break;
                case SetupInputTool.ADD:
                    {
#if USE_GAME_STORE
                        Debug.Log($"SetupCommitPhase: dispatch SetupCommitAt {hoveredPos}");
                        DispatchAction?.Invoke(new SetupCommitAt(hoveredPos));
#else
                        operations.Add(CommitPosition(hoveredPos));
#endif
                    }
                    break;
                case SetupInputTool.REMOVE:
                    {
#if USE_GAME_STORE
                        Debug.Log($"SetupCommitPhase: dispatch SetupUncommitAt {hoveredPos}");
                        DispatchAction?.Invoke(new SetupUncommitAt(hoveredPos));
#else
                        operations.Add(UncommitPosition(hoveredPos));
#endif
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
#if !USE_GAME_STORE
        setupInputTool = GetNextTool();
        operations.Add(new SetupHoverChanged(oldHoveredPos, hoveredPos, this));
#endif
#if !USE_GAME_STORE
        // Keep hover/cursor visuals responsive in legacy path
        InvokeOnPhaseStateChanged(new PhaseChangeSet(operations));
#endif
    }

    SetupInputTool GetNextTool()
    {
        if (StellarManager.IsBusy)
        {
            return SetupInputTool.NONE;
        }
        SetupInputTool tool = SetupInputTool.NONE;
        if (!cachedNetState.IsMySubphase())
        {
            return SetupInputTool.NONE;
        }
        // if hovered over a already committed pawn
        if (cachedNetState.GetAlivePawnFromPosChecked(hoveredPos) is PawnState pawn && pendingCommits.ContainsKey(pawn.pawn_id) && pendingCommits[pawn.pawn_id] != null)
        {
            tool = SetupInputTool.REMOVE;
        }
        else if (selectedRank is Rank rank && cachedNetState.GetTileChecked(hoveredPos) is TileState tile && tile.setup == cachedNetState.userTeam && GetRemainingRank(rank) > 0)
        {
            tool = SetupInputTool.ADD;
        }
        return tool;
    }

    SetupRankCommitted CommitPosition(Vector2Int pos)
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        if (!cachedNetState.lobbyParameters.board.GetTileFromPosition(pos).HasValue)
        {
            throw new InvalidOperationException("Pos is not valid");
        }
        if (selectedRank is Rank rank && GetRemainingRank(rank) <= 0)
        {
            throw new InvalidOperationException("too many pawns of this rank committed");
        }
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        PawnState pawn = cachedNetState.GetAlivePawnFromPosUnchecked(pos);
        pendingCommits[pawn.pawn_id] = selectedRank;
        Debug.Log($"commit position {pawn.pawn_id} {pos} {selectedRank}");
        return new(oldPendingCommits, this);
    }

    SetupRankCommitted UncommitPosition(Vector2Int pos)
    {
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        PawnState pawn = cachedNetState.GetAlivePawnFromPosUnchecked(pos);
        pendingCommits[pawn.pawn_id] = null;
        Debug.Log($"uncommit position regardless of selected rank {pawn.pawn_id} {pos} {selectedRank}");
        return new(oldPendingCommits, this);
    }
    
    public (Rank, int, int)[] RanksRemaining()
    {
        Rank[] ranks = (Rank[])Enum.GetValues(typeof(Rank));
        (Rank, int, int)[] ranksRemaining = new (Rank, int, int)[ranks.Length];
        if (ranks.Length != cachedNetState.lobbyParameters.max_ranks.Length)
        {
            throw new InvalidOperationException($"Rank enum count {ranks.Length} doesn't match max_ranks array length");
        }
        for (int i = 0; i < ranksRemaining.Length; i++)
        {
            Rank rank = ranks[i];
            int max = cachedNetState.lobbyParameters.GetMax(rank);
            int committed = pendingCommits.Values.Count(r => r == rank);
            ranksRemaining[i] = (rank, max, committed);
        }
        return ranksRemaining;
    }

    int GetRemainingRank(Rank rank)
    {
        int max = cachedNetState.lobbyParameters.GetMax(rank);
        int committed = pendingCommits.Values.Count(r => r == rank);
        return max - committed;
    }
}

public class ResolvePhase: PhaseBase
{
    // Processed resolve data for stepping through checkpoints
    public TurnResolveDelta tr;
    public int currentBattleIndex = -1;

    public enum Checkpoint { Pre, PostMoves, Battle, Final }
    public Checkpoint currentCheckpoint = Checkpoint.Pre;

    HashSet<PawnId> pendingMoveAnimPawns = new();

    public ResolvePhase(TurnResolveDelta inTr)
    {
        tr = inTr;
    }

    protected override void SetGui(GuiGame guiGame, bool set) 
    {
        Debug.Log($"ResolvePhase.SetGui: begin set={set}");
        guiGame.resolve.OnPrevButton = set ? OnPrev : null;
        guiGame.resolve.OnNextButton = set ? OnNext : null;
        guiGame.resolve.OnSkipButton = set ? OnSkip : null;
        if (set)
        {
            PawnView.OnMoveAnimationCompleted += OnPawnMoveAnimationCompleted;
        }
        else
        {
            PawnView.OnMoveAnimationCompleted -= OnPawnMoveAnimationCompleted;
            // Ensure all PawnViews cancel any ongoing animations when leaving resolve
            foreach (var view in pawnViews.Values)
            {
                view.StopAllCoroutines();
            }
        }
    }

    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        //Debug.Log("ResolvePhase.OnMouseInput");
    }

    public override void AfterOnGameStateBeforeApplied()
    {
        base.AfterOnGameStateBeforeApplied();
        EnterCheckpoint(currentCheckpoint);
    }

    void OnPrev()
    {
        Debug.Log("ResolvePhase.OnPrev: clicked");
#if USE_GAME_STORE
        DispatchAction?.Invoke(new ResolvePrev());
        return;
#else
        switch (currentCheckpoint)
        {
            case Checkpoint.Final:
                if (tr.battles.Length > 0)
                {
                    currentCheckpoint = Checkpoint.Battle;
                    currentBattleIndex = tr.battles.Length - 1;
                }
                else
                {
                    currentCheckpoint = Checkpoint.PostMoves;
                }
                break;
            case Checkpoint.Battle:
                if (currentBattleIndex > 0)
                {
                    currentBattleIndex--;
                }
                else
                {
                    currentCheckpoint = Checkpoint.Pre;
                    currentBattleIndex = -1;
                }
                break;
            case Checkpoint.PostMoves:
                currentCheckpoint = Checkpoint.Pre;
                currentBattleIndex = -1;
                break;
            case Checkpoint.Pre:
                // stay
                break;
        }
        EnterCheckpoint(currentCheckpoint);
#endif
    }

    void OnNext()
    {
        Debug.Log("ResolvePhase.OnNext: clicked");
#if USE_GAME_STORE
        DispatchAction?.Invoke(new ResolveNext());
        return;
#else
        switch (currentCheckpoint)
        {
            case Checkpoint.Pre:
                EnterCheckpoint(Checkpoint.PostMoves);
                break;
            case Checkpoint.PostMoves:
                if (tr.battles.Length > 0)
                {
                    StartTransitionBattle(0);
                }
                else
                {
                    EnterCheckpoint(Checkpoint.Final);
                }
                break;
            case Checkpoint.Battle:
                if (currentBattleIndex + 1 < tr.battles.Length)
                {
                    StartTransitionBattle(currentBattleIndex + 1);
                }
                else
                {
                    EnterCheckpoint(Checkpoint.Final);
                }
                break;
            case Checkpoint.Final:
                ExitResolve();
                break;
        }
#endif
    }

    void OnSkip()
    {
        Debug.Log("ResolvePhase.OnSkip: clicked");
#if USE_GAME_STORE
        DispatchAction?.Invoke(new ResolveSkip());
        return;
#else
        ExitResolve();
#endif
    }

    void ExitResolve()
    {
        Debug.Log("ResolvePhase.ExitResolve: begin");
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new ResolveDone()));
    }

    void EnterCheckpoint(Checkpoint checkpoint)
    {
        Debug.Log($"ResolvePhase.EnterCheckpoint: begin -> {checkpoint} (current battleIndex={currentBattleIndex})");
        currentCheckpoint = checkpoint;
        // Initialize animation tracking for PostMoves
        if (checkpoint == Checkpoint.PostMoves)
        {
            pendingMoveAnimPawns = new HashSet<PawnId>(
                (tr.pawnDeltas ?? new Dictionary<PawnId, SnapshotPawnDelta>())
                .Values
                .Where(d => d.prePos != d.postPos)
                .Select(d => d.pawnId));
        }
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new ResolveCheckpointEntered(checkpoint, tr, currentBattleIndex, this)));
    }


    // Removed separate ApplyMoves transition; animations now start on entering PostMoves

    void StartTransitionBattle(int battleIndex)
    {
        Debug.Log($"ResolvePhase.StartTransitionBattle: begin -> index={battleIndex}");
        // For now, no animations; directly enter the battle checkpoint
        currentBattleIndex = battleIndex;
        EnterCheckpoint(Checkpoint.Battle);
    }

    void OnPawnMoveAnimationCompleted(PawnId pawn)
    {
        Debug.Log($"ResolvePhase.OnPawnMoveAnimationCompleted: pawn={pawn}");
        if (!pendingMoveAnimPawns.Remove(pawn)) return;
        if (pendingMoveAnimPawns.Count > 0) return;
        // All animations done; remain in PostMoves. UI stays in "Applying Moves" until user advances.
        // No additional operation needed.
    }
}
public class MoveCommitPhase: PhaseBase
{
    public Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairs = new();
    public Vector2Int? selectedPos = null;
    public MoveInputTool moveInputTool = MoveInputTool.NONE;
    public HashSet<Vector2Int> validTargetPositions = new();
    public List<HiddenMove> turnHiddenMoves = new();
    public HashSet<Vector2Int> hoveredValidTargetPositions = new();
    
    bool IsAtMoveLimit()
    {
        return movePairs.Count >= cachedNetState.GetMaxMovesThisTurn();
    }

    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.movement.OnSubmitMoveButton = set ? OnSubmit : null;
        guiGame.movement.OnRefreshButton = set ? OnRefresh : null;
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        if (!cachedNetState.IsMySubphase())
        {
            if (turnHiddenMoves.Count == 0)
            {
                turnHiddenMoves.Clear();
                byte[][] moveHashes = cachedNetState.GetUserMove().move_hashes;
                foreach (byte[] moveHash in moveHashes)
                {
                    if (CacheManager.GetHiddenMove(moveHash) is HiddenMove hiddenMove)
                    {
                        turnHiddenMoves.Add(hiddenMove);
                    }
                }
            }
        }
    }

    void OnSubmit()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        int count = movePairs.Count;
        int maxAllowed = cachedNetState.GetMaxMovesThisTurn();
        if (count < 1 || count > maxAllowed)
        {
            throw new InvalidOperationException($"invalid number of moves for turn: {count} (max {maxAllowed})");
        }
        List<HiddenMove> hiddenMoves = new();
        List<byte[]> hiddenMoveHashes = new();
        foreach (KeyValuePair<PawnId, (Vector2Int, Vector2Int)> entry in movePairs)
        {
            PawnState selectedPawn = cachedNetState.GetPawnFromId(entry.Key);
            Vector2Int targetPos = entry.Value.Item2;
            if (!selectedPawn.alive)
            {
                throw new InvalidOperationException("selectedPawn must exist and be alive");
            }
            if (selectedPawn.GetTeam() != cachedNetState.userTeam)
            {
                throw new InvalidOperationException("selectedPawn team is invalid");
            }
            if (!cachedNetState.GetValidMoveTargetList(selectedPawn.pawn_id).Contains(targetPos))
            {
                throw new InvalidOperationException("targetpos is out of range");
            }
            HiddenMove hiddenMove = new()
            {
                pawn_id = selectedPawn.pawn_id,
                salt = Globals.RandomSalt(),
                start_pos = selectedPawn.pos,
                target_pos = targetPos,
            };
            CacheManager.StoreHiddenMove(hiddenMove, cachedNetState.address, cachedNetState.lobbyInfo.index);
            hiddenMoves.Add(hiddenMove);
            hiddenMoveHashes.Add(SCUtility.Get16ByteHash(hiddenMove));
        }
        // Persist submitted moves locally so TileView can continue showing them after submit
        turnHiddenMoves = new(hiddenMoves);
        
        CommitMoveReq commitMoveReq = new()
        {
            lobby_id = cachedNetState.lobbyInfo.index,
            move_hashes = hiddenMoveHashes.ToArray(),
        };
        ProveMoveReq proveMoveReq = new()
        {
            lobby_id = cachedNetState.lobbyInfo.index,
            move_proofs = hiddenMoves.ToArray(),
        };
        _ = InvokeOnCallContract(commitMoveReq, proveMoveReq);
    }

    void OnRefresh()
    {
        InvokeOnGetNetworkState();
    }

    
    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        List<GameOperation> operations = new();
        Vector2Int oldHoveredPos = hoveredPos;
        hoveredPos = inHoveredPos;
        hoveredValidTargetPositions.Clear();
        
        if (clicked && !StellarManager.IsBusy)
        {
            switch (moveInputTool)
            {
                case MoveInputTool.NONE:
                    break;
                case MoveInputTool.SELECT:
                    operations.Add(ClearMovePair(hoveredPos));
                    operations.Add(SelectPosition(hoveredPos));
                    break;
                case MoveInputTool.TARGET:
                    if (selectedPos is Vector2Int selectedPosVal)
                    {
                        operations.Add(SelectPosition(null));
                        operations.Add(AddMovePair(selectedPosVal, hoveredPos));
                    }
                    else
                    {
                        throw new Exception("Attempted to select a target but selectedPos is null");
                    }
                    break;
                case MoveInputTool.CLEAR_SELECT:
                    operations.Add(SelectPosition(null));
                    break;
                case MoveInputTool.CLEAR_MOVEPAIR:
                {
                    operations.Add(ClearMovePair(hoveredPos));
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        moveInputTool = GetNextTool(hoveredPos);
        switch (moveInputTool)
        {
            case MoveInputTool.NONE:
                break;
            case MoveInputTool.SELECT:
                if (cachedNetState.GetAlivePawnFromPosChecked(hoveredPos) is PawnState hoveredPawn && hoveredPawn.GetTeam() == cachedNetState.userTeam)
                {
                    hoveredValidTargetPositions = cachedNetState.GetValidMoveTargetList(hoveredPawn.pawn_id, movePairs);
                }
                break;
            case MoveInputTool.TARGET:
                break;
            case MoveInputTool.CLEAR_SELECT:
                break;
            case MoveInputTool.CLEAR_MOVEPAIR:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        operations.Add(new MoveHoverChanged(moveInputTool, hoveredPos, this));
        InvokeOnPhaseStateChanged(new PhaseChangeSet(operations));
    }
    
    MoveInputTool GetNextTool(Vector2Int hoveredPosition)
    {
        if (StellarManager.IsBusy)
        {
            return MoveInputTool.NONE;
        }
        if (!cachedNetState.IsMySubphase())
        {
            return MoveInputTool.NONE;
        }
        // When a tile is selected: TARGET if hovered is targetable; otherwise CLEAR_SELECT
        if (selectedPos != null)
        {
            return validTargetPositions.Contains(hoveredPosition)
                ? MoveInputTool.TARGET
                : MoveInputTool.CLEAR_SELECT;
        }
        // Nothing selected: if hovered over start pos of an existing move pair, allow clearing it
        if (movePairs.Any(kv => kv.Value.Item1 == hoveredPosition))
        {
            return MoveInputTool.CLEAR_MOVEPAIR;
        }
        // Nothing selected: SELECT if hovered over a selectable tile; otherwise NONE
        if (!IsAtMoveLimit() && cachedNetState.GetAlivePawnFromPosChecked(hoveredPosition) is PawnState hoveredPawn &&cachedNetState.CanUserMovePawn(hoveredPawn.pawn_id))
        {
            return MoveInputTool.SELECT;
        }
        return MoveInputTool.NONE;
    }


    MovePosSelected SelectPosition(Vector2Int? inSelectedPosition)
    {
        selectedPos = inSelectedPosition;
        validTargetPositions.Clear();
        if (selectedPos is Vector2Int selectedPosVal && cachedNetState.GetAlivePawnFromPosChecked(selectedPosVal) is PawnState selectedPawn)
        {
            validTargetPositions = cachedNetState.GetValidMoveTargetList(selectedPawn.pawn_id, movePairs);
        }
        return new(selectedPos, validTargetPositions, movePairs);
    }

    MovePairUpdated ClearMovePair(Vector2Int startPos)
    {
        PawnId keyToRemove = movePairs.FirstOrDefault(kv => kv.Value.Item1 == startPos).Key;
        movePairs.Remove(keyToRemove);
        Debug.Log("clear movepair");
        return new(movePairs, keyToRemove, this);
    }

    MovePairUpdated AddMovePair(Vector2Int startPos, Vector2Int targetPos)
    {
        PawnState selectedPawn = cachedNetState.GetAlivePawnFromPosUnchecked(startPos);
        movePairs.Add(selectedPawn.pawn_id, (startPos, targetPos));
        return new(movePairs, selectedPawn.pawn_id, this);
    }
}

public class MoveProvePhase: PhaseBase
{
    public List<HiddenMove> turnHiddenMoves = new();
    
    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.movement.OnRefreshButton = set ? OnRefresh : null;
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        // Build hidden moves for this turn from committed move hashes
        turnHiddenMoves.Clear();
        byte[][] moveHashes = cachedNetState.GetUserMove().move_hashes;
        foreach (byte[] moveHash in moveHashes)
        {
            if (CacheManager.GetHiddenMove(moveHash) is not HiddenMove hiddenMove)
            {
                throw new Exception($"Could not find move with move hash {System.Convert.ToBase64String(moveHash)}");
            }
            turnHiddenMoves.Add(hiddenMove);
        }
        AutomaticallySendMoveProof();
    }
    
    void AutomaticallySendMoveProof()
    {
        if (cachedNetState.lobbyInfo.phase != Phase.MoveProve || !cachedNetState.IsMySubphase())
        {
            return;
        }
        Debug.Log("automatically send move proof");
        ProveMoveReq proveMoveReq = new()
        {
            lobby_id = cachedNetState.lobbyInfo.index,
            move_proofs = turnHiddenMoves.ToArray(),
        };
        _ = InvokeOnCallContract(proveMoveReq, null);
    }
    
    void OnRefresh()
    {
        InvokeOnGetNetworkState();
    }
    
    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
    
    }


}

public class RankProvePhase: PhaseBase
{
    public List<HiddenMove> turnHiddenMoves = new();
    
    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.movement.OnRefreshButton = set ? OnRefresh : null;
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        // Build from network state's revealed move proofs for this turn
        turnHiddenMoves.Clear();
        foreach (HiddenMove hiddenMove in cachedNetState.GetUserMove().move_proofs)
        {
            turnHiddenMoves.Add(hiddenMove);
        }
        AutomaticallySendRankProof();
    }
    
    void AutomaticallySendRankProof()
    {
        if (cachedNetState.lobbyInfo.phase != Phase.RankProve || !cachedNetState.IsMySubphase())
        {
            return;
        }
        Debug.Log("automatically send rank proof");
        List<HiddenRank> hiddenRanks = new();
        List<MerkleProof> merkleProofs = new();
        foreach (PawnId pawnId in cachedNetState.GetUserMove().needed_rank_proofs)
        {
            if (CacheManager.GetHiddenRankAndProof(pawnId) is not CachedRankProof rankProof)
            {
                throw new Exception($"cachemanager could not find pawn {pawnId}");
            }
            hiddenRanks.Add(rankProof.hidden_rank);
            merkleProofs.Add(rankProof.merkle_proof);
        }
        ProveRankReq proveRankReq = new()
        {
            hidden_ranks = hiddenRanks.ToArray(),
            lobby_id = cachedNetState.lobbyInfo.index,
            merkle_proofs = merkleProofs.ToArray(),
        };
        _ = InvokeOnCallContract(proveRankReq, null);
    }
    
    void OnRefresh()
    {
        InvokeOnGetNetworkState();
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        
    }
}

public class FinishedPhase : PhaseBase
{

    protected override void SetGui(GuiGame guiGame, bool set)
    {
        
    }

    protected override void OnMouseInput(Vector2Int hoveredPos, bool clicked)
    {
        
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

public abstract record GameOperation;

// Suppress local naming warnings for record positional parameters in this block
#pragma warning disable IDE1006 // Naming rule violation: These words must begin with upper case characters
// ReSharper disable InconsistentNaming
public record SetupHoverChanged(Vector2Int oldHoveredPos, Vector2Int newHoveredPos, SetupCommitPhase phase) : GameOperation;

public record SetupRankCommitted(Dictionary<PawnId, Rank?> oldPendingCommits, SetupCommitPhase phase) : GameOperation;
public record SetupRankSelected(Rank? oldSelectedRank, SetupCommitPhase phase) : GameOperation;

public record ResolveCheckpointEntered(ResolvePhase.Checkpoint checkpoint, TurnResolveDelta tr, int currentBattleIndex, ResolvePhase phase) : GameOperation;
public record ResolveDone() : GameOperation;

public record MoveHoverChanged(MoveInputTool tool, Vector2Int newPos, MoveCommitPhase phase) : GameOperation;
public record MovePosSelected(Vector2Int? selectedPos, HashSet<Vector2Int> targetablePositions, Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairsSnapshot) : GameOperation;
public record MovePairUpdated(Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairsSnapshot, PawnId? changedPawnId, MoveCommitPhase phase) : GameOperation;

public record NetStateUpdated(PhaseBase phase) : GameOperation;
// ReSharper restore InconsistentNaming
#pragma warning restore IDE1006


public class PhaseChangeSet
{
    public List<GameOperation> operations { get; }

    public PhaseChangeSet(List<GameOperation> inOperations)
    {
        operations = inOperations;
    }

    public PhaseChangeSet(GameOperation inOperation)
    {
        operations = new()
        {
            inOperation,
        };
    }

    public NetStateUpdated GetNetStateUpdated()
    {
        foreach (GameOperation op in operations)
        {
            if (op is NetStateUpdated netStateUpdatedOp)
            {
                return netStateUpdatedOp;
            }
        }
        return null;
    }

}
