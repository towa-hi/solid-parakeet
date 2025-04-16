using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;

public class TestBoardManager : MonoBehaviour
{
    // never mutate these externally
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public TestClickInputManager clickInputManager;
    public Vortex vortex;
    // UI stuff generally gets done in the phase
    public GuiTestGame guiTestGame;
    // internal game state. call OnStateChanged when updating these
    public Dictionary<Vector2Int, TestTileView> tileViews = new();
    public List<TestPawnView> pawnViews = new();
    // generally doesn't change
    public BoardDef boardDef;
    public Contract.LobbyParameters parameters;
    public Team userTeam;
    
    public ITestPhase currentPhase;
    public event Action<ITestPhase> OnPhaseChanged;
    public event Action<TestBoardManager> OnStateChanged;
    
    void Start()
    {
        clickInputManager.Initialize();
        clickInputManager.OnClick += OnClick;
        
    }
    
    public List<Pawn> GetMyPawns()
    {
        return pawnViews
            .Where(pv => pv.pawn.team == userTeam)
            .Select(pv => pv.pawn)
            .ToList();
    }

    public List<TestPawnView> GetMyPawnViews()
    {
        return pawnViews.Where(pv => pv.pawn.team == userTeam).ToList();
    }
    
    public Pawn GetPawnAtPos(Vector2Int pos)
    {
        TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
        return pawnView?.pawn;
    }

    public TestPawnView GetPawnViewAtPos(Vector2Int pos)
    {
        TestPawnView pawnView = pawnViews.FirstOrDefault(p => p.pawn.pos == pos);
        return pawnView;
    }

    public Tile GetTileAtPos(Vector2Int pos)
    {
        TestTileView tileView = tileViews.TryGetValue(pos, out TestTileView tile) ? tile : null;
        return tileView?.tile;
    }

    public TestTileView GetTileViewAtPos(Vector2Int pos)
    {
        TestTileView tileView = tileViews.TryGetValue(pos, out TestTileView tile) ? tile : null;
        return tileView;
    }
    
    public void StartGame(Lobby lobby, User user)
    {
        parameters = lobby.parameters;
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
            pawnView.Initialize(pawn, this);
            pawnViews.Add(pawnView);
        }

        switch (lobby.phase)
        {
            case 1:
                SetPhase(new SetupTestPhase(this, guiTestGame.setup));
                break;
            case 2:
                SetPhase(new MovementTestPhase(this, guiTestGame.movement));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
    }
    
    void SetPhase(ITestPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
        OnPhaseChanged?.Invoke(currentPhase);
    }

    public void StateChanged()
    {
        // TODO: figure out a good way to react to network changes and swap phases
        OnStateChanged?.Invoke(this);
    }
    
    void OnClick(Vector2Int pos)
    {
        TestTileView tileView = GetTileViewAtPos(pos);
        TestPawnView pawnView = GetPawnViewAtPos(pos);
        currentPhase?.OnClick(pos, tileView, pawnView);
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
    GuiTestSetup setupGui;
    
    public SetupTestPhase(TestBoardManager inBm, GuiTestSetup inSetupGui)
    {
        bm = inBm;
        setupGui = inSetupGui;
        setupGui.OnClearButton += OnClear;
        setupGui.OnAutoSetupButton += OnAutoSetup;
        setupGui.OnDeleteButton += OnDelete;
        setupGui.OnSubmitButton += OnSubmit;

    }
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
            pawnView.pawn.MutSetupRemove();
            bm.StateChanged();
            return;
        }
        Rank? selectedRank = setupGui.selectedRankEntry?.rank;
        // If no pawn at position and we have a selected rank, try to place a pawn
        if (selectedRank.HasValue)
        {
            // Find the first available pawn of the selected rank
            TestPawnView availablePawnView = bm.pawnViews.FirstOrDefault(p => 
                p.pawn.def.rank == selectedRank.Value && !p.pawn.isAlive &&p.pawn.team == bm.userTeam);
            if (availablePawnView)
            {
                availablePawnView.pawn.MutSetupAdd(clickedPos);
                bm.StateChanged();
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
    }

    void OnClear()
    {
        List<Pawn> myPawns = bm.GetMyPawns();
        foreach (Pawn p in myPawns)
        {
            p.MutSetupRemove();
        }
        bm.StateChanged();
    }

    void OnAutoSetup()
    {
        List<Pawn> myPawns = bm.GetMyPawns();
        foreach (Pawn p in myPawns)
        {
            p.MutSetupRemove();
        }
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
                Pawn pawn = myPawns.FirstOrDefault(p => 
                    p.def.rank == (Rank)maxPawns.rank && !p.isAlive);
                if (pawn != null)
                {
                    pawn.MutSetupAdd(selectedTile.pos);
                }
                else
                {
                    Debug.LogError($"No available pawns of rank {maxPawns.rank} to place");
                }
            }
        }
        bm.StateChanged();
    }

    void OnDelete()
    {
        
    }

    async void OnSubmit()
    {
        List<Pawn> myPawns = bm.GetMyPawns();
        int code = await StellarManagerTest.CommitSetupRequest(myPawns);
        bm.StateChanged();
        Debug.Log(code);
        
    }
}

public class MovementTestPhase : ITestPhase
{
    TestBoardManager bm;
    TestPawnView selectedPawnView;
    GuiTestMovement movementGui;
    
    public MovementTestPhase(TestBoardManager inBm, GuiTestMovement inMovementGui)
    {
        bm = inBm;
        movementGui = inMovementGui;
    }
    
    public void EnterState()
    {
        selectedPawnView = null;
    }

    public void ExitState()
    {
        selectedPawnView = null;
    }

    public void Update() {}

    public void OnHover() {}

    public void OnClick(Vector2Int clickedPos, TestTileView tileView, TestPawnView pawnView)
    {
        if (pawnView != null)
        {
            if (pawnView.pawn.team == bm.userTeam && !pawnView.pawn.hasMoved)
            {
                selectedPawnView = pawnView;
            }
        }
        else if (tileView != null && selectedPawnView != null)
        {
            if (IsValidMove(selectedPawnView.pawn, clickedPos))
            {
                selectedPawnView.pawn.MutMove(clickedPos);
                selectedPawnView = null;
                bm.StateChanged();
            }
        }
        else
        {
            selectedPawnView = null;
        }
    }

    bool IsValidMove(Pawn pawn, Vector2Int targetPos)
    {
        if (!bm.boardDef.IsPosValid(targetPos))
            return false;
            
        Tile targetTile = bm.boardDef.GetTileByPos(targetPos);
        if (!targetTile.isPassable)
            return false;
            
        TestPawnView existingPawn = bm.GetPawnViewAtPos(targetPos);
        if (existingPawn != null)
            return false;
        // TODO: make sure its in range
        return true;
    }
}