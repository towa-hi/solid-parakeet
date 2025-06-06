using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Contract;
using JetBrains.Annotations;
using Stellar;
using UnityEngine;
using UnityEngine.Assertions;

public class TestBoardManager : MonoBehaviour
{
    public bool initialized;
    // never mutate these externally
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public GameObject setupPawnPrefab;
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
    public List<SetupPawnView> setupPawnViews = new();
    // last known lobby
    //public Lobby cachedLobby;
    public GameNetworkState cachedNetworkState;
    public Phase lastPhase;
    public ITestPhase currentPhase;
    public Transform cameraBounds;

    public Transform waveOrigin1;
    public Transform waveOrigin2;

    
    //public event Action<Lobby> OnPhaseChanged;
    public event Action<GameNetworkState, ITestPhase> OnClientGameStateChanged;
    public event Action<Vector2Int, TestTileView, TestPawnView, ITestPhase> OnGameHover;
    
    void Start()
    {
        clickInputManager.OnClick += OnClick;
        clickInputManager.OnPositionHovered += OnHover;
        StellarManagerTest.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }

    bool firstTime;

    public static bool singlePlayer;
    
    public void StartBoardManager(bool networkUpdated)
    {
        clickInputManager.Initialize(this);
        if (!networkUpdated)
        {
            singlePlayer = true;
            //FakeServer.ins.StartFakeLobby();
            //Initialize(FakeServer.ins.fakeHost, FakeServer.ins.fakeLobby);
            //only invoke this directly once on start
        }
        else
        {
            Assert.IsTrue(StellarManagerTest.networkState.user.HasValue);
            GameNetworkState networkState = new GameNetworkState(StellarManagerTest.networkState);
            Initialize(networkState);
            //only invoke this directly once on start
        }
        initialized = true;
        firstTime = true;
        OnNetworkStateUpdated(); //only invoke this directly once on start
    }

    public void FakeOnNetworkStateUpdated()
    {
        // only called by FakeServer in singleplayer mode
        Debug.Log("FakeOnNetworkStateUpdated");
        OnNetworkStateUpdated();
    }
    
    void OnNetworkStateUpdated()
    {
        Debug.Log("TestBoardManager::OnNetworkStateUpdated");
        if (!initialized)
        {
            return;
        }
        GameNetworkState networkState = new GameNetworkState(StellarManagerTest.networkState);
        if (firstTime || networkState.gameState.phase != lastPhase)
        {
            firstTime = false;
            switch (networkState.gameState.phase)
            {
                case Phase.Setup:
                    Debug.Log("SetPhase setup");
                    SetPhase(new SetupTestPhase(this, guiTestGame.setup, networkState));
                    break;
                case Phase.Movement:
                    Debug.Log("SetPhase movement");
                    SetPhase(new MovementTestPhase(this, guiTestGame.movement, networkState));
                    break;
                case Phase.Completed:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        // if this is the first time, currentPhase gets it's state changed here
        currentPhase?.OnNetworkGameStateChanged(networkState);
        // refreshGui happens first
        currentPhase?.RefreshGui();
        Debug.Log("OnClientGameStateChanged invoked by OnNetworkStateUpdated");
        OnClientGameStateChanged?.Invoke(networkState, currentPhase);
        cachedNetworkState = networkState;
        lastPhase = networkState.gameState.phase;
        clickInputManager.ForceInvokeOnPositionHovered();
    }

    public void OnlyClientGameStateChanged()
    {
        // this function is only called from within currentState
        // when the phase is running and user input changed something
        Debug.Log("OnClientGameStateChanged invoked by OnlyClientGameStateChanged");
        currentPhase?.RefreshGui();
        OnClientGameStateChanged?.Invoke(cachedNetworkState, currentPhase);
        clickInputManager.ForceInvokeOnPositionHovered();
    }
    
    void Initialize(GameNetworkState networkState)
    {
        // get boarddef from hash
        BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
        SHA256 sha256 = SHA256.Create();
        boardDef = boardDefs.First(def => 
            sha256.ComputeHash(Encoding.UTF8.GetBytes(def.boardName)).SequenceEqual(networkState.lobbyParameters.board_hash)
        );
        if (!boardDef)
        {
            throw new NullReferenceException();
        }
        cameraBounds.position = cameraBounds.position + transform.position + boardDef.center;
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
        // foreach (Contract.Pawn p in lobby.pawns)
        // {
        //     GameObject pawnObject = Instantiate(pawnPrefab, transform);
        //     TestPawnView pawnView = pawnObject.GetComponent<TestPawnView>();
        //     pawnView.Initialize(p, this);
        //     pawnViews.Add(pawnView);
        // }
        // lobbyId = lobby.index;
        // isHost = lobby.host_address == user.index;
    }
    
    public List<TestPawnView> GetMyPawnViews()
    {
        return pawnViews.Where(pv => pv.team == userTeam).ToList();
    }
    
    public Tile GetTileAtPos(Vector2Int pos)
    {
        TestTileView tileView = tileViews.GetValueOrDefault(pos);
        return tileView?.tile;
    }

    public TestTileView GetTileViewAtPos(Vector2Int pos)
    {
        if (pos == Globals.Purgatory)
        {
            return null;
        }
        TestTileView tileView = tileViews.GetValueOrDefault(pos);
        return tileView;
    }

    TestPawnView GetPawnViewAtPos(Vector2Int pos)
    {
        if (pos == Globals.Purgatory)
        {
            return null;
        }
        return pawnViews.FirstOrDefault(pv => pv.displayedPos == pos);
    }
    
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
        currentPhase?.OnHover(pos, tileView, pawnView);
        OnGameHover?.Invoke(pos, tileView, pawnView, currentPhase);
    }
}

public interface ITestPhase
{
    public void EnterState();
    public void ExitState();
    public void Update();
    public void OnHover(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView);
    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView);

    public void OnNetworkGameStateChanged(GameNetworkState networkState);

    public void RefreshGui();

}

public class SetupClientState
{
    public bool dirty = true;
    //public Dictionary<uint, PawnCommit> commitments;
    public PawnCommit[] lockedCommits;
    public List<SetupPawnView> setupPawns;
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
        SetupPawnView setupPawnView = GetUnusedSetupPawnView(rank);
        if (!setupPawnView)
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
        PawnCommit? maybeCommitment = GetUnusedCommitment(selectedRank.Value);
        if (!maybeCommitment.HasValue)
        {
            return false;
        }
        ChangeCommitment(maybeCommitment.Value.pawn_id, pos);
        return true;
    }

    public void ClearCommitment(uint id)
    {
        ChangeCommitment(id, Globals.Purgatory);
    }
    
    public void ClearAllCommitments()
    {
        List<uint> keys = commitments.Keys.ToList();
        foreach (uint key in keys)
        {
            ChangeCommitment(key, Globals.Purgatory);
        }
    }
    
    void ChangeCommitment(uint id, Vector2Int pos)
    {
        PawnCommit commitment = commitments[id];
        commitment.starting_pos = new Pos(pos);
        commitments[id] = commitment;
        dirty = true;
    }

    public SetupPawnView GetUnusedSetupPawnView(Rank rank)
    {
        foreach (SetupPawnView setupPawnView in setupPawns)
        {
            if (setupPawnView.pos == Globals.Purgatory && setupPawnView.rank == rank)
            {
                return setupPawnView;
            }
        }
        return null;
    }
}

public class SetupTestPhase : ITestPhase
{
    TestBoardManager bm;
    GuiTestSetup setupGui;

    public SetupClientState clientState;
    
    public SetupTestPhase(TestBoardManager inBm, GuiTestSetup inSetupGui, GameNetworkState networkState)
    {
        bm = inBm;
        setupGui = inSetupGui;
        // TODO: figure out a better way to tell gui to do stuff
        bm.guiTestGame.SetCurrentElement(setupGui, networkState);
    }

    public void RefreshGui()
    {
        setupGui.Refresh(clientState);
    }
    
    void ResetClientState(GameNetworkState networkState)
    {
        Debug.Log("SetupTestPhase.ResetClientState");
        UserState userState = networkState.GetUserState();
        SetupClientState newSetupClientState = new SetupClientState()
        {
            selectedRank = null,
            commitments = new Dictionary<uint, PawnCommit>(),
            committed = userState.setup_hash.All(b => b == 0),
            team = networkState.clientTeam,
        };
        // we can assume every max in maxRanks sums to commitIndex - 1;
        int commitIndex = 0;
        // userState.setup is going to be empty in setup phase
        // if already committed, set commitments to stored values
        if (newSetupClientState.committed)
        {
            string proveSetupReqXdr = PlayerPrefs.GetString(networkState.GetProveSetupReqPlayerPrefsKey());
            ProveSetupReq req = ProveSetupReq.FromXdrString(proveSetupReqXdr);
            // TODO: get commitments here
            foreach (var commit in req.setup)
            {
                newSetupClientState.commitments.Add(commit.pawn_id, new PawnCommit());
            }
        }
        else
        {
            
        }
        // foreach (Tile tile in bm.boardDef.tiles)
        // {
        //     if (tile.setupTeam == networkState.clientTeam)
        //     {
        //         uint id = (uint)tile.pos.x * 101 + (uint)tile.pos.y;
        //         commits[tile.pos] = new PawnCommit
        //         {
        //             pawn_def_hash = null,
        //             pawn_id = id,
        //             starting_pos = new Pos(tile.pos),
        //         };
        //     }
        // }
        // TODO: figure out how to fill up newSetupClientState.commitments
        // foreach (MaxRank maxRanks in networkState.lobbyParameters.max_ranks)
        // {
        //     for (int i = 0; i < maxRanks.max; i++)
        //     {
        //         string pawnDefHash = Globals.PawnDefToFakeHash(Globals.RankToPawnDef(maxRanks.rank));
        //         PawnCommit commitment = new PawnCommit()
        //         {
        //             pawn_def_hash = pawnDefHash, // this is the only original data we fill in
        //             pawn_id = userState.setup[commitIndex].pawn_id,
        //             starting_pos = userState.setup[commitIndex].starting_pos,
        //         };
        //         newSetupClientState.commitments[commitment.pawn_id] = commitment;
        //         commitIndex += 1;
        //     }
        // }
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

    public void OnHover(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
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

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        ResetClientState(networkState);
    }

    void ClientStateChanged()
    {
        Debug.Log("SetupClientState.ClientStateChanged");
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
        // clientState.ClearAllCommitments();
        // // Generate valid setup positions for each pawn
        // HashSet<Tile> usedTiles = new();
        // Contract.MaxPawns[] sortedMaxPawns = bm.parameters.max_pawns;
        // Array.Sort(sortedMaxPawns, (maxPawns1, maxPawns2) => Rules.GetSetupZone((Rank)maxPawns1.rank) < Rules.GetSetupZone((Rank)maxPawns2.rank) ? 1 : -1);
        // foreach (MaxPawns maxPawns in sortedMaxPawns)
        // {
        //     for (int i = 0; i < maxPawns.max; i++)
        //     {
        //         // Get available tiles for this rank
        //         List<Tile> availableTiles = bm.boardDef.GetEmptySetupTiles(bm.userTeam, (Rank)maxPawns.rank, usedTiles);
        //         if (availableTiles.Count == 0)
        //         {
        //             Debug.LogError($"No available tiles for rank {maxPawns.rank}");
        //             continue;
        //         }
        //         // Pick a random tile from available tiles
        //         int randomIndex = UnityEngine.Random.Range(0, availableTiles.Count);
        //         Tile selectedTile = availableTiles[randomIndex];
        //         // Find a pawn of this rank in purgatory
        //         bool success = clientState.SetCommitment((Rank)maxPawns.rank, selectedTile.pos);
        //         if (success)
        //         {
        //             usedTiles.Add(selectedTile);
        //         }
        //         else
        //         {
        //             Debug.LogError($"No available pawns of rank {maxPawns.rank} to place");
        //         }
        //     }
        // }
        // ClientStateChanged();
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

        if (TestBoardManager.singlePlayer)
        {
            //FakeServer.ins.CommitSetupRequest(clientState.commitments);
        }
        else
        {
            _ = StellarManagerTest.CommitSetupRequest(clientState.commitments.Values.ToArray());
        }
    }
}

public abstract class MovementClientSubState { }

public class SelectingPawnMovementClientSubState : MovementClientSubState
{
    public Vector2Int? selectedPos;
    public uint? selectedPawnId;
    
    public SelectingPawnMovementClientSubState(uint? inSelectedPawnId = null, Vector2Int? inSelectedPos = null)
    {
        selectedPawnId = inSelectedPawnId;
        selectedPos = inSelectedPos;
        if (MovementClientState.autoSubmit)
        {
            SubmitMove();
        }
    }

    public void SubmitMove()
    {
        if (!selectedPawnId.HasValue || !selectedPos.HasValue) return;
        if (TestBoardManager.singlePlayer)
        {
            FakeServer.ins.QueueMove(new QueuedMove { pawnId = selectedPawnId.Value, pos = selectedPos.Value });

        }
        else
        {
            _ = StellarManagerTest.QueueMove(new QueuedMove { pawnId = selectedPawnId.Value, pos = selectedPos.Value });
        }
    }
}

public class SelectingPosMovementClientSubState : MovementClientSubState
{
    public uint selectedPawnId;
    public HashSet<Vector2Int> highlightedTiles;

    public SelectingPosMovementClientSubState(uint inPawnId, HashSet<Vector2Int> inHighlightedTiles)
    {
        selectedPawnId = inPawnId;
        highlightedTiles = inHighlightedTiles;
    }
}

public class WaitingUserHashMovementClientSubState : MovementClientSubState { }

public class WaitingOpponentMoveMovementClientSubState : MovementClientSubState { }

public class WaitingOpponentHashMovementClientSubState : MovementClientSubState { }

public class ResolvingMovementClientSubState : MovementClientSubState { }

public class GameOverMovementClientSubState : MovementClientSubState
{
    public uint endState;

    public GameOverMovementClientSubState(uint inEndState)
    {
        endState = inEndState;
    }

    public string EndStateMessage()
    {
        switch (endState)
        {
            case 0:
                return "Game tied";
            case 1:
                return "Red team won";
            case 2:
                return "Blue team won";
            case 3:
                return "Game in session";
            case 4:
                return "Game ended inconclusively";
            default:
                return "Game over";
        }
    }
}

public class MovementClientState
{
    public bool dirty = true;
    public Team team;
    public Contract.ResolveEvent[] myEvents;
    public string myEventsHash;
    public TurnMove myTurnMove;
    public Contract.ResolveEvent[] otherEvents;
    public string otherEventsHash;
    public TurnMove otherTurnMove;
    public int turn;
    public static bool autoSubmit;

    public MovementClientSubState subState;
    readonly BoardDef boardDef;
    readonly Dictionary<Vector2Int, Contract.Pawn> pawnPositions;

    public MovementClientState(GameNetworkState networkState, bool isHost, Team myTeam, BoardDef boardDef)
    {
        // this.boardDef = boardDef;
        // Turn currentTurn = lobby.GetLatestTurn();
        // dirty = true;
        // team = myTeam;
        // myEvents = isHost ? currentTurn.host_events : currentTurn.guest_events;
        // myEventsHash = isHost ? currentTurn.host_events_hash : currentTurn.guest_events_hash;
        // myTurnMove = isHost ? currentTurn.host_turn : currentTurn.guest_turn;
        // otherEvents = isHost ? currentTurn.guest_events : currentTurn.host_events;
        // otherEventsHash = isHost ? currentTurn.guest_events_hash : currentTurn.host_events_hash;
        // otherTurnMove = isHost ? currentTurn.guest_turn : currentTurn.host_turn;
        // turn = currentTurn.turn;
        // // Build pawn position lookup
        // pawnPositions = new Dictionary<Vector2Int, Contract.Pawn>();
        // foreach (Contract.Pawn pawn in lobby.pawns)
        // {
        //     pawnPositions[pawn.pos.ToVector2Int()] = pawn;
        // }
        // // Initialize state based on current conditions
        // if (lobby.game_end_state == 3)
        // {
        //     if (myTurnMove.initialized)
        //     {
        //         if (otherTurnMove.initialized)
        //         {
        //             if (!string.IsNullOrEmpty(myEventsHash))
        //             {
        //                 if (!string.IsNullOrEmpty(otherEventsHash))
        //                 {
        //                     Assert.IsTrue(myEvents == otherEvents);
        //                     subState = new ResolvingMovementClientSubState();
        //                 }
        //                 else
        //                 {
        //                     subState = new WaitingOpponentHashMovementClientSubState();
        //                 }
        //             }
        //             else
        //             {
        //                 subState = new WaitingUserHashMovementClientSubState();
        //             }
        //         }
        //         else
        //         {
        //             subState = new WaitingOpponentMoveMovementClientSubState();
        //         }
        //     }
        //     else
        //     {
        //         subState = new SelectingPawnMovementClientSubState();
        //     }
        // }
        // else
        // {
        //     subState = new GameOverMovementClientSubState(lobby.game_end_state);
        // }
        
    }

    void SetSubState(MovementClientSubState newState)
    {
        subState = newState;
        dirty = true;
    }

    void TransitionToSelectingPos(uint pawnId)
    {
        SetSubState(new SelectingPosMovementClientSubState(pawnId, GetMovableTilePositions(pawnId, boardDef, pawnPositions)));
    }

    void TransitionToSelectingPawn(uint? selectedPawnId = null, Vector2Int? selectedPos = null)
    {
        SetSubState(new SelectingPawnMovementClientSubState(selectedPawnId, selectedPos));
    }

    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        switch (subState)
        {
            case SelectingPawnMovementClientSubState:
                if (pawnView && pawnView.team == team)
                {
                    TransitionToSelectingPos(pawnView.pawnId);
                }
                else
                {
                    TransitionToSelectingPawn();
                }
                break;

            case SelectingPosMovementClientSubState selectingPosSubState:
                if (selectingPosSubState.highlightedTiles.Contains(clickedPos))
                {
                    TransitionToSelectingPawn(selectingPosSubState.selectedPawnId, clickedPos);
                }
                else
                {
                    if (pawnView && pawnView.team == team)
                    {
                        TransitionToSelectingPos(pawnView.pawnId);
                    }
                    else
                    {
                        TransitionToSelectingPawn();
                    }
                }
                break;
        }
    }
    
    static HashSet<Vector2Int> GetMovableTilePositions(uint pawnId, BoardDef boardDef, Dictionary<Vector2Int, Contract.Pawn> pawnPositions)
    {
        Contract.Pawn pawn = pawnPositions.Values.First(p => p.pawn_id == pawnId);
        HashSet<Vector2Int> movableTilePositions = new();
        PawnDef def = Globals.FakeHashToPawnDef(pawn.pawn_def_hash);
        if (!pawn.is_alive || def.movementRange == 0)
        {
            return movableTilePositions;
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
                Tile tile = boardDef.GetTileByPos(currentPos);
                if (tile == null || !tile.isPassable) break;
                if (pawnPositions.TryGetValue(currentPos, out Contract.Pawn value))
                {
                    if (value.team == pawn.team) break;
                    movableTilePositions.Add(currentPos);
                    break;
                }
                movableTilePositions.Add(currentPos);
                walkedTiles++;
            }
        }
        return movableTilePositions;
    }
}

public class MovementTestPhase : ITestPhase
{
    TestBoardManager bm;
    GuiTestMovement movementGui;
    
    public MovementClientState clientState;
    
    public MovementTestPhase(TestBoardManager inBm, GuiTestMovement inMovementGui, GameNetworkState networkState)
    {
        bm = inBm;
        movementGui = inMovementGui;
        bm.guiTestGame.SetCurrentElement(movementGui, networkState);
    }

    public void RefreshGui()
    {
        movementGui.Refresh(clientState);
    }
    
    void ResetClientState(GameNetworkState networkState)
    {
        Debug.Log("ResetClientState");
        clientState = new MovementClientState(networkState, bm.isHost, bm.userTeam, bm.boardDef);
    }

    void ClientStateChanged()
    {
        bm.OnlyClientGameStateChanged();
    }
    
    public void EnterState()
    {
        movementGui.OnSubmitMoveButton += SubmitMove;
        movementGui.OnRefreshButton += RefreshState;
        movementGui.OnAutoSubmitToggle += SetAutoSubmit;
        movementGui.OnCheatButton += SetCheatMode;
        movementGui.OnBadgeButton += SetBadgeVisibility;
    }

    public void ExitState()
    {
        movementGui.OnSubmitMoveButton -= SubmitMove;
        movementGui.OnRefreshButton -= RefreshState;
        movementGui.OnAutoSubmitToggle -= SetAutoSubmit;
        movementGui.OnCheatButton -= SetCheatMode;
        movementGui.OnBadgeButton -= SetBadgeVisibility;
    }

    public void Update() {}

    public void OnHover(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        
    }
    
    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        if (bm.currentPhase != this) return;
        clientState.OnClick(clickedPos, tileView, pawnView);
        if (clientState.dirty)
        {
            ClientStateChanged();
        }
    }

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        // ResetClientState(lobby);
        // Turn latestTurn = lobby.GetLatestTurn();
        // // TODO: make this stateful
        // if (latestTurn.host_turn.initialized && latestTurn.guest_turn.initialized)
        // {
        //     if (bm.isHost)
        //     {
        //         if (string.IsNullOrEmpty(latestTurn.host_events_hash))
        //         {
        //             Debug.Log("Submitting move hash for host because both players have initialized their turns");
        //             if (TestBoardManager.singlePlayer)
        //             {
        //                 FakeServer.ins.SubmitMoveHash();
        //             }
        //             else
        //             {
        //                 _ = StellarManagerTest.SubmitMoveHash();
        //             }
        //             
        //         }
        //     }
        //     else
        //     {
        //         if (string.IsNullOrEmpty(latestTurn.guest_events_hash))
        //         {
        //             Debug.Log("Submitting move hash for guest because both players have initialized their turns");
        //             if (TestBoardManager.singlePlayer)
        //             {
        //                 FakeServer.ins.SubmitMoveHash();
        //             }
        //             else
        //             {
        //                 _ = StellarManagerTest.SubmitMoveHash();
        //             }
        //         }
        //     }
        // }
    }

    void SubmitMove()
    {
        if (clientState.subState is SelectingPawnMovementClientSubState selectingState)
        {
            selectingState.SubmitMove();
        }
    }

    void RefreshState()
    {
        _ = StellarManagerTest.UpdateState();
    }

    void SetAutoSubmit(bool autoSubmit)
    {
        MovementClientState.autoSubmit = autoSubmit;
    }

    void SetCheatMode()
    {
        int cheatMode = PlayerPrefs.GetInt("CHEATMODE");
        if (cheatMode == 0)
        {
            PlayerPrefs.SetInt("CHEATMODE", 1);
        }
        else
        {
            PlayerPrefs.SetInt("CHEATMODE", 0);
        }
        ClientStateChanged();
    }

    void SetBadgeVisibility()
    {
        int displayBadge = PlayerPrefs.GetInt("DISPLAYBADGE");
        if (displayBadge == 0)
        {
            PlayerPrefs.SetInt("DISPLAYBADGE", 1);
        }
        else
        {
            PlayerPrefs.SetInt("DISPLAYBADGE", 0);
        }
        ClientStateChanged();
    }
}

public class QueuedMove
{
    public uint pawnId;
    public Vector2Int pos;
}
