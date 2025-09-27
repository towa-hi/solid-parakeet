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
    
    
    GameStore store;
    
    
    
    public void StartBoardManager()
    {
        Debug.Log("BoardManager.StartBoardManager: begin");
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
        GameLogger.Initialize(netState);
        Debug.Log("BoardManager.Initialize: creating GameStore (reducers/effects)");
        IGameReducer[] reducers = new IGameReducer[] { new NetworkReducer(), new ResolveReducer(), new UiReducer() };
        IGameEffect[] effects = new IGameEffect[] { new NetworkEffects(), new global::ViewAdapterEffects(), new global::StoreDebugEffect() };
        store = new GameStore(
            new GameSnapshot { Net = netState, Mode = ModeDecider.DecideClientMode(netState, default) },
            reducers,
            effects
        );
        ClientMode initMode = ModeDecider.DecideClientMode(netState, default);
        Debug.Log($"BoardManager.Initialize: initial ClientMode={initMode}");
        guiGame.setup.OnClearButton = () => store.Dispatch(new SetupClearAll());
        guiGame.setup.OnAutoSetupButton = () => store.Dispatch(new SetupAutoFill());
        guiGame.setup.OnRefreshButton = () => store.Dispatch(new RefreshRequested());
        guiGame.setup.OnSubmitButton = () => store.Dispatch(new SetupSubmit());
        guiGame.setup.OnEntryClicked = (rank) => store.Dispatch(new SetupSelectRank(rank));
        guiGame.movement.OnRefreshButton = () => store.Dispatch(new RefreshRequested());
        guiGame.movement.OnSubmitMoveButton = () => store.Dispatch(new MoveSubmit());
        guiGame.resolve.OnPrevButton = () => store.Dispatch(new ResolvePrev());
        guiGame.resolve.OnNextButton = () => store.Dispatch(new ResolveNext());
        guiGame.resolve.OnSkipButton = () => store.Dispatch(new ResolveSkip());
        guiGame.setup.AttachSubscriptions();
        guiGame.movement.AttachSubscriptions();
        guiGame.resolve.AttachSubscriptions();
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
        // Seed initial mode to views now that board/pawn views exist
        ViewEventBus.RaiseClientModeChanged(initMode, netState, store.State.Ui ?? LocalUiState.Empty);
        Debug.Log("BoardManager.Initialize: finished creating views; starting music");
        AudioManager.PlayMusic(MusicTrack.BATTLE_MUSIC);
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
        store = null;
        // Reset debug SO if present
        var debugSO = Resources.Load<StoreDebugSO>("StoreDebug");
        if (debugSO != null) debugSO.ResetState();
        // Clear resolver to avoid stale references held by views/utilities
        ViewEventBus.TileViewResolver = null;
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

		store?.Dispatch(new NetworkStateChanged(netState, delta));
        // Polling toggles are now handled by PollingEffects
    }

    void OnMouseInput(Vector2Int pos, bool clicked)
    {
        //Debug.Log($"BoardManager.OnMouseInput: pos={pos} clicked={clicked} mode={store?.State.Mode}");
        switch (store?.State.Mode)
        {
            case ClientMode.Setup:
				store.Dispatch(new SetupHoverAction(pos));
				if (clicked && !StellarManager.IsBusy)
                {
                    store.Dispatch(new SetupClickAt(pos));
					// Do another hover pass immediately after the click
					store.Dispatch(new SetupHoverAction(pos));
                }
                break;
            case ClientMode.Move:
				store.Dispatch(new MoveHoverAction(pos));
				if (clicked && !StellarManager.IsBusy)
                {
                    store.Dispatch(new MoveClickAt(pos));
					// Do another hover pass immediately after the click
					store.Dispatch(new MoveHoverAction(pos));
                }
                break;
        }
    }
}