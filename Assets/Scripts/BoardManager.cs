using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Contract;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Board = Contract.Board;
using Random = UnityEngine.Random;

public class BoardManager : MonoBehaviour
{
    public bool activated;
    public bool initialized;
    // never mutate these externally
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public TestClickInputManager clickInputManager;
    public Vortex vortex;
    // UI stuff generally gets done in the phase
    [FormerlySerializedAs("guiTestGame")] public GuiGame guiGame;
    // generally doesn't change after lobby is set in StartGame
    public BoardDef boardDef;
    // internal game state. call OnStateChanged when updating these. only StartGame can make new views
    public Dictionary<Vector2Int, TileView> tileViews = new();
    public Dictionary<PawnId, PawnView> pawnViews = new();
    // last known lobby
    //public GameNetworkState cachedNetworkState;
    public Phase lastPhase;
    public PhaseBase currentPhase;
    public Transform cameraBounds;

    public Transform waveOrigin1;
    public Transform waveOrigin2;

    public event Action<Vector2Int, TileView, PawnView, PhaseBase> OnGameHover;
    
    void Start()
    {
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }
    
    public void StartBoardManager()
    {
        activated = true;
        OnNetworkStateUpdated(); //only invoke this directly once on start
    }

    void Initialize(GameNetworkState networkState)
    {
        // Clear existing tileviews and replace
        foreach (TileView tile in tileViews.Values)
        {
            Destroy(tile.gameObject);
        }
        tileViews.Clear();
        Board board = networkState.lobbyParameters.board;
        grid.SetBoard(board.hex);
        foreach (TileState tile in board.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(tile, board.hex);
            tileViews.Add(tile.pos, tileView);
        }
        // Clear any existing pawnviews and replace
        foreach (PawnView pawnView in pawnViews.Values)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();
        foreach (PawnState pawn in networkState.gameState.pawns)
        {
            GameObject pawnObject = Instantiate(pawnPrefab, transform);
            PawnView pawnView = pawnObject.GetComponent<PawnView>();
            pawnView.Initialize(pawn, tileViews[pawn.pos]);
            pawnViews.Add(pawn.pawn_id, pawnView);
        }
    }
    
    void OnNetworkStateUpdated()
    {
        if (!activated)
        {
            return;
        }
        Debug.Log("TestBoardManager::OnNetworkStateUpdated");
        GameNetworkState netState = new(StellarManager.networkState);
        if (!initialized)
        {
            Initialize(netState);
            clickInputManager.Initialize(this);
            CacheManager.Initialize(netState.address, netState.lobbyInfo.index);
            lastPhase = Phase.Lobby;
            initialized = true;
        }
        Phase nextPhase = netState.lobbyInfo.phase;
        if (nextPhase != lastPhase)
        {
            switch (netState.lobbyInfo.phase)
            {
                case Phase.SetupCommit:
                    SetPhase(new SetupCommitPhase(this, guiGame.setup, netState, clickInputManager));
                    break;
                case Phase.MoveCommit:
                    SetPhase(new MoveCommitPhase(this, guiGame.movement, netState, clickInputManager));
                    break;
                case Phase.MoveProve:
                    SetPhase(new MoveProvePhase(this, guiGame.movement, netState, clickInputManager));
                    break;
                case Phase.RankProve:
                    SetPhase(new RankProvePhase(this, guiGame.movement, netState, clickInputManager));
                    break;
                case Phase.Finished:
                case Phase.Aborted:
                case Phase.Lobby:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
            lastPhase = nextPhase;
        }
        else
        {
            currentPhase.UpdateNetworkState(netState);
        }
        // directly invoke this
        PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(netState)));
    }
    
    void SetPhase(PhaseBase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState(PhaseStateChanged);
    }
    
    void PhaseStateChanged(IPhaseChangeSet changes)
    {
        // Central callback - receives all operations and broadcasts to views
        // boardmanager handles its own stuff first
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupHoverChanged setupHoverChanged:
                    UpdateCursor(setupHoverChanged.phase.setupInputTool);
                    break;
                case MoveHoverChanged moveHoverChanged:
                    UpdateCursor(moveHoverChanged.phase.moveInputTool);
                    break;
            }
        }
        // update tiles and pawns
        foreach (TileView tileView in tileViews.Values)
        {
            tileView.PhaseStateChanged(currentPhase, changes);
        }
        foreach (PawnView pawnView in pawnViews.Values)
        {
            pawnView.PhaseStateChanged(currentPhase, changes);
        }
        // update gui
        guiGame.PhaseStateChanged(currentPhase, changes);
    }
    
    static void UpdateCursor(SetupInputTool tool)
    {
        switch (tool)
        {
            case SetupInputTool.NONE:
                CursorController.ChangeCursor(CursorType.DEFAULT);
                break;
            case SetupInputTool.ADD:
                CursorController.ChangeCursor(CursorType.PLUS);
                break;
            case SetupInputTool.REMOVE:
                CursorController.ChangeCursor(CursorType.MINUS);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    static void UpdateCursor(MoveInputTool tool)
    {
        switch (tool)
        {
            case MoveInputTool.NONE:
                CursorController.ChangeCursor(CursorType.DEFAULT);
                break;
            case MoveInputTool.SELECT:
                CursorController.ChangeCursor(CursorType.PLUS);
                break;
            case MoveInputTool.TARGET:
                CursorController.ChangeCursor(CursorType.TARGET);
                break;
            case MoveInputTool.CLEAR_SELECT:
                CursorController.ChangeCursor(CursorType.MINUS);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public abstract class PhaseBase
{
    protected GameNetworkState cachedNetState;
    protected GameNetworkState oldNetState;
    public Vector2Int hoveredPos;
    public bool mouseInputEnabled;
    
    public Dictionary<Vector2Int, TileView> tileViews;
    public Dictionary<PawnId, PawnView> pawnViews;

    protected Action<IPhaseChangeSet> OnPhaseStateChanged;
    
    protected PhaseBase(BoardManager bm, GameNetworkState netState, TestClickInputManager clickInputManager)
    {
        tileViews = bm.tileViews;
        pawnViews = bm.pawnViews;
        clickInputManager.OnMouseInput = OnMouseInput;
        oldNetState = netState;
        cachedNetState = netState;
    }
    
    public virtual void EnterState(Action<IPhaseChangeSet> inOnPhaseStateChanged)
    {
        OnPhaseStateChanged = inOnPhaseStateChanged;
    }

    public virtual void ExitState()
    {
        OnPhaseStateChanged = null;
    }

    public virtual void UpdateNetworkState(GameNetworkState netState)
    {
        oldNetState = cachedNetState;
        cachedNetState = netState;
    }


    protected abstract void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState);
    public abstract void OnMouseInput(Vector2Int hoveredPos, bool clicked);
}
public class SetupCommitPhase : PhaseBase
{
    public Dictionary<PawnId, Rank?> pendingCommits;
    public Rank? selectedRank;
    public SetupInputTool setupInputTool;
    public SetupCommitPhase(BoardManager bm, GuiSetup guiSetup, GameNetworkState inNetworkState, TestClickInputManager clickInputManager)
        : base(bm, inNetworkState, clickInputManager)
    {
        pendingCommits = new();
        foreach (PawnState pawn in cachedNetState.gameState.pawns.Where(p => p.GetTeam() == cachedNetState.userTeam))
        {
            pendingCommits[pawn.pawn_id] = null;
        }

        setupInputTool = SetupInputTool.NONE;
        guiSetup.OnClearButton = OnClear;
        guiSetup.OnAutoSetupButton = OnAutoSetup;
        guiSetup.OnRefreshButton = OnRefresh;
        guiSetup.OnSubmitButton = OnSubmit;
        guiSetup.OnEntryClicked = OnEntryClicked;
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
    
    void OnClear()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }

        foreach ((PawnId pawnId, Rank? _) in pendingCommits)
        {
            pendingCommits[pawnId] = null;
        }
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
    }

    void OnAutoSetup()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        foreach ((PawnId pawnId, Rank? _) in pendingCommits)
        {
            pendingCommits[pawnId] = null;
        }
        Dictionary<Vector2Int, Rank> autoCommitments = cachedNetState.AutoSetup(cachedNetState.userTeam);
        foreach ((Vector2Int pos, Rank rank) in autoCommitments)
        {
            PawnState pawn = cachedNetState.GetPawnFromPos(pos);
            pendingCommits[pawn.pawn_id] = rank;
        }
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
    }

    void OnRefresh()
    {
        _ = StellarManager.UpdateState();
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
        CacheManager.StoreHiddenRanks(hiddenRanks.ToArray(), cachedNetState.address, cachedNetState.lobbyInfo.index);
        Setup setup = new()
        {
            salt = Globals.RandomSalt(),
            setup_commits = commits.ToArray(),
        };
        _ = StellarManager.CommitSetupRequest(cachedNetState.lobbyInfo.index, setup);
    }

    void OnEntryClicked(Rank clickedRank)
    {
        Debug.Log("OnEntryClicked" + clickedRank);
        Rank? oldSelectedRank = selectedRank;
        if (selectedRank is Rank rank && rank == clickedRank)
        {
            selectedRank = null;
        }
        else
        {
            selectedRank = clickedRank;
        }
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(new SetupRankSelected(oldSelectedRank, this)));
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
            throw new InvalidOperationException("Rank is not available");
        }
        PawnState pawn = cachedNetState.GetPawnFromPos(pos);
        pendingCommits[pawn.pawn_id] = selectedRank;
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        Debug.Log($"commit position {pawn.pawn_id} {pos} {selectedRank}");
        return new(oldPendingCommits, this);
    }

    protected override void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState)
    {
        //throw new NotImplementedException();
    }

    public override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        if (!mouseInputEnabled)
        {
            return;
        }
        Debug.Log($"On Hover {hoveredPos}");
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
                case SetupInputTool.REMOVE:
                    SetupRankCommitted rankCommittedOperation = CommitPosition(hoveredPos);
                    operations.Add(rankCommittedOperation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        SetupInputTool tool = GetNextTool();
        operations.Add(new SetupHoverChanged(oldHoveredPos, this));
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(operations));
    }

    SetupInputTool GetNextTool()
    {
        SetupInputTool tool = SetupInputTool.NONE;
        // if hovered over a already committed pawn
        if (cachedNetState.GetPawnFromPosChecked(hoveredPos) is PawnState pawn && pendingCommits[pawn.pawn_id] != null)
        {
            tool = SetupInputTool.REMOVE;
        }
        else if (selectedRank is Rank rank && cachedNetState.GetTileChecked(hoveredPos) is TileState tile && tile.setup == cachedNetState.userTeam && GetRemainingRank(rank) > 0)
        {
            tool = SetupInputTool.ADD;
        }
        return tool;
    }
}

public class MoveCommitPhase: PhaseBase
{
    public Vector2Int? selectedPos;
    public Vector2Int? targetPos;
    public MoveInputTool moveInputTool;
    public MoveCommitPhase(BoardManager bm, GuiMovement guiMovement, GameNetworkState inNetworkState, TestClickInputManager clickInputManager): base(bm, inNetworkState, clickInputManager)
    {
        tileViews = bm.tileViews;

    }
    
    MovePosSelected SelectPosition(Vector2Int? pos)
    {
        Vector2Int? oldPos = selectedPos;
        selectedPos = pos;
        return new MovePosSelected(oldPos, this);
    }

    MoveTargetSelected TargetPosition(Vector2Int? target)
    {
        Vector2Int? oldTarget = targetPos;
        targetPos = target;
        return new MoveTargetSelected(oldTarget, this);
    }
    
    protected override void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState)
    {
        //throw new NotImplementedException();
    }

    public override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
        if (!mouseInputEnabled)
        {
            return;
        }
        Debug.Log($"On Hover {hoveredPos}");
        List<GameOperation> operations = new();
        Vector2Int oldHoveredPos = hoveredPos;
        hoveredPos = inHoveredPos;
        if (clicked)
        {
            switch (moveInputTool)
            {
                case MoveInputTool.NONE:
                    break;
                case MoveInputTool.SELECT:
                    MovePosSelected posSelected = SelectPosition(hoveredPos);
                    MoveTargetSelected targetSelectedClear1 = TargetPosition(null);
                    operations.Add(posSelected);
                    operations.Add(targetSelectedClear1);
                    break;
                case MoveInputTool.TARGET:
                    MoveTargetSelected targetSelected = TargetPosition(hoveredPos);
                    operations.Add(targetSelected);
                    break;
                case MoveInputTool.CLEAR_SELECT:
                    MovePosSelected posUnselected = SelectPosition(null);
                    MoveTargetSelected targetSelectedClear2 = TargetPosition(null);
                    operations.Add(posUnselected);
                    operations.Add(targetSelectedClear2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        moveInputTool = GetNextTool(hoveredPos);
        operations.Add(new MoveHoverChanged(oldHoveredPos, this));
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(operations));
        
    }

    MoveInputTool GetNextTool(Vector2Int pos)
    {
        if (cachedNetState.GetTileChecked(hoveredPos) == null)
        {
            return MoveInputTool.NONE;
        }
        
        // a pawn is already selected
        if (selectedPos is Vector2Int selectedPos2 && cachedNetState.GetPawnFromPosChecked(selectedPos2) is PawnState selectedPawn)
        {
            // if hovering over selected pawn we do nothing
            if (selectedPawn.pos == pos)
            {
                return MoveInputTool.NONE;
            }
            // check if hovering over another selectable pawn
            if (cachedNetState.GetPawnFromPosChecked(hoveredPos) is PawnState hoveredPawn 
                && cachedNetState.CanUserMovePawn(hoveredPawn.pawn_id))
            {
                return MoveInputTool.SELECT;
            }
            // check if hovering over a valid target
            if (cachedNetState.GetValidMoveTargetList(selectedPawn.pawn_id).Contains(pos))
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
            if (cachedNetState.GetPawnFromPosChecked(hoveredPos) is PawnState hoveredPawn 
                && cachedNetState.CanUserMovePawn(hoveredPawn.pawn_id))
            {
                return MoveInputTool.SELECT;
            }
            return MoveInputTool.NONE;
        }
    }

}

public class MoveProvePhase: PhaseBase
{
    public MoveProvePhase(BoardManager bm, GuiMovement guiMovement, GameNetworkState inNetworkState, TestClickInputManager clickInputManager): base(bm, inNetworkState, clickInputManager)
    {
    }

    protected override void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState)
    {
        throw new NotImplementedException();
    }

    public override void OnMouseInput(Vector2Int hoveredPos, bool clicked)
    {
    
    }


}

public class RankProvePhase: PhaseBase
{
    public RankProvePhase(BoardManager bm, GuiMovement guiMovement, GameNetworkState inNetworkState, TestClickInputManager clickInputManager): base(bm, inNetworkState, clickInputManager)
    {
    }
    
    protected override void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState)
    {
        throw new NotImplementedException();
    }

    public override void OnMouseInput(Vector2Int hoveredPos, bool clicked)
    {
        
    }


}


namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}
public abstract record GameOperation;

public record SetupHoverChanged(Vector2Int oldHoveredPos, SetupCommitPhase phase) : GameOperation;

public record SetupRankCommitted(Dictionary<PawnId, Rank?> oldPendingCommits, SetupCommitPhase phase) : GameOperation;
public record SetupRankSelected(Rank? oldSelectedRank, SetupCommitPhase phase) : GameOperation;

public record MoveHoverChanged(Vector2Int oldPos, MoveCommitPhase phase) : GameOperation;
public record MovePosSelected(Vector2Int? oldPos, MoveCommitPhase phase) : GameOperation;
public record MoveTargetSelected(Vector2Int? oldTarget, MoveCommitPhase phase) : GameOperation;

public record NetStateUpdated(GameNetworkState netState) : GameOperation;


public interface IPhaseChangeSet
{
    List<GameOperation> operations { get; }
    bool phaseChanged { get; }

    public NetStateUpdated NetStateUpdated();
}

public class PhaseChangeSet : IPhaseChangeSet
{
    public List<GameOperation> operations { get; }
    public bool phaseChanged { get; }

    public PhaseChangeSet(List<GameOperation> inOperations, bool inPhaseChanged = false)
    {
        operations = inOperations;
        phaseChanged = inPhaseChanged;
    }

    public PhaseChangeSet(GameOperation inOperation)
    {
        operations = new()
        {
            inOperation,
        };
    }

    public NetStateUpdated NetStateUpdated()
    {
        foreach (GameOperation op in operations)
        {
            if (op is NetStateUpdated netStateUpdated)
            {
                return netStateUpdated;
            }
        }
        return null;
    }
}

//
// public class SetupClientState
// {
//     public bool changedByInput;
//     //public Dictionary<uint, PawnCommit> commitments;
//     public Dictionary<Rank, uint> maxRanks;
//     public SetupCommit[] lockedCommits;
//     public Dictionary<Vector2Int, Rank?> pendingCommits;
//     public Rank? selectedRank;
//     public bool committed;
//     public bool opponentCommitted;
//     public bool submitted;
//     public bool opponentSubmitted;
//     public Team team;
//     
//     public void SetSelectedRank(Rank? rank)
//     {
//         selectedRank = rank;
//         changedByInput = true;
//     }
//
//     public void SetPendingCommit(Vector2Int pos, Rank? rank)
//     {
//         pendingCommits[pos] = rank;
//         changedByInput = true;
//     }
//
//     public void ClearPendingCommits()
//     {
//         foreach (Vector2Int key in pendingCommits.Keys.ToArray())
//         {
//             pendingCommits[key] = null;
//         }
//     }
//     public int GetPendingRemainingCount(Rank rank)
//     {
//         return (int)maxRanks[rank] - pendingCommits.Values.Count(c => c == rank);
//     }
// }
//
// public class SetupPhase : IPhase
// {
//     BoardManager bm;
//     GuiSetup setupGui;
//     SetupInputTool tool;
//     
//     public SetupClientState clientState;
//     bool attemptedProveSetup;
//     
//     public SetupPhase(BoardManager bm, GuiSetup setupGui, GameNetworkState networkState)
//     {
//         this.bm = bm;
//         this.setupGui
//         setupGui = setupGui;
//         tool = SetupInputTool.NONE;
//         // TODO: figure out a better way to tell gui to do stuff
//         bm.guiGame.SetCurrentElement(setupGui, networkState);
//     }
//
//     public void RefreshGui()
//     {
//         setupGui.Refresh(clientState);
//     }
//     
//     void ResetClientState(GameNetworkState networkState)
//     {
//         Debug.Log("SetupTestPhase.ResetClientState");
//         UserState userState = networkState.GetUserState();
//         Dictionary<Rank, uint> maxRankDictionary = new();
//         foreach (MaxRank maxRank in networkState.lobbyParameters.max_ranks)
//         {
//             maxRankDictionary[maxRank.rank] = maxRank.max;
//         }
//         bool committed = userState.setup_hash.Any(b => b != 0);
//         bool opponentCommitted = networkState.GetOpponentUserState().setup_hash.Any(b => b != 0);
//         bool submitted = userState.setup.Length >= 1;
//         bool opponentSubmitted = userState.setup.Length > 1;
//         PawnCommit[] lockedCommits = Array.Empty<PawnCommit>();
//         if (submitted) // if submitted then we can just get what we submitted
//         {
//             lockedCommits = userState.setup;
//         }
//         else if (committed) // if committed but not submitted we get from cache
//         {
//             ProveSetupReq proveSetupReq = CacheManager.LoadProveSetupReq(userState.setup_hash);
//             lockedCommits = proveSetupReq.setup;
//         }
//         Dictionary<Vector2Int, Rank?> pendingCommits = bm.boardDef.tiles
//             .Where(tile => tile.IsTileSetupAllowed(networkState.clientTeam))
//             .ToDictionary<Tile, Vector2Int, Rank?>(tile => tile.pos, tile => null);
//         SetupClientState newSetupClientState = new()
//         {
//             selectedRank = null,
//             maxRanks = maxRankDictionary,
//             lockedCommits = lockedCommits,
//             pendingCommits = pendingCommits,
//             committed = committed,
//             opponentCommitted = opponentCommitted,
//             submitted = submitted,
//             opponentSubmitted = opponentSubmitted,
//             team = networkState.clientTeam,
//         };
//         clientState = newSetupClientState;
//     }
//     
//     public void EnterState()
//     {
//         setupGui.OnClearButton += OnClear;
//         setupGui.OnAutoSetupButton += OnAutoSetup;
//         setupGui.OnRefreshButton += OnRefresh;
//         setupGui.OnSubmitButton += OnSubmit;
//         setupGui.OnRankEntryClicked += OnRankEntryClicked;
//
//     }
//     
//     public void ExitState()
//     {
//         setupGui.OnClearButton -= OnClear;
//         setupGui.OnAutoSetupButton -= OnAutoSetup;
//         setupGui.OnRefreshButton -= OnRefresh;
//         setupGui.OnSubmitButton -= OnSubmit;
//     }
//
//     public void Update()
//     {
//         
//     }
//
//     public void OnHover(Vector2Int hoveredPos, TileView tileView, PawnView pawnView)
//     {
//         SetupInputTool newTool;
//         if (!tileView)
//         {
//             newTool = SetupInputTool.NONE;
//         }
//         else if (clientState.committed)
//         {
//             newTool = SetupInputTool.NONE;
//         }
//         // if hovered over a valid setup tile
//         else if (clientState.pendingCommits.TryGetValue(hoveredPos, out Rank? commit))
//         {
//             // if hovered over tile with a commit already on it
//             if (commit.HasValue)
//             {
//                 newTool = SetupInputTool.REMOVE;
//             }
//             else
//             {
//                 // if has selectedrank
//                 if (clientState.selectedRank.HasValue)
//                 {
//                     // if selectedrank is not exhausted
//                     if (clientState.GetPendingRemainingCount(clientState.selectedRank.Value) > 0)
//                     {
//                         newTool = SetupInputTool.ADD;
//                     }
//                     else
//                     {
//                         newTool = SetupInputTool.NONE;
//                     }
//                 }
//                 else
//                 {
//                     newTool = SetupInputTool.NONE;
//                 }
//             }
//         }
//         else
//         {
//             newTool = SetupInputTool.NONE;
//         }
//
//         if (tool != newTool)
//         {
//             tool = newTool;
//             switch (tool)
//             {
//                 case SetupInputTool.NONE:
//                     CursorController.ChangeCursor(CursorType.DEFAULT);
//                     break;
//                 case SetupInputTool.ADD:
//                     CursorController.ChangeCursor(CursorType.PLUS);
//                     break;
//                 case SetupInputTool.REMOVE:
//                     CursorController.ChangeCursor(CursorType.MINUS);
//                     break;
//                 default:
//                     throw new ArgumentOutOfRangeException();
//             }
//         }
//     }
//
//     public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
//     {
//         Debug.Log("OnClick from setup Phase");
//         if (tileView == null) return;
//         if (clickedPos == Globals.Purgatory) return;
//         switch (tool)
//         {
//
//             case SetupInputTool.NONE:
//                 break;
//             case SetupInputTool.ADD:
//                 Assert.IsTrue(clientState.selectedRank != null, "clientState.selectedRank != null");
//                 clientState.SetPendingCommit(clickedPos, clientState.selectedRank.Value);
//                 break;
//             case SetupInputTool.REMOVE:
//                 clientState.SetPendingCommit(clickedPos, null);
//                 break;
//             default:
//                 throw new ArgumentOutOfRangeException();
//         }
//         ClientStateChanged();
//     }
//
//     public void OnNetworkGameStateChanged(GameNetworkState networkState)
//     {
//         ResetClientState(networkState);
//         if (clientState.committed && clientState.opponentCommitted && !clientState.submitted && !attemptedProveSetup)
//         {
//             attemptedProveSetup = true;
//             _ = StellarManager.ProveSetupRequest();
//         }
//     }
//
//     void ClientStateChanged()
//     {
//         Debug.Log("SetupClientState.ClientStateChanged");
//         clientState.changedByInput = false;
//         bm.OnlyClientGameStateChanged();
//     }
//     
//     void OnRankEntryClicked(Rank rank)
//     {
//         if (clientState.selectedRank == rank)
//         {
//             clientState.SetSelectedRank(null);
//         }
//         else
//         {
//             clientState.SetSelectedRank(rank);
//         }
//         ClientStateChanged();
//     }
//     
//     void OnClear()
//     {
//         clientState.ClearPendingCommits();
//         ClientStateChanged();
//     }
//
//     void OnAutoSetup()
//     {
//         clientState.ClearPendingCommits();
//         // Generate valid setup positions for each pawn
//         HashSet<Tile> usedTiles = new();
//         Rank[] sortedRanks = clientState.maxRanks.Keys.ToArray();
//         Array.Sort(sortedRanks, (rank1, rank2) => Rules.GetSetupZone(rank1) < Rules.GetSetupZone(rank2) ? 1 : -1);
//         foreach (Rank rank in sortedRanks)
//         {
//             for (int i = 0; i < clientState.maxRanks[rank]; i++)
//             {
//                 // Get available tiles for this rank
//                 List<Tile> availableTiles = bm.boardDef.GetEmptySetupTiles(clientState.team, rank, usedTiles);
//                 if (availableTiles.Count == 0)
//                 {
//                     Debug.LogError($"No available tiles for rank {rank}");
//                     continue;
//                 }
//                 // Pick a random tile from available tiles
//                 int randomIndex = Random.Range(0, availableTiles.Count);
//                 Tile selectedTile = availableTiles[randomIndex];
//                 clientState.SetPendingCommit(selectedTile.pos, rank);
//                 usedTiles.Add(selectedTile);
//             }
//         }
//         ClientStateChanged();
//     }
//     
//     void OnRefresh()
//     {
//         _ = StellarManager.UpdateState();
//     }
//
//     void OnSubmit()
//     {
//         Dictionary<Vector2Int, Rank> pendingCommits = new();
//         foreach (KeyValuePair<Vector2Int, Rank?> commitment in clientState.pendingCommits)
//         {
//             if (commitment.Value != null)
//             {
//                 pendingCommits[commitment.Key] = commitment.Value.Value;
//             }
//             else
//             {
//                 throw new NullReferenceException();
//             }
//         }
//         if (BoardManager.singlePlayer)
//         {
//             //FakeServer.ins.CommitSetupRequest(clientState.commitments);
//         }
//         else
//         {
//             _ = StellarManager.CommitSetupRequest(pendingCommits);
//         }
//     }
//
//     void ProveSetup()
//     {
//         if (clientState.committed && clientState.opponentCommitted && !clientState.submitted)
//         {
//             _ = StellarManager.ProveSetupRequest();
//         }
//     }
// }
//
// public abstract class MovementClientSubState { }
//
// public class SelectingPawnMovementClientSubState : MovementClientSubState
// {
//     public Vector2Int? selectedPos;
//     public uint? selectedPawnId;
//     
//     public SelectingPawnMovementClientSubState(uint? inSelectedPawnId = null, Vector2Int? inSelectedPos = null)
//     {
//         selectedPawnId = inSelectedPawnId;
//         selectedPos = inSelectedPos;
//         if (MovementClientState.autoSubmit)
//         {
//             SubmitMove();
//         }
//     }
//
//     public void SubmitMove()
//     {
//         if (!selectedPawnId.HasValue || !selectedPos.HasValue) return;
//         if (BoardManager.singlePlayer)
//         {
//             //FakeServer.ins.QueueMove(new QueuedMove { pawnId = selectedPawnId.Value, pos = selectedPos.Value });
//
//         }
//         //_ = StellarManagerTest.QueueMove(new QueuedMove { pawnId = selectedPawnId.Value, pos = selectedPos.Value });
//     }
// }
//
// public class SelectingPosMovementClientSubState : MovementClientSubState
// {
//     public uint selectedPawnId;
//     public HashSet<Vector2Int> highlightedTiles;
//
//     public SelectingPosMovementClientSubState(uint inPawnId, HashSet<Vector2Int> inHighlightedTiles)
//     {
//         selectedPawnId = inPawnId;
//         highlightedTiles = inHighlightedTiles;
//     }
// }
//
// public class WaitingUserHashMovementClientSubState : MovementClientSubState { }
//
// public class WaitingOpponentMoveMovementClientSubState : MovementClientSubState { }
//
// public class WaitingOpponentHashMovementClientSubState : MovementClientSubState { }
//
// public class ResolvingMovementClientSubState : MovementClientSubState { }
//
// public class GameOverMovementClientSubState : MovementClientSubState
// {
//     public uint endState;
//
//     public GameOverMovementClientSubState(uint inEndState)
//     {
//         endState = inEndState;
//     }
//
//     public string EndStateMessage()
//     {
//         switch (endState)
//         {
//             case 0:
//                 return "Game tied";
//             case 1:
//                 return "Red team won";
//             case 2:
//                 return "Blue team won";
//             case 3:
//                 return "Game in session";
//             case 4:
//                 return "Game ended inconclusively";
//             default:
//                 return "Game over";
//         }
//     }
// }
//
// public class MovementClientState
// {
//     public bool dirty = true;
//     public Team team;
//     public Contract.ResolveEvent[] myEvents;
//     public string myEventsHash;
//     public TurnMove myTurnMove;
//     public Contract.ResolveEvent[] otherEvents;
//     public string otherEventsHash;
//     public TurnMove otherTurnMove;
//     public int turn;
//     public static bool autoSubmit;
//
//     public MovementClientSubState subState;
//     readonly BoardDef boardDef;
//     readonly Dictionary<Vector2Int, Contract.Pawn> pawnPositions;
//
//     public MovementClientState(GameNetworkState networkState, BoardDef boardDef)
//     {
//         // this.boardDef = boardDef;
//         // Turn currentTurn = lobby.GetLatestTurn();
//         // dirty = true;
//         // team = myTeam;
//         // myEvents = isHost ? currentTurn.host_events : currentTurn.guest_events;
//         // myEventsHash = isHost ? currentTurn.host_events_hash : currentTurn.guest_events_hash;
//         // myTurnMove = isHost ? currentTurn.host_turn : currentTurn.guest_turn;
//         // otherEvents = isHost ? currentTurn.guest_events : currentTurn.host_events;
//         // otherEventsHash = isHost ? currentTurn.guest_events_hash : currentTurn.host_events_hash;
//         // otherTurnMove = isHost ? currentTurn.guest_turn : currentTurn.host_turn;
//         // turn = currentTurn.turn;
//         // // Build pawn position lookup
//         // pawnPositions = new Dictionary<Vector2Int, Contract.Pawn>();
//         // foreach (Contract.Pawn pawn in lobby.pawns)
//         // {
//         //     pawnPositions[pawn.pos.ToVector2Int()] = pawn;
//         // }
//         // // Initialize state based on current conditions
//         // if (lobby.game_end_state == 3)
//         // {
//         //     if (myTurnMove.initialized)
//         //     {
//         //         if (otherTurnMove.initialized)
//         //         {
//         //             if (!string.IsNullOrEmpty(myEventsHash))
//         //             {
//         //                 if (!string.IsNullOrEmpty(otherEventsHash))
//         //                 {
//         //                     Assert.IsTrue(myEvents == otherEvents);
//         //                     subState = new ResolvingMovementClientSubState();
//         //                 }
//         //                 else
//         //                 {
//         //                     subState = new WaitingOpponentHashMovementClientSubState();
//         //                 }
//         //             }
//         //             else
//         //             {
//         //                 subState = new WaitingUserHashMovementClientSubState();
//         //             }
//         //         }
//         //         else
//         //         {
//         //             subState = new WaitingOpponentMoveMovementClientSubState();
//         //         }
//         //     }
//         //     else
//         //     {
//         //         subState = new SelectingPawnMovementClientSubState();
//         //     }
//         // }
//         // else
//         // {
//         //     subState = new GameOverMovementClientSubState(lobby.game_end_state);
//         // }
//         
//     }
//
//     void SetSubState(MovementClientSubState newState)
//     {
//         subState = newState;
//         dirty = true;
//     }
//
//     void TransitionToSelectingPos(uint pawnId)
//     {
//         SetSubState(new SelectingPosMovementClientSubState(pawnId, GetMovableTilePositions(pawnId, boardDef, pawnPositions)));
//     }
//
//     void TransitionToSelectingPawn(uint? selectedPawnId = null, Vector2Int? selectedPos = null)
//     {
//         SetSubState(new SelectingPawnMovementClientSubState(selectedPawnId, selectedPos));
//     }
//
//     public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
//     {
//         switch (subState)
//         {
//             case SelectingPawnMovementClientSubState:
//                 if (pawnView && pawnView.team == team)
//                 {
//                     TransitionToSelectingPos(pawnView.pawnId);
//                 }
//                 else
//                 {
//                     TransitionToSelectingPawn();
//                 }
//                 break;
//
//             case SelectingPosMovementClientSubState selectingPosSubState:
//                 if (selectingPosSubState.highlightedTiles.Contains(clickedPos))
//                 {
//                     TransitionToSelectingPawn(selectingPosSubState.selectedPawnId, clickedPos);
//                 }
//                 else
//                 {
//                     if (pawnView && pawnView.team == team)
//                     {
//                         TransitionToSelectingPos(pawnView.pawnId);
//                     }
//                     else
//                     {
//                         TransitionToSelectingPawn();
//                     }
//                 }
//                 break;
//         }
//     }
//     
//     static HashSet<Vector2Int> GetMovableTilePositions(uint pawnId, BoardDef boardDef, Dictionary<Vector2Int, Contract.Pawn> pawnPositions)
//     {
//         Contract.Pawn pawn = pawnPositions.Values.First(p => p.pawn_id == pawnId);
//         HashSet<Vector2Int> movableTilePositions = new();
//         // PawnDef def = Globals.FakeHashToPawnDef(pawn.pawn_def_hash);
//         // if (!pawn.is_alive || def.movementRange == 0)
//         // {
//         //     return movableTilePositions;
//         // }
//         // Vector2Int pawnPos = pawn.pos.ToVector2Int();
//         // Vector2Int[] initialDirections = Shared.GetDirections(pawnPos, boardDef.isHex);
//         // for (int dirIndex = 0; dirIndex < initialDirections.Length; dirIndex++)
//         // {
//         //     Vector2Int currentPos = pawnPos;
//         //     int walkedTiles = 0;
//         //     while (walkedTiles < def.movementRange)
//         //     {
//         //         Vector2Int[] currentDirections = Shared.GetDirections(currentPos, boardDef.isHex);
//         //         currentPos += currentDirections[dirIndex];
//         //         Tile tile = boardDef.GetTileByPos(currentPos);
//         //         if (tile == null || !tile.isPassable) break;
//         //         if (pawnPositions.TryGetValue(currentPos, out Contract.Pawn value))
//         //         {
//         //             if (value.team == pawn.team) break;
//         //             movableTilePositions.Add(currentPos);
//         //             break;
//         //         }
//         //         movableTilePositions.Add(currentPos);
//         //         walkedTiles++;
//         //     }
//         // }
//         return movableTilePositions;
//     }
// }
//
// public class MovementPhase : IPhase
// {
//     BoardManager bm;
//     GuiMovement movementGui;
//     
//     public MovementClientState clientState;
//     
//     public MovementPhase(BoardManager inBm, GuiMovement inMovementGui, GameNetworkState networkState)
//     {
//         bm = inBm;
//         movementGui = inMovementGui;
//         bm.guiGame.SetCurrentElement(movementGui, networkState);
//     }
//
//     public void RefreshGui()
//     {
//         movementGui.Refresh(clientState);
//     }
//     
//     void ResetClientState(GameNetworkState networkState)
//     {
//         Debug.Log("ResetClientState");
//         clientState = new MovementClientState(networkState, bm.boardDef);
//     }
//
//     void ClientStateChanged()
//     {
//         bm.OnlyClientGameStateChanged();
//     }
//     
//     public void EnterState()
//     {
//         movementGui.OnSubmitMoveButton += SubmitMove;
//         movementGui.OnRefreshButton += RefreshState;
//         movementGui.OnAutoSubmitToggle += SetAutoSubmit;
//         movementGui.OnCheatButton += SetCheatMode;
//         movementGui.OnBadgeButton += SetBadgeVisibility;
//     }
//
//     public void ExitState()
//     {
//         movementGui.OnSubmitMoveButton -= SubmitMove;
//         movementGui.OnRefreshButton -= RefreshState;
//         movementGui.OnAutoSubmitToggle -= SetAutoSubmit;
//         movementGui.OnCheatButton -= SetCheatMode;
//         movementGui.OnBadgeButton -= SetBadgeVisibility;
//     }
//
//     public void Update() {}
//
//     public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
//     {
//         
//     }
//     
//     public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
//     {
//         if (bm.currentPhase != this) return;
//         clientState.OnClick(clickedPos, tileView, pawnView);
//         if (clientState.dirty)
//         {
//             ClientStateChanged();
//         }
//     }
//
//     public void OnNetworkGameStateChanged(GameNetworkState networkState)
//     {
//         // ResetClientState(lobby);
//         // Turn latestTurn = lobby.GetLatestTurn();
//         // // TODO: make this stateful
//         // if (latestTurn.host_turn.initialized && latestTurn.guest_turn.initialized)
//         // {
//         //     if (bm.isHost)
//         //     {
//         //         if (string.IsNullOrEmpty(latestTurn.host_events_hash))
//         //         {
//         //             Debug.Log("Submitting move hash for host because both players have initialized their turns");
//         //             if (TestBoardManager.singlePlayer)
//         //             {
//         //                 FakeServer.ins.SubmitMoveHash();
//         //             }
//         //             else
//         //             {
//         //                 _ = StellarManagerTest.SubmitMoveHash();
//         //             }
//         //             
//         //         }
//         //     }
//         //     else
//         //     {
//         //         if (string.IsNullOrEmpty(latestTurn.guest_events_hash))
//         //         {
//         //             Debug.Log("Submitting move hash for guest because both players have initialized their turns");
//         //             if (TestBoardManager.singlePlayer)
//         //             {
//         //                 FakeServer.ins.SubmitMoveHash();
//         //             }
//         //             else
//         //             {
//         //                 _ = StellarManagerTest.SubmitMoveHash();
//         //             }
//         //         }
//         //     }
//         // }
//     }
//
//     void SubmitMove()
//     {
//         if (clientState.subState is SelectingPawnMovementClientSubState selectingState)
//         {
//             selectingState.SubmitMove();
//         }
//     }
//
//     void RefreshState()
//     {
//         _ = StellarManager.UpdateState();
//     }
//
//     void SetAutoSubmit(bool autoSubmit)
//     {
//         MovementClientState.autoSubmit = autoSubmit;
//     }
//
//     void SetCheatMode()
//     {
//         int cheatMode = PlayerPrefs.GetInt("CHEATMODE");
//         if (cheatMode == 0)
//         {
//             PlayerPrefs.SetInt("CHEATMODE", 1);
//         }
//         else
//         {
//             PlayerPrefs.SetInt("CHEATMODE", 0);
//         }
//         ClientStateChanged();
//     }
//
//     void SetBadgeVisibility()
//     {
//         int displayBadge = PlayerPrefs.GetInt("DISPLAYBADGE");
//         if (displayBadge == 0)
//         {
//             PlayerPrefs.SetInt("DISPLAYBADGE", 1);
//         }
//         else
//         {
//             PlayerPrefs.SetInt("DISPLAYBADGE", 0);
//         }
//         ClientStateChanged();
//     }
// }
//
// public class QueuedMove
// {
//     public uint pawnId;
//     public Vector2Int pos;
// }
