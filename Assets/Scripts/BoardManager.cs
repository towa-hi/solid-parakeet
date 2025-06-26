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
using Random = UnityEngine.Random;

public class BoardManager : MonoBehaviour
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
    [FormerlySerializedAs("guiTestGame")] public GuiGame guiGame;
    // generally doesn't change after lobby is set in StartGame
    public BoardDef boardDef;
    public LobbyParameters parameters;
    public bool isHost;
    public string lobbyId;
    // internal game state. call OnStateChanged when updating these. only StartGame can make new views
    public Dictionary<Vector2Int, TileView> tileViews = new();
    public List<PawnView> pawnViews = new();
    public Dictionary<Vector2Int, SetupPawnView> setupPawnViews = new();
    // last known lobby
    //public Lobby cachedLobby;
    public GameNetworkState cachedNetworkState;
    public Phase lastPhase;
    public IPhase currentPhase;
    public Transform cameraBounds;

    public Transform waveOrigin1;
    public Transform waveOrigin2;

    
    //public event Action<Lobby> OnPhaseChanged;
    public event Action<GameNetworkState, IPhase> OnClientGameStateChanged;
    public event Action<Vector2Int, TileView, PawnView, IPhase> OnGameHover;
    
    void Start()
    {
        clickInputManager.OnClick += OnClick;
        clickInputManager.OnPositionHovered += OnHover;
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
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
            Assert.IsTrue(StellarManager.networkState.user.HasValue);
            GameNetworkState networkState = new(StellarManager.networkState);
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
        GameNetworkState networkState = new(StellarManager.networkState);
        if (firstTime || networkState.lobbyInfo.phase != lastPhase)
        {
            firstTime = false;
            switch (networkState.lobbyInfo.phase)
            {
                case Phase.SetupCommit:
                    SetPhase(new SetupCommitPhase(this, guiGame.setup));
                    break;
                case Phase.SetupProve:
                    SetPhase(new SetupProvePhase(this, guiGame.setup));
                    break;
                case Phase.MoveCommit:
                    SetPhase(new MoveCommitPhase(this, guiGame.movement));
                    break;
                case Phase.MoveProve:
                    SetPhase(new MoveProvePhase(this, guiGame.movement));
                    break;
                case Phase.RankProve:
                    SetPhase(new RankProvePhase(this, guiGame.movement));
                    break;
                case Phase.Finished:
                case Phase.Aborted:
                case Phase.Lobby:
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
        lastPhase = networkState.lobbyInfo.phase;
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
        boardDef = boardDefs.First(def => 
            def.GetHash().SequenceEqual(networkState.lobbyParameters.board_hash)
        );
        if (!boardDef)
        {
            throw new NullReferenceException();
        }
        cameraBounds.position = cameraBounds.position + transform.position + boardDef.center;
        // Clear existing tileviews and replace
        foreach (TileView tile in tileViews.Values)
        {
            Destroy(tile.gameObject);
        }
        tileViews.Clear();
        grid.SetBoard(new SBoardDef(boardDef));
        foreach (Tile tile in boardDef.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(tile, this);
            tileViews.Add(tile.pos, tileView);
        }
        // Clear any existing pawnviews and replace
        foreach (PawnView pawnView in pawnViews)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();
        foreach (SetupPawnView setupPawnView in setupPawnViews.Values)
        {
            Destroy(setupPawnView.gameObject);
        }
        setupPawnViews.Clear();
        foreach (Tile tile in boardDef.tiles)
        {
            if (tile.IsTileSetupAllowed(networkState.userTeam))
            {
                GameObject setupPawnObject = Instantiate(setupPawnPrefab);
                SetupPawnView setupPawnView = setupPawnObject.GetComponent<SetupPawnView>();
                setupPawnView.Initialize(GetTileViewAtPos(tile.pos), networkState.userTeam, this);
            }
        }
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
    
    public Tile GetTileAtPos(Vector2Int pos)
    {
        TileView tileView = tileViews.GetValueOrDefault(pos);
        return tileView?.tile;
    }

    public TileView GetTileViewAtPos(Vector2Int pos)
    {
        if (pos == Globals.Purgatory)
        {
            return null;
        }
        TileView tileView = tileViews.GetValueOrDefault(pos);
        return tileView;
    }

    PawnView GetPawnViewAtPos(Vector2Int pos)
    {
        if (pos == Globals.Purgatory)
        {
            return null;
        }
        return pawnViews.FirstOrDefault(pv => pv.displayedPos == pos);
    }
    
    void SetPhase(IPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
    }
    
    void OnClick(Vector2Int pos)
    {
        TileView tileView = GetTileViewAtPos(pos);
        PawnView pawnView = GetPawnViewAtPos(pos);
        currentPhase?.OnClick(pos, tileView, pawnView);
    }

    void OnHover(Vector2Int pos)
    {
        TileView tileView = GetTileViewAtPos(pos);
        PawnView pawnView = GetPawnViewAtPos(pos);
        currentPhase?.OnHover(pos, tileView, pawnView);
        OnGameHover?.Invoke(pos, tileView, pawnView, currentPhase);
    }
}

public interface IPhase
{
    public void EnterState();
    public void ExitState();
    public void Update();
    public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView);
    public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView);

    public void OnNetworkGameStateChanged(GameNetworkState networkState);

    public void RefreshGui();

}

public class SetupCommitPhase : IPhase
{
    GuiSetup guiSetup;
    public SetupCommitPhase(BoardManager bm, GuiSetup guiSetup)
    {
        this.guiSetup = guiSetup;
    }
    
    public void EnterState()
    {
        throw new NotImplementedException();
    }

    public void ExitState()
    {
        throw new NotImplementedException();
    }

    public void Update()
    {
        throw new NotImplementedException();
    }

    public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        throw new NotImplementedException();
    }

    public void RefreshGui()
    {
        throw new NotImplementedException();
    }
}

public class SetupProvePhase : IPhase
{
    BoardManager bm;
    GuiSetup guiSetup;
    public SetupProvePhase(BoardManager bm, GuiSetup guiSetup)
    {
        this.bm = bm;
        this.guiSetup = guiSetup;
    }
    
    public void EnterState()
    {
        throw new NotImplementedException();
    }

    public void ExitState()
    {
        throw new NotImplementedException();
    }

    public void Update()
    {
        throw new NotImplementedException();
    }

    public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        throw new NotImplementedException();
    }

    public void RefreshGui()
    {
        throw new NotImplementedException();
    }
}

public class MoveCommitPhase: IPhase
{
    BoardManager bm;
    GuiMovement guiMovement;
    public MoveCommitPhase(BoardManager bm, GuiMovement guiMovement)
    {
        this.bm = bm;
        this.guiMovement = guiMovement;
    }
    
    public void EnterState()
    {
        throw new NotImplementedException();
    }

    public void ExitState()
    {
        throw new NotImplementedException();
    }

    public void Update()
    {
        throw new NotImplementedException();
    }

    public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        throw new NotImplementedException();
    }

    public void RefreshGui()
    {
        throw new NotImplementedException();
    }
}

public class MoveProvePhase: IPhase
{
    BoardManager bm;
    GuiMovement guiMovement;

    public MoveProvePhase(BoardManager bm, GuiMovement guiMovement)
    {
        this.bm = bm;
        this.guiMovement = guiMovement;
    }
    
    public void EnterState()
    {
        throw new NotImplementedException();
    }

    public void ExitState()
    {
        throw new NotImplementedException();
    }

    public void Update()
    {
        throw new NotImplementedException();
    }

    public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        throw new NotImplementedException();
    }

    public void RefreshGui()
    {
        throw new NotImplementedException();
    }
}

public class RankProvePhase: IPhase
{
    BoardManager bm;
    GuiMovement guiMovement;

    public RankProvePhase(BoardManager bm, GuiMovement guiMovement)
    {
        this.bm = bm;
        this.guiMovement = guiMovement;
    }
    
    public void EnterState()
    {
        throw new NotImplementedException();
    }

    public void ExitState()
    {
        throw new NotImplementedException();
    }

    public void Update()
    {
        throw new NotImplementedException();
    }

    public void OnHover(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnClick(Vector2Int clickedPos, TileView tileView, PawnView pawnView)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkGameStateChanged(GameNetworkState networkState)
    {
        throw new NotImplementedException();
    }

    public void RefreshGui()
    {
        throw new NotImplementedException();
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
