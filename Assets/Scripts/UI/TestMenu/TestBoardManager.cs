using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;

public class TestBoardManager : MonoBehaviour
{
    public bool initialized;
    // never mutate these externally
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public TestClickInputManager clickInputManager;
    public Vortex vortex;
    // UI stuff generally gets done in the phase
    public GuiTestGame guiTestGame;
    // generally doesn't change after lobby is set in StartGame
    public BoardDef boardDef;
    public Contract.LobbyParameters parameters;
    public Team userTeam;
    public bool isHost;
    public string lobbyId;
    // internal game state. call OnStateChanged when updating these. only StartGame can make new views
    public Dictionary<Vector2Int, TestTileView> tileViews = new();
    public List<TestPawnView> pawnViews = new();
    // last known lobby
    public Lobby cachedLobby;
    
    public ITestPhase currentPhase;
    
    //public event Action<Lobby> OnPhaseChanged;
    public event Action<Lobby, ITestPhase> OnClientGameStateChanged;
    
    void Start()
    {
        clickInputManager.OnClick += OnClick;
        StellarManagerTest.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }

    bool firstTime;
    
    public void StartBoardManager(bool networkUpdated)
    {
        if (!networkUpdated)
        {
            throw new NotImplementedException();
        }
        clickInputManager.Initialize(this);
        Assert.IsTrue(StellarManagerTest.currentUser.HasValue);
        Assert.IsTrue(StellarManagerTest.currentLobby.HasValue);
        Lobby lobby = StellarManagerTest.currentLobby.Value;
        Initialize(StellarManagerTest.currentUser.Value, lobby);
        initialized = true;
        firstTime = true;
        cachedLobby = lobby;
        OnNetworkStateUpdated(); //only invoke this directly once on start
    }

    void OnNetworkStateUpdated()
    {
        Debug.Log("TestBoardManager::OnNetworkStateUpdated");
        if (!initialized)
        {
            return;
        }
        Assert.IsTrue(StellarManagerTest.currentLobby.HasValue);
        Lobby lobby = StellarManagerTest.currentLobby.Value; // this should be the only time we ever reach into SMT for lobby
        if (firstTime || lobby.phase != cachedLobby.phase || cachedLobby.turns.Length != lobby.turns.Length)
        {
            firstTime = false;
            switch (lobby.phase)
            {
                case 1:
                    Debug.Log("SetPhase setup");
                    SetPhase(new SetupTestPhase(this, guiTestGame.setup, lobby));
                    break;
                case 2:
                    Debug.Log("SetPhase movement");
                    SetPhase(new MovementTestPhase(this, guiTestGame.movement, lobby));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        cachedLobby = lobby;
        currentPhase?.OnNetworkGameStateChanged(lobby);
    }

    public void OnlyClientGameStateChanged()
    {
        Debug.Log("TestBoardManager::OnlyClientGameStateChanged");
        OnClientGameStateChanged?.Invoke(cachedLobby, currentPhase);
    }
    
    void Initialize(User user, Lobby lobby)
    {
        BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
        boardDef = boardDefs.FirstOrDefault(def => def.name == lobby.parameters.board_def_name);
        if (!boardDef)
        {
            throw new NullReferenceException();
        }
        if (user.index == lobby.host_address)
        {
            userTeam = (Team)lobby.host_state.team;
        }
        else
        {
            userTeam = (Team)lobby.guest_state.team;
        }
        // Clear existing tileviews and replace
        foreach (TestTileView tile in tileViews.Values)
        {
            Destroy(tile.gameObject);
        }
        tileViews.Clear();
        grid.SetBoard(new SBoardDef(boardDef));
        foreach (Tile tile in boardDef.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TestTileView tileView = tileObject.GetComponent<TestTileView>();
            tileView.Initialize(tile, this);
            tileViews.Add(tile.pos, tileView);
        }
        // Clear any existing pawnviews and replace
        foreach (TestPawnView pawnView in pawnViews)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();
        foreach (Contract.Pawn p in lobby.pawns)
        {
            GameObject pawnObject = Instantiate(pawnPrefab, transform);
            TestPawnView pawnView = pawnObject.GetComponent<TestPawnView>();
            pawnView.Initialize(p, this);
            pawnViews.Add(pawnView);
        }
        lobbyId = lobby.index;
        isHost = lobby.host_address == user.index;
        parameters = lobby.parameters;
    }
    
    public List<TestPawnView> GetMyPawnViews()
    {
        return pawnViews.Where(pv => pv.team == userTeam).ToList();
    }
    
    // public Pawn GetPawnAtPos(Vector2Int pos)
    // {
    //     TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
    //     return pawnView?.pawn;
    // }
    //
    // public TestPawnView GetPawnViewAtPos(Vector2Int pos)
    // {
    //     TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
    //     return pawnView;
    // }

    public Tile GetTileAtPos(Vector2Int pos)
    {
        TestTileView tileView = tileViews.GetValueOrDefault(pos);
        return tileView?.tile;
    }

    public TestTileView GetTileViewAtPos(Vector2Int pos)
    {
        TestTileView tileView = tileViews.GetValueOrDefault(pos);
        return tileView;
    }

    public TestPawnView GetPawnViewAtPos(Vector2Int pos)
    {
        return pawnViews.FirstOrDefault(pv => pv.displayedPos == pos);
    }
    
    // public List<Pawn> GetMyPawns()
    // {
    //     return pawnViews
    //         .Where(pv => pv.pawn.team == userTeam)
    //         .Select(pv => pv.pawn)
    //         .ToList();
    // }
    //
    
    
    void SetPhase(ITestPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
    }
    
    void OnClick(Vector2Int pos)
    {
        TestTileView tileView = GetTileViewAtPos(pos);
        TestPawnView pawnView = GetPawnViewAtPos(pos);
        currentPhase?.OnClick(pos, tileView, pawnView);
    }

    void OnHover(Vector2Int pos)
    {
        TestTileView tileView = GetTileViewAtPos(pos);
        TestPawnView pawnView = GetPawnViewAtPos(pos);
        currentPhase?.OnHover();
    }

    public Contract.Pawn? GetCachedPawnState(Guid pawnId)
    {
        foreach (Contract.Pawn p in cachedLobby.pawns)
        {
            if (p.pawn_id == pawnId.ToString())
            {
                return p;
            }
        }
        return null;
    }

    public Contract.Pawn GetCachedPawnStateUnchecked(Guid pawnId)
    {
        foreach (Contract.Pawn p in cachedLobby.pawns)
        {
            if (p.pawn_id == pawnId.ToString())
            {
                return p;
            }
        }
        throw new ArgumentOutOfRangeException();
    }
    
    public Contract.Pawn? GetCachedPawnState(Vector2Int pos)
    {
        foreach (Contract.Pawn p in cachedLobby.pawns)
        {
            if (p.pos.ToVector2Int() == pos)
            {
                return p;
            }
        }
        return null;
    }
    
    public Contract.Pawn GetCachedPawnStateUnchecked(Vector2Int pos)
    {
        foreach (Contract.Pawn p in cachedLobby.pawns)
        {
            if (p.pos.ToVector2Int() == pos)
            {
                return p;
            }
        }
        throw new ArgumentOutOfRangeException();
    }
    
}

public interface ITestPhase
{
    public void EnterState();
    public void ExitState();
    public void Update();
    public void OnHover();
    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView);

    public void OnNetworkGameStateChanged(Lobby lobby);
    
}

public class SetupClientState
{
    public bool dirty = true;
    public Dictionary<string, PawnCommitment> commitments;
    public Rank? selectedRank;
    public bool committed;
    public Team team;

    public void SetSelectedRank(Rank? rank)
    {
        selectedRank = rank;
        dirty = true;
    }

    public bool SetCommitment(Rank rank, Vector2Int pos)
    {
        PawnCommitment? maybeCommitment = GetUnusedCommitment(rank);
        if (!maybeCommitment.HasValue)
        {
            return false;
        }
        ChangeCommitment(maybeCommitment.Value.pawn_id, pos);
        return true;
    }
    
    public bool SetCommitment(Vector2Int pos)
    {
        if (!selectedRank.HasValue)
        {
            throw new Exception();
        }
        if (pos == Globals.Purgatory)
        {
            throw new Exception();
        }
        PawnCommitment? maybeCommitment = GetUnusedCommitment(selectedRank.Value);
        if (!maybeCommitment.HasValue)
        {
            return false;
        }
        ChangeCommitment(maybeCommitment.Value.pawn_id, pos);
        return true;
    }

    public void ClearCommitment(Guid guid)
    {
        ChangeCommitment(guid.ToString(), Globals.Purgatory);
    }
    
    public void ClearAllCommitments()
    {
        List<string> keys = commitments.Keys.ToList();
        foreach (string key in keys)
        {
            ChangeCommitment(key, Globals.Purgatory);
        }
    }
    
    void ChangeCommitment(string id, Vector2Int pos)
    {
        PawnCommitment commitment = commitments[id];
        commitment.starting_pos = new Pos(pos);
        commitments[id] = commitment;
        dirty = true;
    }

    PawnCommitment? GetUnusedCommitment(Rank rank)
    {
        foreach (PawnCommitment commitment in commitments.Values
                     .Where(commitment => commitment.starting_pos.ToVector2Int() == Globals.Purgatory)
                     .Where(commitment => Globals.FakeHashToPawnDef(commitment.pawn_def_hash).rank == rank))
        {
            return commitment;
        }
        return null;
    }
}

public class SetupTestPhase : ITestPhase
{
    TestBoardManager bm;
    GuiTestSetup setupGui;

    public SetupClientState clientState;
    
    public SetupTestPhase(TestBoardManager inBm, GuiTestSetup inSetupGui, Lobby lobby)
    {
        bm = inBm;
        setupGui = inSetupGui;
        ResetClientState(lobby);
        // TODO: figure out a better way to tell gui to do stuff
        bm.guiTestGame.SetCurrentElement(setupGui, lobby);
    }

    void ResetClientState(Lobby lobby)
    {
        Debug.Log("SetupTestPhase.ResetClientState");
        UserState userState = lobby.GetUserStateByTeam(bm.userTeam);
        SetupClientState newSetupClientState = new SetupClientState()
        {
            selectedRank = null,
            commitments = new Dictionary<string, PawnCommitment>(),
            committed = userState.committed,
            team = (Team)userState.team,
        };
        // we can assume every max in maxRanks sums to commitIndex - 1;
        int commitIndex = 0;
        foreach (MaxPawns maxRanks in lobby.parameters.max_pawns)
        {
            for (int i = 0; i < maxRanks.max; i++)
            {
                string pawnDefHash = userState.committed ? userState.setup_commitments[commitIndex].pawn_def_hash : Globals.PawnDefToFakeHash(Globals.RankToPawnDef((Rank)maxRanks.rank));
                PawnCommitment commitment = new PawnCommitment()
                {
                    pawn_def_hash = pawnDefHash, // this is the only original data we fill in
                    pawn_id = userState.setup_commitments[commitIndex].pawn_id,
                    starting_pos = userState.setup_commitments[commitIndex].starting_pos,
                };
                newSetupClientState.commitments[commitment.pawn_id] = commitment;
                commitIndex += 1;
            }
        }
        clientState = newSetupClientState;
    }
    
    public void EnterState()
    {
        setupGui.OnClearButton += OnClear;
        setupGui.OnAutoSetupButton += OnAutoSetup;
        setupGui.OnRefreshButton += OnRefresh;
        setupGui.OnSubmitButton += OnSubmit;
        setupGui.OnRankEntryClicked += OnRankEntryClicked;

    }
    
    public void ExitState()
    {
        setupGui.OnClearButton -= OnClear;
        setupGui.OnAutoSetupButton -= OnAutoSetup;
        setupGui.OnRefreshButton -= OnRefresh;
        setupGui.OnSubmitButton -= OnSubmit;
    }

    public void Update()
    {
        
    }

    public void OnHover()
    {
        
    }

    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        Debug.Log("OnClick from setup Phase");
        if (tileView == null) return;
        if (clickedPos == Globals.Purgatory) return;
        // If there's a pawn at the clicked position, remove it
        if (pawnView)
        {
            clientState.ClearCommitment(pawnView.pawnId);
        }
        // If no pawn at position and we have a selected rank, try to place a pawn
        else if (clientState.selectedRank.HasValue)
        {
            // if tile is your setup tile
            if (tileView.tile.IsTileSetupAllowed(clientState.team))
            {
                bool success = clientState.SetCommitment(clickedPos);
                if (!success)
                {
                    Debug.LogWarning("Failed to find valid pawn of this rank");
                }
            }
            else
            {
                Debug.LogWarning("tile is not allowed");
            }
        }
        else
        {
            Debug.LogWarning("No rank selected");
            // do nothing
        }
        ClientStateChanged();
    }

    public void OnNetworkGameStateChanged(Lobby lobby)
    {
        ResetClientState(lobby);
        ClientStateChanged();
    }

    void ClientStateChanged()
    {
        Debug.Log("SetupClientState.ClientStateChanged");
        setupGui.Refresh(clientState);
        bm.OnlyClientGameStateChanged();
    }
    
    void OnRankEntryClicked(Rank rank)
    {
        if (clientState.selectedRank == rank)
        {
            clientState.SetSelectedRank(null);
        }
        else
        {
            clientState.SetSelectedRank(rank);
        }
        ClientStateChanged();
    }
    
    void OnClear()
    {
        clientState.ClearAllCommitments();
        ClientStateChanged();
    }

    void OnAutoSetup()
    {
        clientState.ClearAllCommitments();
        // Generate valid setup positions for each pawn
        HashSet<Tile> usedTiles = new();
        foreach (MaxPawns maxPawns in bm.parameters.max_pawns)
        {
            for (int i = 0; i < maxPawns.max; i++)
            {
                // Get available tiles for this rank
                List<Tile> availableTiles = bm.boardDef.GetEmptySetupTiles(bm.userTeam, (Rank)maxPawns.rank, usedTiles);
                if (availableTiles.Count == 0)
                {
                    Debug.LogError($"No available tiles for rank {maxPawns.rank}");
                    continue;
                }
                // Pick a random tile from available tiles
                int randomIndex = UnityEngine.Random.Range(0, availableTiles.Count);
                Tile selectedTile = availableTiles[randomIndex];
                usedTiles.Add(selectedTile);
                // Find a pawn of this rank in purgatory
                
                bool success = clientState.SetCommitment((Rank)maxPawns.rank, selectedTile.pos);
                if (!success)
                {
                    Debug.LogError($"No available pawns of rank {maxPawns.rank} to place");
                }
            }
        }
        ClientStateChanged();
    }
    
    void OnRefresh()
    {
        _ = StellarManagerTest.UpdateState();
    }

    void OnSubmit()
    {
        foreach (var commitment in clientState.commitments.Values)
        {
            if (commitment.starting_pos.ToVector2Int() == Globals.Purgatory)
            {
                Debug.LogWarning("Submit rejected because all pawns must have commitments");
                return;
            }
        }
        _ = StellarManagerTest.CommitSetupRequest(clientState.commitments);
    }
}

public enum MovementClientStateStatus
{
    AWAITING_SELECTION,
    AWAITING_POSITION,
    AWAITING_USER_HASH,
    AWAITING_OPPONENT_MOVE,
    AWAITING_OPPONENT_HASH,
    RESOLVING,
}

public class MovementClientSubState
{
    
}

public class SelectingPawnMovementClientSubState : MovementClientSubState
{

}

public class SelectingPosMovementClientSubState : MovementClientSubState
{
    [CanBeNull] string selectedPawnId;
    public HashSet<TestTileView> highlightedTiles;
    [CanBeNull] Vector2Int selectedPos;
}

public class WaitingUserHashMovementClientSubState : MovementClientSubState
{
    
}

public class WaitingOpponentHashMovementClientSubState : MovementClientSubState
{
    
}

public class ResolvingMovementClientSubState : MovementClientSubState
{
    
}

public class MovementClientState
{
    public bool dirty = true;
    // filled by client
    public TestPawnView selectedPawnView;
    public QueuedMove queuedMove;
    public HashSet<TestTileView> highlightedTiles;
    // filled by server
    public Team team;
    public Contract.ResolveEvent[] myEvents;
    public string myEventsHash;
    public TurnMove myTurnMove;
    public Contract.ResolveEvent[] otherEvents;
    public string otherEventsHash;
    public TurnMove otherTurnMove;
    public int turn;
    public static bool autoSubmit;

    public void SetSelectedPawnView(TestPawnView inSelectedPawnView, HashSet<TestTileView> inHighlightedTiles)
    {
        selectedPawnView = inSelectedPawnView;
        highlightedTiles = inHighlightedTiles ?? new();
        dirty = true;
    }

    public MovementClientState(Lobby lobby, TestBoardManager bm)
    {
        bool isHost = bm.isHost;
        Team myTeam = bm.userTeam;
        Turn currentTurn = lobby.GetLatestTurn();
        dirty = true;
        selectedPawnView = null;
        queuedMove = null;
        highlightedTiles = new();
        // fill in network state stuff
        team = myTeam;
        myEvents = isHost ? currentTurn.host_events : currentTurn.guest_events;
        myEventsHash = isHost ? currentTurn.host_events_hash : currentTurn.guest_events_hash;
        myTurnMove = isHost ? currentTurn.host_turn : currentTurn.guest_turn;
        otherEvents = isHost ? currentTurn.guest_events : currentTurn.host_events;
        otherEventsHash = isHost ? currentTurn.guest_events_hash : currentTurn.host_events_hash;
        otherTurnMove = isHost ? currentTurn.guest_turn : currentTurn.host_turn;
        turn = currentTurn.turn;
    }

    public MovementClientSubState subState;
    void SetSubState(MovementClientSubState newState)
    {
        subState = newState;
    }
    
    
    public MovementClientStateStatus GetMovementPhase()
    {
        MovementClientStateStatus movementPhase = MovementClientStateStatus.AWAITING_SELECTION;
        if (myTurnMove.initialized)
        {
            // already submitted
            if (otherTurnMove.initialized)
            {
                // both players submitted moves
                if (!string.IsNullOrEmpty(myEventsHash))
                {
                    if (!string.IsNullOrEmpty(otherEventsHash))
                    {
                        movementPhase = MovementClientStateStatus.RESOLVING;
                        throw new Exception("This state shouldn't ever happen");
                    }
                    else
                    {
                        movementPhase = MovementClientStateStatus.AWAITING_OPPONENT_HASH;
                    }
                }
                else
                {
                    movementPhase = MovementClientStateStatus.AWAITING_USER_HASH;
                }
            }
            else
            {
                // waiting for other player
                movementPhase = MovementClientStateStatus.AWAITING_OPPONENT_MOVE;
            }
        }
        else
        {
            if (selectedPawnView)
            {
                // pawn is selected so highlighted should also be filled
                movementPhase = MovementClientStateStatus.AWAITING_POSITION;
            }
            else
            {
                movementPhase = MovementClientStateStatus.AWAITING_SELECTION;
            }
        }
        Debug.Log("Returned " + movementPhase);
        return movementPhase;
    }

    public void TrySelectPawn(TestPawnView pawnView, TestBoardManager bm)
    {
        selectedPawnView = null;
        highlightedTiles = new();
        queuedMove = null;
        Assert.IsTrue(GetMovementPhase() == MovementClientStateStatus.AWAITING_SELECTION);
        Contract.Pawn pawn = bm.GetCachedPawnStateUnchecked(pawnView.pawnId);
        bool selectionIsValid = (Team)pawn.team == team;
        if (selectionIsValid)
        {
            selectedPawnView = pawnView;
            highlightedTiles = GetMovableTileViews(pawn, bm);
            Assert.IsTrue(GetMovementPhase() == MovementClientStateStatus.AWAITING_POSITION);
        }
    }
    
    public void TryQueueMove(Vector2Int pos)
    {
        Assert.IsTrue(GetMovementPhase() == MovementClientStateStatus.AWAITING_POSITION);
        bool isPosValid = highlightedTiles.Any(tile => tile.tile.pos == pos);
        if (isPosValid)
        {
            queuedMove = new QueuedMove()
            {
                pawnId = selectedPawnView.pawnId,
                pos = pos,
            };
            selectedPawnView = null;
            highlightedTiles.Clear();
            dirty = true;
            Assert.IsTrue(GetMovementPhase() == MovementClientStateStatus.AWAITING_SELECTION);
        }
        else
        {
            queuedMove = null;
            selectedPawnView = null;
            highlightedTiles = new();
            dirty = true;
            Assert.IsTrue(GetMovementPhase() == MovementClientStateStatus.AWAITING_SELECTION);
        }
    }

    public void ClearQueueMove()
    {
        queuedMove = null;
        selectedPawnView = null;
        highlightedTiles = new();
        dirty = true;
    }

    public void SetAutoSubmit(bool inAutoSubmit)
    {
        MovementClientState.autoSubmit = inAutoSubmit;
    }
    
    HashSet<TestTileView> GetMovableTileViews(Contract.Pawn pawn, TestBoardManager bm)
    {
        // TODO: remove this jank
        BoardDef boardDef = bm.boardDef;
        HashSet<TestTileView> movableTileViews = new();
        PawnDef def = Globals.FakeHashToPawnDef(pawn.pawn_def_hash);
        if (!pawn.is_alive)
        {
            return movableTileViews;
        }
        if (def.movementRange == 0)
        {
            return movableTileViews;
        }
        Vector2Int pawnPos = pawn.pos.ToVector2Int();
        Vector2Int[] initialDirections = Shared.GetDirections(pawnPos, boardDef.isHex);
        for (int dirIndex = 0; dirIndex < initialDirections.Length; dirIndex++)
        {
            Vector2Int currentPos = pawnPos;
            int walkedTiles = 0;
            while (walkedTiles < def.movementRange)
            {
                Vector2Int[] currentDirections = Shared.GetDirections(currentPos, boardDef.isHex);
                currentPos += currentDirections[dirIndex];
                TestTileView tileView = bm.GetTileViewAtPos(currentPos);
                if (!tileView) break;
                if (!tileView.tile.isPassable) break;
                Contract.Pawn? maybePawnOnPos = bm.GetCachedPawnState(currentPos);
                if (maybePawnOnPos.HasValue)
                {
                    if (maybePawnOnPos.Value.team == pawn.team) break;
                    movableTileViews.Add(tileView);
                    break;
                }
                movableTileViews.Add(tileView);
                walkedTiles++;
            }
        }
        return movableTileViews;
    }
}

public class MovementTestPhase : ITestPhase
{
    TestBoardManager bm;
    // public TestPawnView selectedPawnView;
    // public QueuedMove queuedMove;
    // public HashSet<TestTileView> highlightedTiles;
    
    GuiTestMovement movementGui;
    
    // public Turn cachedTurn;
    // public TurnMove committedMove;
    public MovementClientState clientState;
    
    
    public MovementTestPhase(TestBoardManager inBm, GuiTestMovement inMovementGui, Lobby lobby)
    {
        bm = inBm;
        movementGui = inMovementGui;
        ResetClientState(lobby);
        // TODO: figure out a better way to tell gui to do stuff
        bm.guiTestGame.SetCurrentElement(movementGui, lobby);
    }

    void ResetClientState(Lobby lobby)
    {
        Debug.Log("ResetClientState");
        MovementClientState newClientState = new MovementClientState(lobby, bm);
        clientState = newClientState;
    }

    void ClientStateChanged()
    {
        movementGui.Refresh(clientState);
        bm.OnlyClientGameStateChanged();
    }
    
    public void EnterState()
    {
        movementGui.OnSubmitMoveButton += SubmitMove;
        movementGui.OnRefreshButton += RefreshState;
        movementGui.OnAutoSubmitToggle += SetAutoSubmit;
    }

    public void ExitState()
    {
        movementGui.OnSubmitMoveButton -= SubmitMove;
        movementGui.OnRefreshButton -= RefreshState;
    }

    public void Update() {}
    
    public void OnHover() {}
    
    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        if (bm.currentPhase != this) return;
        MovementClientStateStatus status = clientState.GetMovementPhase();
        switch (status)
        {
            case MovementClientStateStatus.AWAITING_SELECTION:
                clientState.TrySelectPawn(pawnView, bm);
                ClientStateChanged();
                break;
            case MovementClientStateStatus.AWAITING_POSITION:
                clientState.TryQueueMove(tileView.tile.pos);
                ClientStateChanged();
                break;
            case MovementClientStateStatus.AWAITING_USER_HASH:
                // do nothing
                break;
            case MovementClientStateStatus.AWAITING_OPPONENT_MOVE:
                // do nothing
                break;
            case MovementClientStateStatus.AWAITING_OPPONENT_HASH:
                // do nothing
                break;
            case MovementClientStateStatus.RESOLVING:
                // do nothing
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
    }

    public void OnNetworkGameStateChanged(Lobby lobby)
    {
        ResetClientState(lobby);
        ClientStateChanged();
        Turn latestTurn = lobby.GetLatestTurn();
        if (latestTurn.host_turn.initialized && latestTurn.guest_turn.initialized)
        {
            if (bm.isHost)
            {
                if (string.IsNullOrEmpty(latestTurn.host_events_hash))
                {
                    Debug.Log("Submitting move hash for host because both players have initialized their turns");
                    StellarManagerTest.SubmitMoveHash();
                }
            }
            else
            {
                if (string.IsNullOrEmpty(latestTurn.guest_events_hash))
                {
                    Debug.Log("Submitting move hash for guest because both players have initialized their turns");
                    StellarManagerTest.SubmitMoveHash();
                }
            }
            
        }
    }
    
    HashSet<TestTileView> GetMovableTileViews(Contract.Pawn pawn)
    {
        BoardDef boardDef = bm.boardDef;
        HashSet<TestTileView> movableTileViews = new();
        PawnDef def = Globals.FakeHashToPawnDef(pawn.pawn_def_hash);
        if (!pawn.is_alive)
        {
            return movableTileViews;
        }
        if (def.movementRange == 0)
        {
            return movableTileViews;
        }
        Vector2Int pawnPos = pawn.pos.ToVector2Int();
        Vector2Int[] initialDirections = Shared.GetDirections(pawnPos, boardDef.isHex);
        for (int dirIndex = 0; dirIndex < initialDirections.Length; dirIndex++)
        {
            Vector2Int currentPos = pawnPos;
            int walkedTiles = 0;
            while (walkedTiles < def.movementRange)
            {
                Vector2Int[] currentDirections = Shared.GetDirections(currentPos, boardDef.isHex);
                currentPos += currentDirections[dirIndex];
                TestTileView tileView = bm.GetTileViewAtPos(currentPos);
                if (!tileView) break;
                if (!tileView.tile.isPassable) break;
                Contract.Pawn? maybePawnOnPos = bm.GetCachedPawnState(currentPos);
                if (maybePawnOnPos.HasValue)
                {
                    if (maybePawnOnPos.Value.team == pawn.team) break;
                    movableTileViews.Add(tileView);
                    break;
                }
                movableTileViews.Add(tileView);
                walkedTiles++;
            }
        }
        return movableTileViews;
    }

    void SubmitMove()
    {
        if (clientState.queuedMove == null) return;
        _ = StellarManagerTest.QueueMove(clientState.queuedMove);
        clientState.ClearQueueMove();
        ClientStateChanged();
        bm.OnlyClientGameStateChanged();
    }

    void RefreshState()
    {
        _ = StellarManagerTest.UpdateState();
    }

    void SetAutoSubmit(bool autoSubmit)
    {
        clientState.SetAutoSubmit(autoSubmit);
    }
}

public class QueuedMove
{
    public Guid pawnId;
    public Vector2Int pos;
}
