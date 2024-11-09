using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// BoardManager is responsible for managing the board state, including tiles and pawns.
// It handles the setup of the board and pawns for a given player.

public class BoardManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public Grid grid;

    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly List<PawnView> pawnViews = new();
    
    public Player player;
    public SetupParameters setupParameters;

    public event Action<PawnView> OnPawnAdded; 
    public event Action<PawnView> OnPawnRemoved;
    
    public void StartBoardSetup(Player inPlayer, SetupParameters inSetupParameters)
    {
        setupParameters = inSetupParameters;
        if (inPlayer == Player.NONE)
        {
            throw new Exception("BoardManager cannot have player == Player.NONE");
        }
        player = inPlayer;
        LoadBoardData(setupParameters.board);
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
                tileViews.Add(new(x, y), tileView);
            }
        }
    }

    public void AutoSetup()
    {
        ClearPawns();
        BoardDef boardDef = setupParameters.board;
        // Keep track of positions that have already been used
        HashSet<Vector2Int> usedPositions = new();
        // Get all eligible positions for the player
        List<Vector2Int> allEligiblePositions = tileViews.Values
            .Where(tileView => tileView.tile.IsTileEligibleForPlayer(player))
            .Select(tileView => tileView.tile.pos)
            .ToList();
        foreach ((PawnDef pawnDef, int pawnCount) in setupParameters.maxPawnsList)
        {
            List<Vector2Int> eligiblePositions = boardDef.GetEligiblePositionsForPawn(player, pawnDef, usedPositions);
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
                Pawn pawn = new(pawnDef, player, pos);
                AddPawn(pawn);
            }
        }
    }
    
    void AddPawn(Pawn pawn)
    {
        GameObject pawnObject = Instantiate(pawnPrefab, transform);
        PawnView pawnView = pawnObject.GetComponent<PawnView>();
        pawnView.Initialize(pawn, GetTileView(pawn.pos));
        pawnViews.Add(pawnView);
        OnPawnAdded?.Invoke(pawnView);
    }

    void DeletePawn(Pawn pawn)
    {
        PawnView pawnView = GetPawnViewFromPawn(pawn);
        if (!pawnView) return;
        Destroy(pawnView.gameObject);
        pawnViews.Remove(pawnView);
        OnPawnRemoved?.Invoke(pawnView);
    }

    void ClearTiles()
    {
        // this also clears all pawns
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
            PawnView pawnView = GetPawnViewAtPos(tileView.tile.pos);
            if (pawnView)
            {
                DeletePawn(pawnView.pawn);
            }
        }
        tileViews.Clear();
    }

    void ClearPawns()
    {
        foreach (PawnView pawnView in pawnViews)
        {
            Destroy(pawnView.gameObject);
            OnPawnRemoved?.Invoke(pawnView);
        }
        pawnViews.Clear();
    }

    public PawnView GetPawnViewAtPos(Vector2Int pos)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == pos);
    }

    public PawnView GetPawnViewFromPawn(Pawn pawn)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn == pawn);
    }

    public TileView GetTileView(Vector2Int pos)
    {
        if (tileViews.TryGetValue(pos, out TileView tileView))
        {
            return tileView;
        }
        else
        {
            Debug.LogError($"TileView not found at position {pos}");
            return null;
        }
    }

}
