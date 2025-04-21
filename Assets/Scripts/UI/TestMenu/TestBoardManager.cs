using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contract;
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
    public event Action<Lobby> OnClientGameStateChanged;
    public event Action<Lobby> OnNetworkGameStateChanged;
    
    void Start()
    {
        clickInputManager.OnClick += OnClick;
        StellarManagerTest.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }

    public void ClientGameStateChanged()
    {
        OnClientGameStateChanged?.Invoke(cachedLobby);
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
        if (firstTime || lobby.phase != cachedLobby.phase)
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
        currentPhase?.OnNetworkGameStateChanged(lobby);
        Debug.Log("invoking OnNetworkGameStateChanged");
        OnNetworkGameStateChanged?.Invoke(lobby);
        Debug.Log("invoking OnClientGameStateChanged");
        OnClientGameStateChanged?.Invoke(lobby);
        cachedLobby = lobby;
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

public class SetupTestPhase : ITestPhase
{
    TestBoardManager bm;
    GuiTestSetup setupGui;
    public Dictionary<string, PawnCommitment> commitments;
    public Rank? selectedRank;
    public bool committed = false;
    
    public SetupTestPhase(TestBoardManager inBm, GuiTestSetup inSetupGui, Lobby lobby)
    {
        bm = inBm;
        setupGui = inSetupGui;
        commitments = new Dictionary<string, PawnCommitment>();
        UserState userState = lobby.GetUserStateByTeam(bm.userTeam);
        committed = userState.committed;
        if (committed)
        {
            foreach (PawnCommitment commitment in userState.setup_commitments)
            {
                commitments[commitment.pawn_id] = commitment;
            }
        }
        else
        {
            // we can assume every max in maxRanks sums to commitIndex - 1;
            int commitIndex = 0;
            foreach (MaxPawns maxRanks in lobby.parameters.max_pawns)
            {
                for (int i = 0; i < maxRanks.max; i++)
                {
                    PawnCommitment commitment = new PawnCommitment()
                    {
                        pawn_def_hash = Globals.PawnDefToFakeHash(Globals.RankToPawnDef((Rank)maxRanks.rank)),
                        pawn_id = userState.setup_commitments[commitIndex].pawn_id,
                        starting_pos = new Pos(Globals.Purgatory),
                    };
                    commitments[commitment.pawn_id] = commitment;
                    commitIndex += 1;
                }
            }
        }
        
        // TODO: figure out a better way to tell gui to do stuff
        bm.guiTestGame.SetCurrentElement(setupGui, lobby);
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
            ChangeCommitment(pawnView.pawnId.ToString(), Globals.Purgatory);
            setupGui.Refresh(this);
            bm.ClientGameStateChanged();
            return;
        }
        // If no pawn at position and we have a selected rank, try to place a pawn
        if (selectedRank.HasValue)
        {
            // if tile is your setup tile
            if (tileView.tile.IsTileSetupAllowed(bm.userTeam))
            {
                // Find the first available pawn of the selected rank
                PawnCommitment? maybeCommitment = GetUnusedCommitment(selectedRank.Value);
                if (maybeCommitment.HasValue)
                {
                    PawnCommitment commitment = maybeCommitment.Value;
                    commitment.starting_pos = new Pos(clickedPos);
                    commitments[maybeCommitment.Value.pawn_id] = commitment;
                    setupGui.Refresh(this);
                    bm.ClientGameStateChanged();
                }
                else
                {
                    Debug.LogWarning($"No available pawns of rank {selectedRank.Value} to place");
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
    }

    public void OnNetworkGameStateChanged(Lobby lobby)
    {
        setupGui.Refresh(this);
    }

    void OnRankEntryClicked(Rank rank)
    {
        if (selectedRank == rank)
        {
            selectedRank = null;
        }
        else
        {
            selectedRank = rank;
        }
        setupGui.Refresh(this);
    }
    
    void OnClear()
    {
        List<string> keys = commitments.Keys.ToList();
        foreach (string key in keys)
        {
            PawnCommitment commitment = commitments[key];
            commitment.starting_pos = new Pos(Globals.Purgatory);
            commitments[key] = commitment;
        }
    }

    void OnAutoSetup()
    {
        OnClear();
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
                PawnCommitment? maybeCommitment = GetUnusedCommitment((Rank)maxPawns.rank);
                if (maybeCommitment.HasValue)
                {
                    PawnCommitment commitment = maybeCommitment.Value;
                    commitment.starting_pos = new Pos(selectedTile.pos);
                    commitments[maybeCommitment.Value.pawn_id] = commitment;
                }
                else
                {
                    Debug.LogError($"No available pawns of rank {maxPawns.rank} to place");
                }
            }
        }
        setupGui.Refresh(this);
        bm.ClientGameStateChanged();
    }

    void ChangeCommitment(string id, Vector2Int pos)
    {
        PawnCommitment commitment = commitments[id];
        commitment.starting_pos = new Pos(pos);
        commitments[id] = commitment;
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
    
    void OnRefresh()
    {
        _ = StellarManagerTest.UpdateState();
    }

    async void OnSubmit()
    {
        foreach (var commitment in commitments.Values)
        {
            if (commitment.starting_pos.ToVector2Int() == Globals.Purgatory)
            {
                Debug.LogWarning("Submit rejected because all pawns must have commitments");
                return;
            }
        }
        int code = await StellarManagerTest.CommitSetupRequest(commitments);
        Debug.Log(code);
        
    }
}

public class MovementTestPhase : ITestPhase
{
    TestBoardManager bm;
    public TestPawnView selectedPawnView;
    public QueuedMove queuedMove;
    public HashSet<TestTileView> highlightedTiles;
    
    GuiTestMovement movementGui;
    
    public Turn cachedTurn;
    public TurnMove committedMove;
    
    public MovementTestPhase(TestBoardManager inBm, GuiTestMovement inMovementGui, Lobby lobby)
    {
        bm = inBm;
        movementGui = inMovementGui;
        highlightedTiles = new HashSet<TestTileView>();
        // TODO: figure out a better way to tell gui to do stuff
        bm.guiTestGame.SetCurrentElement(movementGui, lobby);
        cachedTurn = lobby.GetLatestTurn();
        committedMove = lobby.GetLatestTurnMove(bm.userTeam);
        Debug.Log(committedMove);
    }
    
    public void EnterState()
    {
        selectedPawnView = null;
        movementGui.OnSubmitMoveButton += SubmitMove;
        movementGui.OnRefreshButton += RefreshState;
        
    }

    public void ExitState()
    {
        selectedPawnView = null;
        movementGui.OnSubmitMoveButton -= SubmitMove;
        movementGui.OnRefreshButton -= RefreshState;
        
    }

    public void Update() {}
    public void OnHover()
    {
        
    }

    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        if (bm.currentPhase != this) return;
        if (committedMove.initialized)
        {
            return;
        }
        if (pawnView && pawnView.team == bm.userTeam)
        {
            selectedPawnView = pawnView;
            Contract.Pawn pawn = bm.GetCachedPawnStateUnchecked(pawnView.pawnId);
            highlightedTiles = GetMovableTileViews(pawn);
        }
        else
        {
            QueueMove(selectedPawnView, tileView);
            selectedPawnView = null;
            highlightedTiles.Clear();
        }
        movementGui.Refresh(this);
        bm.ClientGameStateChanged();
    }

    public void OnNetworkGameStateChanged(Lobby lobby)
    {
        cachedTurn = lobby.GetLatestTurn();
        committedMove = lobby.GetLatestTurnMove(bm.userTeam);
        if (committedMove.initialized)
        {
            highlightedTiles.Clear();
            queuedMove = null;
            selectedPawnView = null;
        }
        Debug.Log(committedMove);
        movementGui.Refresh(this);
    }

    void QueueMove(TestPawnView pawnView, TestTileView tileView = null)
    {
        queuedMove = null;
        if (pawnView && tileView)
        {
            if (highlightedTiles.Contains(tileView))
            {
                queuedMove = new QueuedMove
                {
                    pawnId = pawnView.pawnId,
                    pos = tileView.tile.pos,
                };
            }
        }
        Debug.Log(queuedMove == null
            ? "QueueMove set to null"
            : $"QueuedMove set to {queuedMove.pawnId} to {queuedMove.pos}");
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
        if (queuedMove == null) return;
        _ = StellarManagerTest.QueueMove(queuedMove);
        highlightedTiles.Clear();
        queuedMove = null;
        selectedPawnView = null;
    }

    void RefreshState()
    {
        
    }
}

public class QueuedMove
{
    public Guid pawnId;
    public Vector2Int pos;
}