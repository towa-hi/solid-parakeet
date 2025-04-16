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

    public UserState userState;

    public Dictionary<Vector2Int, TestTileView> tileViews = new();
    public List<TestPawnView> pawnViews = new();

    public Transform waveStartPositionOne;
    public Transform waveStartPositionTwo;
    public float waveSpeed;
    public BoardDef boardDef;
    public ITestPhase currentPhase;
    
    public event Action<ITestPhase> OnPhaseChanged;
    public event Action<PawnDef> OnSetupStateChanged;
    public event Action<Pawn> OnPawnChanged;
    
    Lobby GetLobby()
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
    
    void OnRankSelected(Rank rank)
    {
        if (currentPhase is SetupTestPhase setupPhase)
        {
            setupPhase.selectedRank = rank;
        }
    }
    
    void OnClick(Vector2Int pos)
    {
        TestTileView tileView = tileViews.TryGetValue(pos, out TestTileView tile) ? tile : null;
        TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
        currentPhase?.OnClick(pos, tileView, pawnView);
        // Update all visuals after state changes are complete
        UpdateAllPawnVisuals();
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
            TestPawnView availablePawn = bm.pawnViews.FirstOrDefault(p => 
                p.pawn.def.rank == selectedRank.Value && !p.pawn.isAlive);
                
            if (availablePawn)
            {
                availablePawn.pawn.isAlive = true;
                availablePawn.pawn.pos = clickedPos;
            }
            else
            {
                Debug.LogError($"No available pawns of rank {selectedRank.Value} to place");
            }
        }
        else
        {
            Debug.LogWarning("No rank selected");
            // do nothing
        }
    }
}