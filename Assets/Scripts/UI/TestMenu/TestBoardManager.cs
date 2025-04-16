using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;

public class TestBoardManager : MonoBehaviour
{
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public TestClickInputManager clickInputManager;
    public Vortex vortex;
    public GuiTestGame guiTestGame;
    
    public Dictionary<Vector2Int, TestTileView> tileViews = new();
    public List<TestPawnView> pawnViews = new();

    public Transform waveStartPositionOne;
    public Transform waveStartPositionTwo;
    public float waveSpeed;
    public BoardDef boardDef;
    public ITestPhase currentPhase;
    
    //stuff that never changes while ingame
    public LobbyParameters parameters;
    public Team userTeam;
    
    public event Action<ITestPhase> OnPhaseChanged;
    public event Action<TestBoardManager> OnStateChanged;
    public event Action<Pawn> OnPawnChanged;
    
    public Lobby GetLobby()
    {
        Lobby? maybeLobby = StellarManagerTest.currentLobby;
        if (maybeLobby.HasValue)
        {
            return maybeLobby.Value;
        }
        else
        {
            throw new NullReferenceException();
        }
    }

    public User GetUser()
    {
        User? maybeUser = StellarManagerTest.currentUser;
        if (maybeUser.HasValue)
        {
            return maybeUser.Value;
        }
        else
        {
            throw new NullReferenceException();
        }
    }

    public UserState GetUserState()
    {
        Lobby lobby = GetLobby();
        User user = GetUser();
        if (lobby.guest_address == user.index)
        {
            return lobby.guest_state;
        }
        else if (lobby.host_address == user.index)
        {
            return lobby.host_state;
        }
        throw new NullReferenceException();
    }
    void Start()
    {
        clickInputManager.Initialize();
        clickInputManager.OnClick += OnClick;
        
    }

    public void UpdateAllPawnVisuals()
    {
        foreach (var pawnView in pawnViews)
        {
            pawnView.Apply();
        }
    }
    
    public Pawn GetPawnAtPosition(Vector2Int pos)
    {
        TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
        return pawnView?.pawn;
    }
    
    public void StartGame()
    {
        Lobby lobby = GetLobby();
        User user = GetUser();
        if (user.index == lobby.host_address)
        {
            userTeam = (Team)lobby.host_state.team;
        }
        else if (user.index == lobby.guest_address)
        {
            userTeam = (Team)lobby.guest_state.team;
        }
        else
        {
            throw new Exception("User is not a part of this lobby");
        }
        BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
        foreach (var board in boardDefs)
        {
            if (board.name != lobby.parameters.board_def_name) continue;
            boardDef = board;
            break;
        }
        grid.SetBoard(new SBoardDef(boardDef));
        foreach (Tile tile in boardDef.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TestTileView tileView = tileObject.GetComponent<TestTileView>();
            tileView.Initialize(tile,boardDef.isHex);
            tileViews.Add(tile.pos, tileView);
        }

        // Clear any existing pawn views
        foreach (var pawnView in pawnViews)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();

        // Instantiate pawn views for each pawn in the lobby, preserving their server state
        foreach (Contract.Pawn p in lobby.pawns)
        {
            Pawn pawn = new Pawn(p);  // This will preserve the state from the server
            GameObject pawnObject = Instantiate(pawnPrefab, transform);
            TestPawnView pawnView = pawnObject.GetComponent<TestPawnView>();
            pawnView.Initialize(pawn);
            pawnViews.Add(pawnView);
        }

        // Update all pawn visuals to match their server state
        UpdateAllPawnVisuals();

        SetPhase(new SetupTestPhase(this));
    }
    
    void SetPhase(ITestPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
        OnPhaseChanged?.Invoke(currentPhase);
        
        // Subscribe to GUI events when entering setup phase
        if (currentPhase is SetupTestPhase setupPhase)
        {
            guiTestGame.setup.OnRankSelected += OnRankSelected;
        }
        else
        {
            guiTestGame.setup.OnRankSelected -= OnRankSelected;
        }
    }

    public void StateChanged()
    {
        
        UpdateAllPawnVisuals();
    }
    
    void OnRankSelected(Rank rank)
    {
        if (currentPhase is SetupTestPhase setupPhase)
        {
            setupPhase.selectedRank = rank;
        }
        OnStateChanged?.Invoke(this);
    }
    
    void OnClick(Vector2Int pos)
    {
        TestTileView tileView = tileViews.TryGetValue(pos, out TestTileView tile) ? tile : null;
        TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
        currentPhase?.OnClick(pos, tileView, pawnView);
        // Update all visuals after state changes are complete
        //UpdateAllPawnVisuals();
    }
}

public interface ITestPhase
{
    public void EnterState();
    public void ExitState();
    public void Update();
    public void OnHover();
    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView);
}

public class SetupTestPhase : ITestPhase
{
    TestBoardManager bm;
    
    public SetupTestPhase(TestBoardManager boardManager)
    {
        bm = boardManager;
    }
    
    public Rank? selectedRank = null;
    
    public void EnterState()
    {
        
    }

    public void ExitState()
    {
        
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
        
        // If there's a pawn at the clicked position, remove it
        if (pawnView)
        {
            pawnView.pawn.isAlive = false;
            pawnView.pawn.pos = Globals.Purgatory;
            return;
        }
        
        // If no pawn at position and we have a selected rank, try to place a pawn
        if (selectedRank.HasValue)
        {
            // Find the first available pawn of the selected rank
            TestPawnView availablePawnView = bm.pawnViews.FirstOrDefault(p => 
                p.pawn.def.rank == selectedRank.Value && !p.pawn.isAlive &&p.pawn.team == bm.userTeam);
            if (availablePawnView)
            {
                availablePawnView.pawn.MutSetupAdd(clickedPos);
            }
            else
            {
                Debug.LogWarning($"No available pawns of rank {selectedRank.Value} to place");
            }
        }
        else
        {
            Debug.LogWarning("No rank selected");
            // do nothing
        }
        bm.StateChanged();
    }

    public void OnAutoSetup()
    {
        // Get the lobby parameters
        Lobby lobby = bm.GetLobby();

        // Generate valid setup positions for each pawn
        HashSet<Tile> usedTiles = new();
        foreach (MaxPawns maxPawns in lobby.parameters.max_pawns)
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
                TestPawnView pawnView = bm.pawnViews.FirstOrDefault(p => 
                    p.pawn.def.rank == (Rank)maxPawns.rank && !p.pawn.isAlive);

                if (pawnView != null)
                {
                    pawnView.pawn.MutSetupAdd(selectedTile.pos);
                }
                else
                {
                    Debug.LogError($"No available pawns of rank {maxPawns.rank} to place");
                }
            }
        }
        bm.StateChanged();
    }
}