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
        PhaseStateChanged(new PhaseChangeSet(new NetStateUpdated(currentPhase)));
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
            tileView.PhaseStateChanged(changes);
        }
        foreach (PawnView pawnView in pawnViews.Values)
        {
            pawnView.PhaseStateChanged(changes);
        }
        // update gui
        guiGame.PhaseStateChanged(changes);
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
    public GameNetworkState cachedNetState;
    public GameNetworkState oldNetState;
    public Vector2Int hoveredPos;
    
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
    protected abstract void OnMouseInput(Vector2Int hoveredPos, bool clicked);
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
        selectedRank = null;
        setupInputTool = SetupInputTool.NONE;
        guiSetup.OnClearButton = OnClear;
        guiSetup.OnAutoSetupButton = OnAutoSetup;
        guiSetup.OnRefreshButton = OnRefresh;
        guiSetup.OnSubmitButton = OnSubmit;
        guiSetup.OnEntryClicked = OnEntryClicked;
        if (!cachedNetState.IsMySubphase())
        {
            LoadCachedData();
        }
    }
    
    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        if (!cachedNetState.IsMySubphase())
        {
            LoadCachedData();
        }
    }

    void LoadCachedData()
    {
        pendingCommits = new();
        foreach (PawnState pawn in cachedNetState.gameState.pawns.Where(p => p.GetTeam() == cachedNetState.userTeam))
        {
            byte[] hiddenRankHash = pawn.hidden_rank_hash;
            if (CacheManager.GetHiddenRank(hiddenRankHash) is HiddenRank hiddenRank)
            {
                pendingCommits[pawn.pawn_id] = hiddenRank.rank;
            }
            else
            {
                pendingCommits[pawn.pawn_id] = null;
            }
        }
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
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        foreach ((PawnId pawnId, Rank? _) in pendingCommits)
        {
            pendingCommits[pawnId] = null;
        }
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(new SetupRankCommitted(oldPendingCommits, this)));
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
            PawnState pawn = cachedNetState.GetPawnFromPos(pos);
            pendingCommits[pawn.pawn_id] = rank;
        }
        
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
            throw new InvalidOperationException("too many pawns of this rank committed");
        }
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        PawnState pawn = cachedNetState.GetPawnFromPos(pos);
        pendingCommits[pawn.pawn_id] = selectedRank;
        Debug.Log($"commit position {pawn.pawn_id} {pos} {selectedRank}");
        return new(oldPendingCommits, this);
    }

    SetupRankCommitted UncommitPosition(Vector2Int pos)
    {
        Dictionary<PawnId, Rank?> oldPendingCommits = new(pendingCommits);
        PawnState pawn = cachedNetState.GetPawnFromPos(pos);
        pendingCommits[pawn.pawn_id] = null;
        Debug.Log($"uncommit position regardless of selected rank {pawn.pawn_id} {pos} {selectedRank}");
        return new(oldPendingCommits, this);
    }
    
    protected override void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState)
    {
        //throw new NotImplementedException();
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
                    SetupRankCommitted rankCommittedOperation = CommitPosition(hoveredPos);
                    operations.Add(rankCommittedOperation);
                    break;
                case SetupInputTool.REMOVE:
                    SetupRankCommitted rankUncommittedOperation = UncommitPosition(hoveredPos);
                    operations.Add(rankUncommittedOperation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        setupInputTool = GetNextTool();
        operations.Add(new SetupHoverChanged(oldHoveredPos, this));
        OnPhaseStateChanged?.Invoke(new PhaseChangeSet(operations));
    }

    SetupInputTool GetNextTool()
    {
        SetupInputTool tool = SetupInputTool.NONE;
        if (!cachedNetState.IsMySubphase())
        {
            return SetupInputTool.NONE;
        }
        // if hovered over a already committed pawn
        if (cachedNetState.GetPawnFromPosChecked(hoveredPos) is PawnState pawn && pendingCommits.ContainsKey(pawn.pawn_id) && pendingCommits[pawn.pawn_id] != null)
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
}

public class MoveCommitPhase: PhaseBase
{
    public Vector2Int? selectedPos;
    public Vector2Int? targetPos;
    public MoveInputTool moveInputTool;
    public HashSet<Vector2Int> targetablePositions;
    
    public MoveCommitPhase(BoardManager bm, GuiMovement guiMovement, GameNetworkState inNetworkState, TestClickInputManager clickInputManager): base(bm, inNetworkState, clickInputManager)
    {
        tileViews = bm.tileViews;
        selectedPos = null;
        targetPos = null;
        moveInputTool = MoveInputTool.NONE;
        targetablePositions = new();
        guiMovement.OnSubmitMoveButton = OnSubmit;
        guiMovement.OnRefreshButton = OnRefresh;
        if (!cachedNetState.IsMySubphase())
        {
            LoadCachedData();
        }
    }

    public override void UpdateNetworkState(GameNetworkState netState)
    {
        base.UpdateNetworkState(netState);
        if (!cachedNetState.IsMySubphase())
        {
            LoadCachedData();
        }
    }

    void LoadCachedData()
    {
        byte[] moveHash = cachedNetState.GetUserMove().move_hash;
        if (CacheManager.GetHiddenMove(moveHash) is HiddenMove hiddenMove)
        {
            selectedPos = hiddenMove.start_pos;
            targetPos = hiddenMove.target_pos;
        }
    }
    
    void OnSubmit()
    {
        if (!cachedNetState.IsMySubphase())
        {
            throw new InvalidOperationException("not my turn to act");
        }
        if (selectedPos is not Vector2Int selectedPosition)
        {
            throw new InvalidOperationException("selectedPos must exist");
        }
        if (cachedNetState.GetAlivePawnFromPosChecked(selectedPosition) is not PawnState selectedPawn)
        {
            throw new InvalidOperationException("selectedPawn must exist and be alive");
        }
        if (targetPos is not Vector2Int targetPosition)
        {
            throw new InvalidOperationException("TargetPos must exist");
        }
        if (selectedPawn.GetTeam() != cachedNetState.userTeam)
        {
            throw new InvalidOperationException("selectedPawn team is invalid");
        }
        if (!cachedNetState.GetValidMoveTargetList(selectedPawn.pawn_id).Contains(targetPosition))
        {
            throw new InvalidOperationException("targetpos is out of range");
        }
        HiddenMove hiddenMove = new()
        {
            pawn_id = selectedPawn.pawn_id,
            salt = Globals.RandomSalt(),
            start_pos = selectedPawn.pos,
            target_pos = targetPosition,
        };
        CacheManager.StoreHiddenMove(hiddenMove, cachedNetState.address, cachedNetState.lobbyInfo.index);
        _ = StellarManager.CommitMoveRequest(cachedNetState.lobbyInfo.index, SCUtility.Get16ByteHash(hiddenMove));
    }

    void OnRefresh()
    {
        _ = StellarManager.UpdateState();
    }
    
    MovePosSelected SelectPosition(Vector2Int? pos)
    {
        Vector2Int? oldPos = selectedPos;
        selectedPos = pos;
        if (selectedPos is Vector2Int p && cachedNetState.GetAlivePawnFromPosChecked(p) is PawnState selectedPawn)
        {
            targetablePositions = cachedNetState.GetValidMoveTargetList(selectedPawn.pawn_id);
        }
        else
        {
            targetablePositions.Clear();
        }
        return new(oldPos, this);
    }

    MoveTargetSelected TargetPosition(Vector2Int? target)
    {
        Vector2Int? oldTarget = targetPos;
        targetPos = target;
        targetablePositions.Clear();
        return new(oldTarget, this);
    }
    
    protected override void AddPhaseSpecificOperations(List<GameOperation> operations, GameNetworkState oldNetState, GameNetworkState netState)
    {
        //throw new NotImplementedException();
    }

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
    {
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
                    operations.Add(posSelected);
                    break;
                case MoveInputTool.TARGET:
                    MoveTargetSelected targetSelected = TargetPosition(hoveredPos);
                    operations.Add(targetSelected);
                    break;
                case MoveInputTool.CLEAR_SELECT:
                    MovePosSelected posUnselected = SelectPosition(null);
                    operations.Add(posUnselected);
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
        if (!cachedNetState.IsMySubphase())
        {
            return MoveInputTool.NONE;
        }
        if (cachedNetState.GetTileChecked(hoveredPos) == null)
        {
            return MoveInputTool.NONE;
        }
        // a pawn is already selected and target hasn't been selected yet
        if (selectedPos is Vector2Int selectedPos2 && cachedNetState.GetAlivePawnFromPosChecked(selectedPos2) is PawnState selectedPawn)
        {
            // if hovering over selected pawn we do nothing
            if (selectedPawn.pos == pos)
            {
                return MoveInputTool.NONE;
            }
            // check if hovering over another selectable pawn
            if (cachedNetState.GetAlivePawnFromPosChecked(hoveredPos) is PawnState hoveredPawn 
                && cachedNetState.CanUserMovePawn(hoveredPawn.pawn_id))
            {
                return MoveInputTool.SELECT;
            }
            // check if hovering over a valid target
            if (targetablePositions.Contains(hoveredPos))
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
            if (cachedNetState.GetAlivePawnFromPosChecked(hoveredPos) is PawnState hoveredPawn 
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

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
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

    protected override void OnMouseInput(Vector2Int inHoveredPos, bool clicked)
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

public record NetStateUpdated(PhaseBase phase) : GameOperation;


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
