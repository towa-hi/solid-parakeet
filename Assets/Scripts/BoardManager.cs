using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// BoardManager is responsible for managing the board state, including tiles and pawns.
// It handles the setup of the board and pawns for a given player.

public class BoardManager : MonoBehaviour
{
    public Transform purgatory;
    public BoardClickInputManager boardClickInputManager;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public Grid grid;
    public GamePhase phase;
    
    // setup stuff
    
    public Player player;
    public SetupParameters setupParameters;
    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly List<PawnView> pawnViews = new();
    public Vector2Int hoveredPos;
    public event Action<Pawn> OnPawnModified; // subscribed to by all pawnviews
    
    
    // game stuff

    public ClickInputManager clickInputManager;

    public PawnView currentHoveredPawnView;
    public TileView currentHoveredTileView;
    
    
    void SetPhase(GamePhase inPhase)
    {
        GamePhase oldPhase = phase;
        switch (oldPhase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        phase = inPhase;
        switch (phase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        //OnPhaseChanged?.Invoke(oldPhase, phase);
    }
    
    public void OnDemoStartedResponse(Response<SSetupParameters> response)
    {
        setupParameters = new(response.data);
        Debug.Log("setup parameters set");
        player = response.data.player;
        Debug.Log("player set");
        Debug.Log("setupSelectedPawnDef set");
        LoadBoardData(setupParameters.board);
        LoadPawns(player);
        Debug.Log("board set");
        SetPhase(GamePhase.SETUP);
        Debug.Log("phase set");
        clickInputManager.OnPositionHovered += OnPositionHovered;
        clickInputManager.OnClick += OnClick;
        clickInputManager.Initialize();
        
    }
    
    void OnPositionHovered(Vector2Int oldPos, Vector2Int newPos)
    {
        // Store references to previous hovered pawn and tile
        PawnView previousHoveredPawnView = currentHoveredPawnView;
        TileView previousHoveredTileView = currentHoveredTileView;

        // Update current hovered pawn and tile based on new position
        if (IsPosValid(newPos))
        {
            currentHoveredPawnView = GetPawnViewFromPos(newPos);
            currentHoveredTileView = GetTileView(newPos);
        }
        else
        {
            currentHoveredPawnView = null;
            currentHoveredTileView = null;
        }

        // Check if the hovered pawn has changed
        if (previousHoveredPawnView != currentHoveredPawnView)
        {
            // Unhover the previous pawn
            if (previousHoveredPawnView != null)
            {
                previousHoveredPawnView.OnHovered(false);
            }
            // Hover the new pawn
            if (currentHoveredPawnView != null)
            {
                currentHoveredPawnView.OnHovered(true);
            }
        }

        // Check if the hovered tile has changed
        if (previousHoveredTileView != currentHoveredTileView)
        {
            // Unhover the previous tile
            if (previousHoveredTileView != null)
            {
                previousHoveredTileView.OnHovered(false);
            }
            // Hover the new tile
            if (currentHoveredTileView != null)
            {
                currentHoveredTileView.OnHovered(true);
            }
        }

        // Update the hovered position
        hoveredPos = newPos;
    }


    void OnClick(Vector2 screenPointerPosition, Vector2Int hoveredPosition)
    {
        switch (phase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                if (currentHoveredPawnView)
                {
                    if (currentHoveredPawnView.pawn.pos != hoveredPosition)
                    {
                        Debug.LogWarning("pos doesn't match hovered position");
                        return;
                    }

                    Pawn clickedPawn = currentHoveredPawnView.pawn;
                    SendPawnToPurgatory(clickedPawn);
                }
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    void LoadPawns(Player targetPlayer)
    {
        foreach ((PawnDef pawnDef, int max) in setupParameters.maxPawnsDict)
        {
            for (int i = 0; i < max; i++)
            {
                Pawn pawn = new(pawnDef, targetPlayer, true);
                GameObject pawnObject = Instantiate(pawnPrefab, transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnViews.Add(pawnView);
                pawnView.Initialize(pawn, null);
            }
        }
    }

    Pawn GetPawnFromPurgatoryByPawnDef(PawnDef pawnDef, Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        foreach (var pawnView in pawnViews)
        {
            Pawn pawn = pawnView.pawn;
            if (pawn.def == pawnDef)
            {
                if (pawn.isSetup)
                {
                    if (!pawn.isAlive)
                    {
                        pawn.SetAlive(true, pos);
                        OnPawnModified?.Invoke(pawn);
                        return pawn;
                    }
                }
            }
        }
        Debug.Log($"can't find any pawns of pawnDef {pawnDef.name}");
        return null;
    }

    void SendPawnToPurgatory(Pawn pawn)
    {
        pawn.SetAlive(false, null);
        OnPawnModified?.Invoke(pawn);
    }
    

    public PawnDef setupSelectedPawnDef;

    public void OnSetupPawnEntrySelected(PawnDef pawnDef)
    {
        setupSelectedPawnDef = pawnDef;
    }
    
    public void StartDemoGame()
    {
        //bool setupValid = IsSetupValid(player);
    }

    bool IsSetupValid(Player targetPlayer)
    {
        // Dictionary<PawnDef, int> pawnDefsOnBoard = new(setupParameters.maxPawnsDict);
        // foreach (SetupPawnView setupPawnView in setupPawnViews.Where(setupPawnView => setupPawnView.pawn.player == targetPlayer))
        // {
        //     if (!setupPawnView.pawn.isSetup)
        //     {
        //         Debug.LogError("isSetup was false");
        //         return false;
        //     }
        //     TileView tileView = GetTileView(setupPawnView.pawn.pos);
        //     if (tileView == null)
        //     {
        //         Debug.LogError("tile was null");
        //         return false;
        //     }
        //     if (!tileView.tile.IsTileEligibleForPlayer(targetPlayer))
        //     {
        //         Debug.LogError("tile was not eligible for player");
        //         return false;
        //     }
        //     if (pawnDefsOnBoard[setupPawnView.pawn.def] <= 0)
        //     {
        //         Debug.LogError("too many pawns of this type");
        //         return false;
        //     }
        //     pawnDefsOnBoard[setupPawnView.pawn.def] -= 1;
        // }
        // if (pawnDefsOnBoard.Values.Any(count => count != 0))
        // {
        //     Debug.LogError("pawns remaining");
        //     return false;
        // }
        // Debug.Log("setup valid");
        return true;
    }
    
    void LoadBoardData(BoardDef boardDef)
    {
        Debug.Log("BoardManager reading BoardDef from setupParameters");
        ClearTiles();
        for (int y = 0; y < boardDef.boardSize.y; y++)
        {
            for (int x = 0; x < boardDef.boardSize.x; x++)
            {
                // Get the position of the tile in the grid
                Vector3Int gridPosition = new(x, y, 0);
                Vector3 worldPosition = grid.CellToWorld(gridPosition);  // Convert grid position to world position
                // Spawn a tile at the grid position
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
                TileView tileView = tileObject.GetComponent<TileView>();
                Tile tile = boardDef.tiles[x + y * boardDef.boardSize.x];
                tileView.Initialize(tile, this);
                Vector2Int pos = new(x, y);
                tileViews.Add(pos, tileView);
            }
        }
    }

    public void AutoSetup(Player targetPlayer)
    {
        if (targetPlayer == Player.NONE)
        {
            throw new Exception("Player can't be none");
        }
        
        // Kill everything
        // TODO: make this not call the same event 1600 times
        foreach (var pawnView in pawnViews)
        {
            SendPawnToPurgatory(pawnView.pawn);
        }
        BoardDef boardDef = setupParameters.board;
        // Keep track of positions that have already been used
        HashSet<Vector2Int> usedPositions = new();
        // Get all eligible positions for the player
        List<Vector2Int> allEligiblePositions = tileViews.Values
            .Where(tileView => tileView.tile.IsTileEligibleForPlayer(targetPlayer))
            .Select(tileView => tileView.tile.pos)
            .ToList();
        foreach ((PawnDef pawnDef, int pawnCount) in setupParameters.maxPawnsDict)
        {
            List<Vector2Int> eligiblePositions = boardDef.GetEligiblePositionsForPawn(targetPlayer, pawnDef, usedPositions);
            // Check if there are enough eligible positions
            if (eligiblePositions.Count < pawnCount)
            {
                Debug.LogWarning($"Not enough eligible positions for pawn '{pawnDef.name}'. Ignoring placement rules for this pawn type.");
                eligiblePositions = allEligiblePositions.Except(usedPositions).ToList();
            }
            // Place the pawns randomly on the eligible positions
            for (int i = 0; i < pawnCount; i++)
            {
                if (eligiblePositions.Count == 0)
                {
                    Debug.LogWarning($"No more eligible positions available for pawn '{pawnDef.name}'.");
                    break;
                }
                int index = UnityEngine.Random.Range(0, eligiblePositions.Count);
                Vector2Int pos = eligiblePositions[index];
                eligiblePositions.RemoveAt(index);
                usedPositions.Add(pos);
                GetPawnFromPurgatoryByPawnDef(pawnDef, pos);
            }
        }
    }
    
    void ClearTiles()
    {
        ClearPawns();
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }
        tileViews.Clear();
    }
    
    void ClearPawns()
    {
        List<PawnView> tempPawnViews = new(pawnViews);
        foreach (PawnView pawnView in tempPawnViews)
        {
            DeletePawnView(pawnView);
        }
        pawnViews.Clear();
    }
    
    void DeletePawnView(PawnView pawnView)
    {
        if (pawnView == null)
        {
            return;
        }
        pawnViews.Remove(pawnView);
        Destroy(pawnView.gameObject);
    }
    
    public PawnView GetPawnViewFromPos(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == pos);
    }
    
    public PawnView GetPawnViewFromPawn(Pawn pawn)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn == pawn);
    }

    public TileView GetTileView(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return tileViews.TryGetValue(pos, out TileView tileView) ? tileView : null;
    }

    int FindPawnCount(Player targetPlayer, PawnDef targetPawnDef)
    {
        return pawnViews.Where(pawnView => pawnView.pawn.def == targetPawnDef).Count(pawnView => pawnView.pawn.player == targetPlayer);
    }

    bool IsPosValid(Vector2Int pos)
    {
        return tileViews.Keys.Contains(pos);
    }
}
