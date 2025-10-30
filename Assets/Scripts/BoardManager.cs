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
    public Graveyard graveyard;
    public ClickInputManager clickInputManager;
    public Vortex vortex;
    public Transform waveOrigin1;
    public Transform waveOrigin2;

    // UI stuff generally gets done in the phase
    public GuiGame guiGame;
    // master references to board objects passed to state
    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly Dictionary<PawnId, PawnView> pawnViews = new();
    
    public ArenaController arenaController;
    
    public GameStore Store { get; private set; }
    MenuController menuController;
    
    
    public void StartBoardManager(MenuController menuController)
    {
        this.menuController = menuController;
        Debug.Log("BoardManager.StartBoardManager: begin");
		// Ensure vortex visuals are reset before we build the board
		if (vortex != null)
		{
			vortex.ResetAll();
		}
		// Reset debug ScriptableObject at launch so we start from a clean slate
		var debugSO = Resources.Load<StoreDebugSO>("StoreDebug");
		if (debugSO != null) debugSO.ResetState();
        GameNetworkState netState = new(StellarManager.networkState);
        Debug.Log($"BoardManager.StartBoardManager: snapshot lobbyPhase={netState.lobbyInfo.phase} sub={netState.lobbyInfo.subphase} turn={netState.gameState.turn} security={netState.lobbyParameters.security_mode}");
        CloseBoardManager();
        guiGame.ShowElement(true);
        clickInputManager.SetUpdating(true);
        Debug.Log($"BoardManager.Initialize: cache init? security_mode={netState.lobbyParameters.security_mode}");
        // Initialize cache only in secure mode; single-player/offline and non-secure should not touch cache
        if (netState.lobbyParameters.security_mode)
        {
            CacheManager.Initialize(netState.address, netState.lobbyInfo.index);
        }
        Debug.Log("BoardManager.Initialize: creating GameStore (reducers/effects)");
        IGameReducer[] reducers = new IGameReducer[] { new NetworkReducer(), new ResolveReducer(), new UiReducer() };
        IGameEffect[] effects = new IGameEffect[] { new ViewAdapterEffects(), new StoreDebugEffect() };
        Store = new GameStore(
            new GameSnapshot { Net = netState, Mode = ModeDecider.DecideClientMode(netState, default) },
            reducers,
            effects
        );
        if (Tooltip.Instance != null)
        {
            Tooltip.Instance.SetStore(Store);
        }
        ClientMode initMode = ModeDecider.DecideClientMode(netState, default);
        Debug.Log($"BoardManager.Initialize: initial ClientMode={initMode}");
        guiGame.setup.OnClearButton = () => Store.Dispatch(new SetupClearAll());
        guiGame.setup.OnAutoSetupButton = () => Store.Dispatch(new SetupAutoFill());
		guiGame.setup.OnSubmitButton = OnSubmitSetupButton;
        guiGame.setup.OnEntryClicked = (rank) => Store.Dispatch(new SetupSelectRank(rank));
        guiGame.setup.menuButton.onClick.AddListener(guiGame.ExitToMainMenu);
		guiGame.movement.OnSubmitMoveButton = OnSubmitMoveButton;
		guiGame.movement.OnMenuButton = guiGame.ExitToMainMenu;
        guiGame.resolve.OnPrevButton = () => Store.Dispatch(new ResolvePrev());
        guiGame.resolve.OnNextButton = () => Store.Dispatch(new ResolveNext());
        guiGame.resolve.OnSkipButton = () => Store.Dispatch(new ResolveSkip());
        // Subscriptions for setup/movement/resolve are managed by GuiGame on ClientMode changes
		// Subscribe to mode changes so we can control Vortex during Resolve
		ViewEventBus.OnClientModeChanged -= HandleClientModeChangedForVortex;
		ViewEventBus.OnClientModeChanged += HandleClientModeChangedForVortex;
        Board board = netState.lobbyParameters.board;
        Debug.Log($"BoardManager.Initialize: building board hex={board.hex} tiles={board.tiles.Length} pawns={netState.gameState.pawns.Length}");
        grid.SetBoard(board.hex, netState.userTeam);
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
        // Initialize and seed Graveyard
        // if (graveyard != null)
        // {
        //     graveyard.Initialize(netState);
        //     graveyard.AttachSubscriptions();
        //     graveyard.SeedFromSnapshot(netState.gameState.pawns);
        // }
        // Expose a resolver so views can map positions to TileViews (for arrows, etc.)
        ViewEventBus.TileViewResolver = (Vector2Int pos) => tileViews.TryGetValue(pos, out TileView tv) ? tv : null;
        // Seed initial mode to views now that board/pawn views exist
        ViewEventBus.RaiseClientModeChanged(new GameSnapshot { Mode = initMode, Net = netState, Ui = Store.State.Ui ?? LocalUiState.Empty });
        Debug.Log("BoardManager.Initialize: finished creating views; starting music");
        AudioManager.PlayBattleMusicWithIntro();
        // Ensure no duplicate subscriptions if StartBoardManager is called repeatedly
        StellarManager.OnGameStateBeforeApplied -= OnGameStateBeforeApplied;
        StellarManager.OnGameStateBeforeApplied += OnGameStateBeforeApplied;
        clickInputManager.OnMouseInput -= OnMouseInput;
        clickInputManager.OnMouseInput += OnMouseInput;
        initialized = true;
        // Initialize arena once per game load
        Debug.Log("BoardManager.Initialize: end (initialized=true)");
        ArenaController.instance?.Initialize(netState.lobbyParameters.board.hex);
        Debug.Log("BoardManager.StartBoardManager: seeding initial OnGameStateBeforeApplied");
        OnGameStateBeforeApplied(netState, default); // seed once on start
    }

    public void CloseBoardManager()
    {
        guiGame.ShowElement(false);
        // Stop polling immediately
        StellarManager.SetPolling(false);
        // Unhook vortex listener if present
        ViewEventBus.OnClientModeChanged -= HandleClientModeChangedForVortex;
        // Unsubscribe from events
        StellarManager.OnGameStateBeforeApplied -= OnGameStateBeforeApplied;
        if (clickInputManager != null)
        {
            clickInputManager.OnMouseInput -= OnMouseInput;
        }
        
        // Cancel any in-flight Stellar task to avoid Task-is-already-set on menu navigation
        StellarManager.AbortCurrentTask();
        
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
        // Reset store and event subscriptions to avoid stale UI/state between games
        Store = null;
        if (Tooltip.Instance != null)
        {
            Tooltip.Instance.SetStore(null);
        }
        // Reset debug SO if present
        var debugSO = Resources.Load<StoreDebugSO>("StoreDebug");
        if (debugSO != null) debugSO.ResetState();
        // Clear resolver to avoid stale references held by views/utilities
        ViewEventBus.TileViewResolver = null;
        // Detach Graveyard subscriptions
        if (graveyard != null)
        {
            graveyard.DetachSubscriptions();
        }
        // Mode change handled by NetworkReducer; nothing to emit here
        // Detach GUI subscriptions to avoid duplicate handlers on next game
        if (guiGame != null)
        {
            if (guiGame.setup != null) guiGame.setup.DetachSubscriptions();
            if (guiGame.movement != null) guiGame.movement.DetachSubscriptions();
            if (guiGame.resolve != null) guiGame.resolve.DetachSubscriptions();
        }
        initialized = false;
    }

    void HandleClientModeChangedForVortex(GameSnapshot snapshot)
    {
        if (vortex == null) return;
        if (snapshot.Mode == ClientMode.Resolve)
        {
            vortex.StartVortex();
        }
        else
        {
            vortex.EndVortex();
        }
    }
    
    async void OnGameStateBeforeApplied(GameNetworkState net, NetworkDelta delta)
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
        // update store
        Store.Dispatch(new NetworkStateChanged(net, delta));
        // automatically send requests if we can build them
        if (!StellarManager.IsBusy)
        {
            bool fireAndForgetUpdateState = false;
            if (Store.State.Net.lobbyInfo.phase == Phase.MoveProve && Store.State.Net.IsMySubphase())
            {
                if (Store.State.TryBuildProveMoveReqFromCache(out var proveMoveReq))
                {
                    Store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = new ProveMove(proveMoveReq), TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
                    Result<bool> result = await StellarManager.ProveMoveRequest(proveMoveReq, net.address, net.lobbyInfo, net.lobbyParameters);
                    Store.Dispatch(new UiWaitingForResponse(null));
                    if (result.IsError)
                    {
                        HandleFatalNetworkError(result.Message);
                        return;
                    }
                    fireAndForgetUpdateState = true;
                }
            }
            if (Store.State.Net.lobbyInfo.phase == Phase.RankProve && Store.State.Net.IsMySubphase())
            {
                if (Store.State.TryBuildProveRankReqFromCache(out var proveRankReq))
                {
                    Store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = new ProveRank(proveRankReq), TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
                    Result<bool> result = await StellarManager.ProveRankRequest(proveRankReq);
                    Store.Dispatch(new UiWaitingForResponse(null));
                    if (result.IsError)
                    {
                        HandleFatalNetworkError(result.Message);
                        return;
                    }
                    fireAndForgetUpdateState = true;
                }
            }
            if (fireAndForgetUpdateState)
            {
                UpdateState();
            }
        }
        
    }

    void OnMouseInput(Vector2Int pos, bool clicked)
    {
        if (WalletManager.IsWalletBusy) return;
        //Debug.Log($"BoardManager.OnMouseInput: pos={pos} clicked={clicked} mode={Store?.State.Mode}");
        switch (Store?.State.Mode)
        {
            case ClientMode.Setup:
				Store.Dispatch(new SetupHoverAction(pos));
				if (clicked && !StellarManager.IsBusy)
                {
					Store.Dispatch(new SetupClickAt(pos));
					// Do another hover pass immediately after the click
					Store.Dispatch(new SetupHoverAction(pos));
                }
                break;
            case ClientMode.Move:
				Store.Dispatch(new MoveHoverAction(pos));
				if (clicked && !StellarManager.IsBusy)
                {
					Store.Dispatch(new MoveClickAt(pos));
					// Do another hover pass immediately after the click
					Store.Dispatch(new MoveHoverAction(pos));
                }
                break;
        }
    }

	async void OnSubmitSetupButton()
	{
		var net = Store.State.Net;
		Debug.Log($"[BoardManager] OnSubmitSetupButton: phase={net.lobbyInfo.phase} isMySubphase={net.IsMySubphase()} busy={StellarManager.IsBusy}");
		if (!net.IsMySubphase() || StellarManager.IsBusy)
		{
			Debug.Log($"[BoardManager] OnSubmitSetupButton: skipped isMySubphase={net.IsMySubphase()} busy={StellarManager.IsBusy}");
			return;
		}
		if (!Store.State.TryBuildCommitSetupReq(out var req))
		{
			Debug.LogWarning("[BoardManager] OnSubmitSetupButton: no commits to submit; skipping");
			return;
		}
		Store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = new CommitSetup(req), TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
        Result<bool> submit = await StellarManager.CommitSetupRequest(req);
		Store.Dispatch(new UiWaitingForResponse(null));
        if (submit.IsError)
        {
            HandleFatalNetworkError(submit.Message);
            return;
        }
        UpdateState();
	}

	async void OnSubmitMoveButton()
	{
		var net = Store.State.Net;
		var pairsCount = Store.State.Ui?.MovePairs?.Count ?? 0;
		Debug.Log($"[BoardManager] OnSubmitMoveButton: phase={net.lobbyInfo.phase} isMySubphase={net.IsMySubphase()} pairs={pairsCount} busy={StellarManager.IsBusy}");
		if (!net.IsMySubphase() || pairsCount == 0 || StellarManager.IsBusy)
		{
			Debug.Log($"[BoardManager] OnSubmitMoveButton: skipped isMySubphase={net.IsMySubphase()} pairs={pairsCount} busy={StellarManager.IsBusy}");
			return;
		}
		if (!Store.State.TryBuildCommitMoveAndProveMoveReqs(out var commit, out var prove))
		{
			Debug.LogWarning("[BoardManager] OnSubmitMoveButton: could not build move reqs; skipping");
			return;
		}
		Store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = new CommitMoveAndProve(commit, prove), TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
		Result<bool> submit = await StellarManager.CommitMoveRequest(commit, prove, net.address, net.lobbyInfo, net.lobbyParameters);
		Store.Dispatch(new UiWaitingForResponse(null));
        if (submit.IsError)
        {
            HandleFatalNetworkError(submit.Message);
            return;
        }
        UpdateState();
	}

    async void OnRedeemWinButton()
    {
        var net = Store.State.Net;
        Debug.Log($"[BoardManager] OnRedeemWinButton: phase={net.lobbyInfo.phase} isMySubphase={net.IsMySubphase()} busy={StellarManager.IsBusy}");
        if (net.lobbyInfo.phase != Phase.Finished || !net.IsMySubphase() || StellarManager.IsBusy)
        {
            Debug.Log($"[BoardManager] OnRedeemWinButton: skipped phase={net.lobbyInfo.phase} isMySubphase={net.IsMySubphase()} busy={StellarManager.IsBusy}");
            return;
        }
        var req = new RedeemWinReq { lobby_id = net.lobbyInfo.index };
        Store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = new RedeemWin(req), TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
        Result<bool> submit = await StellarManager.RedeemWinRequest(req);
        Store.Dispatch(new UiWaitingForResponse(null));
        if (submit.IsError)
        {
            HandleFatalNetworkError(submit.Message);
            return;
        }
        UpdateState();
    }

    async void UpdateState()
    {
        Store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = new UpdateState(), TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
        var result = await StellarManager.UpdateState();
        Store.Dispatch(new UiWaitingForResponse(null));
        if (result.IsError)
        {
            HandleFatalNetworkError(result.Message);
            return;
        }
    }
    
    static void HandleFatalNetworkError(string message)
    {
        string msg = string.IsNullOrEmpty(message) ? "You're now in Offline Mode." : message;
        // Ensure polling is paused via UI transitions; do not manage here
        MenuController menuController = UnityEngine.Object.FindFirstObjectByType<MenuController>();
        if (menuController != null)
        {
            menuController.OpenMessageModal($"Network Unavailable\n{msg}");
            menuController.ExitGame();
        }
        else
        {
            Debug.LogError($"MenuController not found. Error: {msg}");
        }
    }

    static void LogTaskExceptions(Task task)
	{
		if (task == null) return;
		if (task.IsCompleted)
		{
			if (task.IsFaulted && task.Exception != null)
			{
				Debug.LogException(task.Exception);
			}
		}
		else
		{
			_ = task.ContinueWith(t =>
			{
				if (t.IsFaulted && t.Exception != null)
				{
					Debug.LogException(t.Exception);
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
		}
	}
}