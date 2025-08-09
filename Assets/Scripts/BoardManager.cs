using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Contract;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Board = Contract.Board;
using Random = UnityEngine.Random;

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
    Dictionary<Vector2Int, TileView> tileViews = new();
    Dictionary<PawnId, PawnView> pawnViews = new();
    
    Task<bool> updateNetworkStateTask;
    Task<int> currentContractTask;
    PhaseBase currentPhase;
    
    bool polling;
    Coroutine pollingCoroutine;
    
    public void StartBoardManager()
    {
        GameNetworkState netState = new(StellarManager.networkState);
        Initialize(netState);
        OnNetworkStateUpdated(); //only invoke this directly once on start
    }

    public void CloseBoardManager()
    {
        // Stop polling immediately
        PollForPhaseChange(false);
        
        // Unsubscribe from events
        StellarManager.OnNetworkStateUpdated -= OnNetworkStateUpdated;
        
        // Cancel any pending tasks by clearing references
        updateNetworkStateTask = null;
        currentContractTask = null;
        
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
        AudioManager.instance.PlayMusic(MusicTrack.BATTLE_MUSIC);
        
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
        
        initialized = true;
    }
    
    void OnNetworkStateUpdated()
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
        Debug.Log("TestBoardManager::OnNetworkStateUpdated");
        GameNetworkState netState = new(StellarManager.networkState);
        bool shouldPoll = !netState.IsMySubphase();
        if (currentPhase != null)
        {
            if (GameNetworkState.Compare(netState, currentPhase.cachedNetState))
            {
                Debug.Log("OnNetworkStateUpdated skipped this nothingburger");
                
                PollForPhaseChange(shouldPoll);
                return;
            }
        }
        // Reset task references since we got the update
        updateNetworkStateTask = null;
        currentContractTask = null;

        // Check if phase actually changed
        Phase newPhase = netState.lobbyInfo.phase;
        bool phaseChanged = currentPhase == null || 
                          currentPhase.cachedNetState.lobbyInfo.phase != newPhase ||
                          currentPhase.cachedNetState.lobbyInfo.subphase != netState.lobbyInfo.subphase ||
                          currentPhase.cachedNetState.gameState.turn != netState.gameState.turn;
        if (phaseChanged)
        {
            PhaseBase nextPhase = newPhase switch
            {
                Phase.SetupCommit => new SetupCommitPhase(),
                Phase.MoveCommit => new MoveCommitPhase(),
                Phase.MoveProve => new MoveProvePhase(),
                Phase.RankProve => new RankProvePhase(),
                Phase.Finished or Phase.Aborted or Phase.Lobby => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(),
            };
            // set phase
            currentPhase?.ExitState(clickInputManager, guiGame);
            currentPhase = nextPhase;
            currentPhase.EnterState(PhaseStateChanged, CallContract, GetNetworkState, clickInputManager, tileViews, pawnViews, guiGame);
        }
        
        // Always update the network state
        currentPhase.UpdateNetworkState(netState);
        PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(currentPhase)));
        
        // Manage polling based on whose turn it is
        PollForPhaseChange(shouldPoll);
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
        
        if (updateNetworkStateTask != null && !updateNetworkStateTask.IsCompleted)
        {
            Debug.LogWarning("GetNetworkState already has a task in progress");
            return false;
        }
        // Stop polling while network request is in progress
        PollForPhaseChange(false);
        updateNetworkStateTask = StellarManager.UpdateState();
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
        
        if (currentContractTask != null && !currentContractTask.IsCompleted)
        {
            Debug.LogWarning("CallContract already has a task in progress");
            return false;
        }
        // Stop polling while contract call is in progress
        PollForPhaseChange(false);
        
        Task<int> contractTask = null;
        switch (req)
        {
            case CommitSetupReq commitSetupReq:
                contractTask = StellarManager.CommitSetupRequest(commitSetupReq);
                break;
            case CommitMoveReq commitMoveReq:
                if (req2 is not ProveMoveReq followUpProveMoveReq)
                {
                    throw new ArgumentNullException();
                }
                contractTask = StellarManager.CommitMoveRequest(commitMoveReq, followUpProveMoveReq);
                break;
            case ProveMoveReq proveMoveReq:
                contractTask = StellarManager.ProveMoveRequest(proveMoveReq);
                break;
            case ProveRankReq proveRankReq:
                contractTask = StellarManager.ProveRankRequest(proveRankReq);
                break;
            case JoinLobbyReq:
            case MakeLobbyReq:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(req));
        }
        currentContractTask = contractTask;
        return true;
    }

    
    void PollForPhaseChange(bool poll)
    {
        polling = poll;
        if (poll)
        {
            // Start polling if not already running
            if (pollingCoroutine == null)
            {
                pollingCoroutine = CoroutineRunner.instance.StartCoroutine(PollCoroutine());
            }
        }
        else
        {
            // Stop polling
            if (pollingCoroutine != null)
            {
                CoroutineRunner.instance.StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }
        }
    }
    
    IEnumerator PollCoroutine()
    {
        while (polling && initialized)
        {
            yield return new WaitForSeconds(0.5f);
            if (polling && initialized) // Check again in case it was stopped during the wait
            {
                GetNetworkState();
            }
        }
        
        // Clean up coroutine reference when done
        pollingCoroutine = null;
    }
}

public abstract class PhaseBase
{
    public GameNetworkState cachedNetState;
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
            foreach (PawnState pawn in cachedNetState.gameState.pawns.Where(p => p.GetTeam() == cachedNetState.userTeam))
            {
                pendingCommits[pawn.pawn_id] = null;
            }
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
        if (clicked)
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
            if (GetRemainingRank(rank) > 0)
            {
                tool = SetupInputTool.ADD;
            }
            
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

public class MoveCommitPhase: PhaseBase
{
    public Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairs = new();
    public Vector2Int? selectedStartPosition = null;
    Vector2Int? pendingTargetPosition = null;
    public MoveInputTool moveInputTool = MoveInputTool.NONE;
    public HashSet<Vector2Int> validTargetPositions = new();

    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.movement.OnSubmitMoveButton = set ? OnSubmit : null;
        guiGame.movement.OnRefreshButton = set ? OnRefresh : null;
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        // movePairs is a local-only plan for the current submission; do not populate from network
        if (!cachedNetState.IsMySubphase())
        {
            movePairs.Clear();
        }
    }

    void OnSubmit()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }

        if (movePairs.Count == 0)
        {
            throw new InvalidOperationException("movePairs can't be empty");
        }

        List<HiddenMove> hiddenMoves = new List<HiddenMove>();
        List<byte[]> hiddenMoveHashes = new List<byte[]>();
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

            HiddenMove hiddenMove = new HiddenMove()
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
        List<GameOperation> operations = new List<GameOperation>();
        Vector2Int oldHoveredPos = hoveredPos;
        hoveredPos = inHoveredPos;
        if (clicked)
        {
            switch (moveInputTool)
            {
                case MoveInputTool.NONE:
                    break;
                case MoveInputTool.SELECT:
                    {
                        // Remove any existing staged move whose start position equals the clicked position
                        List<PawnId> keysToRemove = movePairs.Where(kv => kv.Value.Item1 == hoveredPos).Select(kv => kv.Key).ToList();
                        if (keysToRemove.Count > 0)
                        {
                            foreach (PawnId key in keysToRemove)
                            {
                                movePairs.Remove(key);
                            }
                            operations.Add(new MovePairUpdated(new Dictionary<PawnId, (Vector2Int, Vector2Int)>(movePairs), null, this));
                        }
                        operations.Add(SelectPosition(hoveredPos));
                        break;
                    }
                case MoveInputTool.TARGET:
                    operations.Add(TargetPosition(hoveredPos));
                    break;
                case MoveInputTool.CLEAR_SELECT:
                    operations.Add(SelectPosition(null));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        moveInputTool = GetNextTool(hoveredPos);
        operations.Add(new MoveHoverChanged(oldHoveredPos, hoveredPos, this));
        InvokeOnPhaseStateChanged(new PhaseChangeSet(operations));
    }
    
    MoveInputTool GetNextTool(Vector2Int hoveredPosition)
    {
        if (!cachedNetState.IsMySubphase())
        {
            return MoveInputTool.NONE;
        }
        if (cachedNetState.GetTileChecked(hoveredPosition) == null)
        {
            return MoveInputTool.NONE;
        }
        // a pawn is already selected and target hasn't been selected yet
        if (selectedStartPosition is Vector2Int selectedStart && cachedNetState.GetAlivePawnFromPosChecked(selectedStart) is PawnState selectedPawn)
        {
            // check if hovering over another selectable pawn
            if (cachedNetState.GetAlivePawnFromPosChecked(hoveredPosition) is PawnState hoveredPawn 
                && cachedNetState.CanUserMovePawn(hoveredPawn.pawn_id))
            {
                return MoveInputTool.SELECT;
            }
            // check if hovering over a valid target
            if (validTargetPositions.Contains(hoveredPosition))
            {
                return MoveInputTool.TARGET;
            }
            // invalid tile when pawn is selected
            return MoveInputTool.CLEAR_SELECT;
        }
        // no pawn is selected
        else
        {
            // check if hovering over another selectable pawn
            if (cachedNetState.GetAlivePawnFromPosChecked(hoveredPosition) is PawnState hoveredPawn 
                && cachedNetState.CanUserMovePawn(hoveredPawn.pawn_id))
            {
                return MoveInputTool.SELECT;
            }
            return MoveInputTool.NONE;
        }
    }
    
    MovePosSelected SelectPosition(Vector2Int? startPosition)
    {
        Vector2Int? previousSelectedStartPosition = selectedStartPosition;
        selectedStartPosition = startPosition;
        validTargetPositions = ComputeTargetablePositionsForSelected();
        return new(previousSelectedStartPosition, selectedStartPosition, validTargetPositions, new Dictionary<PawnId, (Vector2Int, Vector2Int)>(movePairs));
    }

    MovePairUpdated TargetPosition(Vector2Int? targetPosition)
    {
        Vector2Int? previousTargetPosition = pendingTargetPosition;
        pendingTargetPosition = targetPosition;
        PawnId? changedPawnId = null;
        if (selectedStartPosition is Vector2Int start && pendingTargetPosition is Vector2Int end)
        {
            // Prevent duplicate pawn moves; replace if start already present
            if (cachedNetState.GetAlivePawnFromPosChecked(start) is PawnState selectedPawn)
            {
                movePairs[selectedPawn.pawn_id] = (start, end);
                changedPawnId = selectedPawn.pawn_id;
            }
        }
        // Clear selection after creating/setting a pair
        selectedStartPosition = null;
        pendingTargetPosition = null;
        validTargetPositions.Clear();
        return new(new Dictionary<PawnId, (Vector2Int, Vector2Int)>(movePairs), changedPawnId, this);
    }

    HashSet<Vector2Int> ComputeTargetablePositionsForSelected()
    {
        HashSet<Vector2Int> computedTargets = new HashSet<Vector2Int>();
        if (selectedStartPosition is not Vector2Int selectedPosition)
        {
            return computedTargets;
        }
        PawnState? maybeSelectedPawn = cachedNetState.GetAlivePawnFromPosChecked(selectedPosition);
        if (maybeSelectedPawn is not PawnState selectedPawn)
        {
            return computedTargets;
        }
        // Base legal targets from current state
        HashSet<Vector2Int> baseLegalTargetPositions = cachedNetState.GetValidMoveTargetList(selectedPawn.pawn_id);
        // Planned moves bookkeeping
        HashSet<Vector2Int> plannedMovingStartPositions = new HashSet<Vector2Int>();
        HashSet<Vector2Int> plannedTargetPositions = new HashSet<Vector2Int>();
        foreach (KeyValuePair<PawnId, (Vector2Int, Vector2Int)> kv in movePairs)
        {
            Vector2Int plannedStart = kv.Value.Item1;
            plannedMovingStartPositions.Add(plannedStart);
            plannedTargetPositions.Add(kv.Value.Item2);
        }
        // Start with base targets
        foreach (Vector2Int baseTarget in baseLegalTargetPositions)
        {
            computedTargets.Add(baseTarget);
        }
        // Allow stepping into allied-occupied tiles if that ally is moving away this turn
        // and no friendly move targets that tile already
        // (Only applies to immediate neighbor targets; scouts LOS handling is kept server-side)
        Vector2Int[] neighborDirections = Shared.GetDirections(selectedPosition, cachedNetState.lobbyParameters.board.hex);
        foreach (Vector2Int direction in neighborDirections)
        {
            Vector2Int neighborPosition = selectedPosition + direction;
            // Skip out-of-board
            if (cachedNetState.GetTileChecked(neighborPosition) is not Contract.TileState neighborTile || !neighborTile.passable)
            {
                continue;
            }
            // If an allied pawn occupies neighbor
            if (cachedNetState.GetAlivePawnFromPosChecked(neighborPosition) is PawnState ally && ally.GetTeam() == cachedNetState.userTeam)
            {
                // Include neighbor if ally is moving away and no friendly move is targeting neighbor
                bool allyIsMovingAway = plannedMovingStartPositions.Contains(neighborPosition);
                bool someoneTargetsNeighbor = plannedTargetPositions.Contains(neighborPosition);
                if (allyIsMovingAway && !someoneTargetsNeighbor)
                {
                    computedTargets.Add(neighborPosition);
                }
            }
        }
        // Disallow any tiles that are already targeted by our own planned moves
        foreach (Vector2Int blockedByPlannedMovePosition in plannedTargetPositions)
        {
            computedTargets.Remove(blockedByPlannedMovePosition);
        }
        return computedTargets;
    }
}

public class MoveProvePhase: PhaseBase
{
    public List<Vector2Int> selectedPos = null;
    public List<Vector2Int> targetPos = null;
    
    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.movement.OnRefreshButton = set ? OnRefresh : null;
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        byte[][] moveHashes = cachedNetState.GetUserMove().move_hashes;
        foreach (byte[] moveHash in moveHashes)
        {
            if (CacheManager.GetHiddenMove(moveHash) is HiddenMove hiddenMove)
            {
                selectedPos.Add(hiddenMove.start_pos);
                targetPos.Add(hiddenMove.target_pos);
            }
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
        byte[][] moveHashes = cachedNetState.GetUserMove().move_hashes;
        List<HiddenMove> moveProofs = new List<HiddenMove>();
        foreach (byte[] moveHash in moveHashes)
        {
            if (CacheManager.GetHiddenMove(moveHash) is not HiddenMove hiddenMove)
            {
                throw new Exception($"Could not find move with move hash {moveHash}");
            }
            moveProofs.Add(hiddenMove);
        }
        
        ProveMoveReq proveMoveReq = new()
        {
            lobby_id = cachedNetState.lobbyInfo.index,
            move_proofs = moveProofs.ToArray(),
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
    public List<Vector2Int> selectedPos = new();
    public List<Vector2Int> targetPos = new();
    
    protected override void SetGui(GuiGame guiGame, bool set)
    {
        guiGame.movement.OnRefreshButton = set ? OnRefresh : null;
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        foreach (HiddenMove hiddenMove in cachedNetState.GetUserMove().move_proofs)
        {
            selectedPos.Add(hiddenMove.start_pos);
            targetPos.Add(hiddenMove.target_pos);
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

public record SetupHoverChanged(Vector2Int oldHoveredPos, Vector2Int newHoveredPos, SetupCommitPhase phase) : GameOperation;

public record SetupRankCommitted(Dictionary<PawnId, Rank?> oldPendingCommits, SetupCommitPhase phase) : GameOperation;
public record SetupRankSelected(Rank? oldSelectedRank, SetupCommitPhase phase) : GameOperation;

public record MoveHoverChanged(Vector2Int oldPos, Vector2Int newPos, MoveCommitPhase phase) : GameOperation;
public record MovePosSelected(Vector2Int? oldPos, Vector2Int? newPos, HashSet<Vector2Int> targetablePositions, Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairsSnapshot) : GameOperation;
public record MovePairUpdated(Dictionary<PawnId, (Vector2Int, Vector2Int)> movePairsSnapshot, PawnId? changedPawnId, MoveCommitPhase phase) : GameOperation;

public record NetStateUpdated(PhaseBase phase) : GameOperation;


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
