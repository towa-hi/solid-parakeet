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
    
    
    PhaseBase currentPhase;
    
    
    public void StartBoardManager()
    {
        GameNetworkState netState = new(StellarManager.networkState);
        Initialize(netState);
        OnGameStateBeforeApplied(netState, default); // seed once on start
    }

    public void CloseBoardManager()
    {
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
            Destroy(tile.gameObject);
        }
        tileViews.Clear();
        
        // Clear any existing pawnviews and replace
        foreach (PawnView pawnView in pawnViews.Values)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();
        
        clickInputManager.SetUpdating(false);
        initialized = false;
    }
    
    void Initialize(GameNetworkState netState)
    {
        CloseBoardManager();
        
        clickInputManager.SetUpdating(true);
        CacheManager.Initialize(netState.address, netState.lobbyInfo.index);
        GameLogger.Initialize(netState);
        // set up board and pawns
        Board board = netState.lobbyParameters.board;
        grid.SetBoard(board.hex);
        foreach (TileState tile in board.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(tile, board.hex);
            tileViews.Add(tile.pos, tileView);
        }
        foreach (PawnState pawn in netState.gameState.pawns)
        {
            GameObject pawnObject = Instantiate(pawnPrefab, transform);
            PawnView pawnView = pawnObject.GetComponent<PawnView>();
            pawnView.Initialize(pawn, tileViews[pawn.pos]);
            pawnViews.Add(pawn.pawn_id, pawnView);
        }
        AudioManager.PlayMusic(MusicTrack.BATTLE_MUSIC);
        
        StellarManager.OnGameStateBeforeApplied += OnGameStateBeforeApplied;
        
        initialized = true;
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
        Debug.Log("BoardManager::OnGameStateBeforeApplied");
        bool shouldPoll = !netState.IsMySubphase();
        // No local task references to reset; StellarManager governs busy state

        // Check if phase actually changed
        Phase newPhase = netState.lobbyInfo.phase;
        bool turnChanged = currentPhase != null && currentPhase.cachedNetState.gameState.turn != netState.gameState.turn;
        bool phaseUninitialized = currentPhase == null;
        bool phaseChanged = phaseUninitialized || 
                          currentPhase.cachedNetState.lobbyInfo.phase != newPhase ||
                          turnChanged;
        // Hold phase switch while resolving until user allows exit
        if (currentPhase is ResolvePhase rp && !rp.IsExitAllowed())
        {
            if (!phaseUninitialized)
            {
                Debug.Log($"BoardManager: Holding phase switch to {newPhase} while in ResolvePhase; awaiting user input");
                phaseChanged = false;
            }
        }
        if (phaseChanged)
        {
            PhaseBase nextPhase = newPhase switch
            {
                Phase.SetupCommit => new SetupCommitPhase(),
                Phase.MoveCommit => turnChanged ? new ResolvePhase() : new MoveCommitPhase(),
                Phase.MoveProve => new MoveProvePhase(),
                Phase.RankProve => new RankProvePhase(),
                Phase.Finished or Phase.Aborted or Phase.Lobby => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(),
            };
            // set phase
            currentPhase?.ExitState(clickInputManager, guiGame);
            currentPhase = nextPhase;
            Debug.Log($"BoardManager: Switched to phase type {currentPhase.GetType().Name} (netPhase={newPhase}, turnChanged={turnChanged})");
            currentPhase.EnterState(PhaseStateChanged, CallContract, GetNetworkState, clickInputManager, tileViews, pawnViews, guiGame);
        }
        
        // Always update the network state
        currentPhase.UpdateNetworkState(netState, delta);
        GameLogger.RecordNetworkState(netState);
        PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(currentPhase)));
        // After broadcasting NetStateUpdated, if we're in resolve and have data, show the pre-turn snapshot
        if (currentPhase is ResolvePhase resolvePhase)
        {
            Debug.Log("BoardManager: In ResolvePhase after NetStateUpdated; ensuring pre snapshot is applied");
            resolvePhase.ShowPreSnapshotIfAvailable();
        }
        
        // Manage polling based on whose turn it is
        StellarManager.SetPolling(shouldPoll);
    }
    
    // passed to currentPhase
    void PhaseStateChanged(PhaseChangeSet changes)
    {
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
        _ = StellarManager.UpdateState();
        // Don't reset here - let OnNetworkStateUpdated handle cleanup
        return true;
    }
    
    // passed to currentPhase
    bool CallContract(IReq req, [CanBeNull] IReq req2 = null)
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
        
        switch (req)
        {
            case CommitSetupReq commitSetupReq:
                _ = StellarManager.CommitSetupRequest(commitSetupReq);
                break;
            case CommitMoveReq commitMoveReq:
                if (req2 is not ProveMoveReq followUpProveMoveReq)
                {
                    throw new ArgumentNullException();
                }
                _ = StellarManager.CommitMoveRequest(commitMoveReq, followUpProveMoveReq);
                break;
            case ProveMoveReq proveMoveReq:
                _ = StellarManager.ProveMoveRequest(proveMoveReq);
                break;
            case ProveRankReq proveRankReq:
                _ = StellarManager.ProveRankRequest(proveRankReq);
                break;
            case JoinLobbyReq:
            case MakeLobbyReq:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(req));
        }
        return true;
    }

    
}

public abstract class PhaseBase
{
    public GameNetworkState cachedNetState;
    public NetworkDelta lastDelta;
    public Vector2Int hoveredPos;
    
    public Dictionary<Vector2Int, TileView> tileViews;
    public Dictionary<PawnId, PawnView> pawnViews;
    
    Action<PhaseChangeSet> OnPhaseStateChanged;
    Func<IReq, IReq, bool> OnCallContract;
    Func<bool> OnGetNetworkState;
    
    protected PhaseBase()
    {
        //cachedNetState = netState;
    }
    
    public void EnterState(
        Action<PhaseChangeSet> inOnPhaseStateChanged, 
        Func<IReq, IReq, bool> inOnCallContract, 
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

    protected bool InvokeOnCallContract(IReq req1, IReq req2 = null)
    {
        return OnCallContract.Invoke(req1, req2);
    }

    protected void InvokeOnPhaseStateChanged(PhaseChangeSet changes)
    {
        OnPhaseStateChanged.Invoke(changes);
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
        guiGame.setup.OnClearButton = set ? OnClear : null;
        guiGame.setup.OnAutoSetupButton = set ? OnAutoSetup : null;
        guiGame.setup.OnRefreshButton = set ? OnRefresh : null;
        guiGame.setup.OnSubmitButton = set ? OnSubmit : null;
        guiGame.setup.OnEntryClicked = set ? OnEntryClicked : null;
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
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
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
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
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
        CacheManager.StoreHiddenRanksAndProofs(ranksAndProofs, cachedNetState.address, cachedNetState.lobbyInfo.index);
        
        CommitSetupReq req = new()
        {
            lobby_id = cachedNetState.lobbyInfo.index,
            rank_commitment_root = root,
            zz_hidden_ranks = cachedNetState.lobbyParameters.security_mode ? new HiddenRank[]{} : hiddenRanks.ToArray(),
        };
        InvokeOnCallContract(req, null);
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
        InvokeOnPhaseStateChanged(new PhaseChangeSet(new SetupRankSelected(oldSelectedRank, this)));
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        //Debug.Log($"On Hover {hoveredPos}");
        List<GameOperation> operations = new();
        Vector2Int oldHoveredPos = hoveredPos;
        hoveredPos = inHoveredPos;
        if (clicked && !StellarManager.IsBusy)
        {
            switch (setupInputTool)
            {
                case SetupInputTool.NONE:
                    break;
                case SetupInputTool.ADD:
                    operations.Add(CommitPosition(hoveredPos));
                    break;
                case SetupInputTool.REMOVE:
                    operations.Add(UncommitPosition(hoveredPos));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        setupInputTool = GetNextTool();
        operations.Add(new SetupHoverChanged(oldHoveredPos, hoveredPos, this));
        InvokeOnPhaseStateChanged(new PhaseChangeSet(operations));
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
    TurnResolveDelta? resolveData;
    List<ResolveCheckpointType> timeline = new();
    PositionSnapshot[] battleSnapshots = Array.Empty<PositionSnapshot>();
    int checkpointIndex = 0;
    // Minimal FSM
    enum ResolveState { ShowMoves, ApplyMoves, Battle, Final }
    ResolveState currentState = ResolveState.ShowMoves;
    int currentBattleIndex = -1;
    bool allowExit = false;
    public bool IsExitAllowed() { return allowExit; }

    HashSet<PawnId> pendingMoveAnimPawns = new HashSet<PawnId>();

    protected override void SetGui(GuiGame guiGame, bool set) 
    {
        Debug.Log($"ResolvePhase.SetGui set={set}");
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
        }
    }

    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        if (lastDelta.TurnChanged && lastDelta.TurnResolve is TurnResolveDelta tr)
        {
            PrepareResolve(tr);
            // Enter initial state
            EnterState(ResolveState.ShowMoves);
        }
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        Debug.Log("ResolvePhase.OnMouseInput");
    }

    void PrepareResolve(TurnResolveDelta tr)
    {
        resolveData = tr;
        checkpointIndex = 0;
        currentState = ResolveState.ShowMoves;
        currentBattleIndex = -1;
        timeline = new List<ResolveCheckpointType>();
        timeline.Add(ResolveCheckpointType.ShowMoves);
        timeline.Add(ResolveCheckpointType.ApplyMoves);
        for (int i = 0; i < (tr.battles?.Length ?? 0); i++)
        {
            timeline.Add(ResolveCheckpointType.BattleResolved);
        }
        timeline.Add(ResolveCheckpointType.Final);
        battleSnapshots = BuildBattleSnapshots(tr);
        Debug.Log($"ResolvePhase.PrepareResolve: turn={tr.turn} checkpoints={timeline.Count} moves={tr.moves?.Length ?? 0} battles={tr.battles?.Length ?? 0}");
    }

    PositionSnapshot[] BuildBattleSnapshots(TurnResolveDelta tr)
    {
        // Start from postMoves snapshot and apply each battle sequentially
        Dictionary<PawnId, (Vector2Int pos, Team team)> map = tr.postMoves.pawns.ToDictionary(t => t.pawn, t => (t.pos, t.team));
        PositionSnapshot[] snapshots = new PositionSnapshot[tr.battles?.Length ?? 0];
        for (int i = 0; i < snapshots.Length; i++)
        {
            BattleEvent battle = tr.battles[i];
            // Remove dead pawns
            foreach ((PawnId pawn, Team _) in battle.dead)
            {
                map.Remove(pawn);
            }
            // Survivors remain at battle.pos (already in map). Ensure they exist.
            foreach ((PawnId pawn, Team team) in battle.survivors)
            {
                map[pawn] = (battle.pos, team);
            }
            snapshots[i] = new PositionSnapshot
            {
                pawns = map.Select(kv => new SnapshotPawn
                {
                    pawn = kv.Key,
                    team = kv.Value.team,
                    pos = kv.Value.pos,
                    alive = true,
                    revealed = false,
                    rank = null,
                    moved = false,
                    movedScout = false,
                }).ToArray(),
            };
        }
        return snapshots;
    }

    // Public accessors for UI/animation systems
    public ResolveCheckpointType GetCurrentCheckpointType()
    {
        if (timeline.Count == 0) return ResolveCheckpointType.Final;
        return timeline[checkpointIndex];
    }

    public PositionSnapshot GetCurrentSnapshot()
    {
        if (resolveData is not TurnResolveDelta tr)
        {
            return new PositionSnapshot { pawns = Array.Empty<SnapshotPawn>() };
        }
        // Derive from FSM state
        switch (currentState)
        {
            case ResolveState.ShowMoves:
                return tr.pre;
            case ResolveState.ApplyMoves:
                return tr.postMoves;
            case ResolveState.Battle:
                if (currentBattleIndex >= 0 && currentBattleIndex < battleSnapshots.Length)
                {
                    return battleSnapshots[currentBattleIndex];
                }
                return tr.postMoves;
            case ResolveState.Final:
            default:
                return tr.postBattles;
        }
    }

    public MoveEvent[] GetArrows()
    {
        return resolveData?.moves ?? Array.Empty<MoveEvent>();
    }

    public (Vector2Int? pos, BattleEvent? battle) GetCurrentBattle()
    {
        if (resolveData is not TurnResolveDelta tr) return (null, null);
        int battleIdx = checkpointIndex - 2;
        if (battleIdx >= 0 && tr.battles != null && battleIdx < tr.battles.Length)
        {
            return (tr.battles[battleIdx].pos, tr.battles[battleIdx]);
        }
        return (null, null);
    }

    public void ShowPreSnapshotIfAvailable()
    {
        if (resolveData is not TurnResolveDelta tr) return;
        // Force checkpoint to pre and instantly apply board view to match previous turn
        checkpointIndex = 0;
        ApplySnapshotInstant(tr.pre);
    }

    void ApplySnapshotInstant(PositionSnapshot snapshot)
    {
        // Update PawnViews to match snapshot instantly (no animation)
        var byId = snapshot.pawns.ToDictionary(t => t.pawn, t => (t.pos, t.team));
        foreach (PawnView pawnView in pawnViews.Values)
        {
            if (byId.TryGetValue(pawnView.pawnId, out var entry))
            {
                if (tileViews.TryGetValue(entry.pos, out TileView tile))
                {
                    // Set constraint directly to tile (instant positioning)
                    pawnView.SendMessage("DisplayPosView", tile);
                }
            }
        }
        // Highlights/arrows are handled by UI layer later
    }

    void OnPrev()
    {
        if (timeline.Count == 0) { Debug.Log("ResolvePhase.OnPrev: timeline empty"); return; }
        // FSM prev logic
        allowExit = false; // navigating back cancels exit intent
        if (currentState == ResolveState.Final)
        {
            if ((resolveData?.battles?.Length ?? 0) > 0)
            {
                currentState = ResolveState.Battle;
                currentBattleIndex = (resolveData!.Value.battles!.Length - 1);
            }
            else
            {
                currentState = ResolveState.ApplyMoves;
            }
        }
        else if (currentState == ResolveState.Battle)
        {
            if (currentBattleIndex > 0)
            {
                currentBattleIndex--;
            }
            else
            {
                currentState = ResolveState.ApplyMoves;
                currentBattleIndex = -1;
            }
        }
        else if (currentState == ResolveState.ApplyMoves)
        {
            // Snap back to pre-move without animations
            pendingMoveAnimPawns.Clear();
            currentState = ResolveState.ShowMoves;
        }
        // Apply state without animations (snap)
        EnterState(currentState, false);
    }

    void OnNext()
    {
        if (timeline.Count == 0) { Debug.Log("ResolvePhase.OnNext: timeline empty"); return; }
        // FSM next logic
        int battlesCount = resolveData?.battles?.Length ?? 0;
        if (currentState == ResolveState.ShowMoves)
        {
            currentState = ResolveState.ApplyMoves;
        }
        else if (currentState == ResolveState.ApplyMoves)
        {
            if (battlesCount > 0)
            {
                currentState = ResolveState.Battle;
                currentBattleIndex = 0;
            }
            else
            {
                currentState = ResolveState.Final;
            }
        }
        else if (currentState == ResolveState.Battle)
        {
            if (currentBattleIndex + 1 < battlesCount)
            {
                currentBattleIndex++;
            }
            else
            {
                currentState = ResolveState.Final;
                currentBattleIndex = -1;
            }
        }
        else if (currentState == ResolveState.Final)
        {
            // User explicitly requests exit from resolve
            allowExit = true;
            Debug.Log("ResolvePhase.OnNext: user requested exit from Final state");
        }
        // Apply state
        EnterState(currentState);
    }

    void OnSkip()
    {
        if (timeline.Count == 0) { Debug.Log("ResolvePhase.OnSkip: timeline empty"); return; }
        currentState = ResolveState.Final;
        currentBattleIndex = -1;
        EnterState(currentState);
    }

    void EnterState(ResolveState state, bool animate = true)
    {
        // Map FSM state to checkpoint index for legacy helpers
        if (timeline.Count > 0)
        {
            switch (state)
            {
                case ResolveState.ShowMoves:
                    checkpointIndex = 0;
                    break;
                case ResolveState.ApplyMoves:
                    checkpointIndex = Math.Min(1, timeline.Count - 1);
                    break;
                case ResolveState.Battle:
                    checkpointIndex = Math.Clamp(2 + currentBattleIndex, 0, timeline.Count - 1);
                    break;
                case ResolveState.Final:
                    checkpointIndex = timeline.Count - 1;
                    break;
            }
        }
        // Emit operations and apply visuals for the state
        if (resolveData is not TurnResolveDelta tr)
        {
            Debug.LogWarning("ResolvePhase.EnterState: resolveData not ready");
            return;
        }
        switch (state)
        {
            case ResolveState.ShowMoves:
                // Ensure we ignore any late move completion events
                pendingMoveAnimPawns.Clear();
                ApplySnapshotInstant(tr.pre);
                InvokeOnPhaseStateChanged(new PhaseChangeSet(new ResolveStateShowMoves(tr.moves, this)));
                break;
            case ResolveState.ApplyMoves:
                if (!animate)
                {
                    pendingMoveAnimPawns.Clear();
                    ApplySnapshotInstant(tr.postMoves);
                }
                else
                {
                    pendingMoveAnimPawns = new HashSet<PawnId>(tr.moves.Select(m => m.pawn));
                    if (pendingMoveAnimPawns.Count == 0)
                    {
                        AdvanceAfterApplyMoves();
                    }
                    else
                    {
                        InvokeOnPhaseStateChanged(new PhaseChangeSet(new ResolveStateApplyMoves(tr.moves, this)));
                    }
                }
                break;
            case ResolveState.Battle:
                if (currentBattleIndex >= 0 && tr.battles != null && currentBattleIndex < tr.battles.Length)
                {
                    if (!animate)
                    {
                        ApplySnapshotInstant(battleSnapshots[currentBattleIndex]);
                    }
                    else
                    {
                        BattleEvent be = tr.battles[currentBattleIndex];
                        InvokeOnPhaseStateChanged(new PhaseChangeSet(new ResolveStateBattle(be, this)));
                    }
                }
                break;
            case ResolveState.Final:
                ApplySnapshotInstant(tr.postBattles);
                InvokeOnPhaseStateChanged(new PhaseChangeSet(new ResolveStateFinal(this)));
                if (allowExit)
                {
                    // Do nothing more; BoardManager will transition when contract/network state changes.
                    Debug.Log("ResolvePhase.Final: awaiting user Next to exit; exit flag currently true");
                }
                break;
        }
        Debug.Log($"ResolvePhase EnterState -> {state} (checkpoint {checkpointIndex})");
    }

    void OnPawnMoveAnimationCompleted(PawnId pawn)
    {
        // Only track completions while in ApplyMoves; ignore late events when user snapped back
        if (currentState != ResolveState.ApplyMoves)
        {
            return;
        }
        if (pendingMoveAnimPawns.Remove(pawn))
        {
            if (pendingMoveAnimPawns.Count == 0)
            {
                AdvanceAfterApplyMoves();
            }
        }
    }

    void AdvanceAfterApplyMoves()
    {
        if (resolveData is not TurnResolveDelta tr)
        {
            return;
        }
        int battlesCount = tr.battles?.Length ?? 0;
        if (battlesCount > 0)
        {
            currentState = ResolveState.Battle;
            currentBattleIndex = 0;
        }
        else
        {
            currentState = ResolveState.Final;
        }
        EnterState(currentState);
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
        InvokeOnCallContract(commitMoveReq, proveMoveReq);
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
        InvokeOnCallContract(proveMoveReq, null);
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
        InvokeOnCallContract(proveRankReq, null);
    }
    
    void OnRefresh()
    {
        InvokeOnGetNetworkState();
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
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

public record MoveHoverChanged(MoveInputTool tool, Vector2Int newPos, MoveCommitPhase phase) : GameOperation;
public record MovePosSelected(Vector2Int? selectedPos, HashSet<Vector2Int> targetablePositions, Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairsSnapshot) : GameOperation;
public record MovePairUpdated(Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairsSnapshot, PawnId? changedPawnId, MoveCommitPhase phase) : GameOperation;

public record NetStateUpdated(PhaseBase phase) : GameOperation;
public record ApplyMovesRequested(MoveEvent[] moves, ResolvePhase phase) : GameOperation; // TODO: deprecated
public record ResolveStateShowMoves(MoveEvent[] moves, ResolvePhase phase) : GameOperation;
public record ResolveStateApplyMoves(MoveEvent[] moves, ResolvePhase phase) : GameOperation;
public record ResolveStateBattle(BattleEvent battle, ResolvePhase phase) : GameOperation;
public record ResolveStateFinal(ResolvePhase phase) : GameOperation;
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
