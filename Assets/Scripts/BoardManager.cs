using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager instance;
    
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    
    Grid grid;

    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly Dictionary<Vector2Int, PawnView> pawnViews = new();
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
    }

    void Start()
    {
        grid = GetComponent<Grid>();  // Get the Grid component
    }

    public void StartBoard(GameState state)
    {
        LoadBoardData(state);
    }
    
    public void ClearBoard()
    {
        ClearPawns();
        ClearTiles();
    }
    
    void LoadBoardData(GameState state)
    {
        Debug.Log("BoardManager reading BoardDef from gameState");
        SpawnTiles(state);
    }
    
    void SpawnTiles(GameState state)
    {
        Debug.Log("BoardManager SpawnTiles()");
        BoardDef board = state.board;
        ClearTiles();  // Clear any previously spawned tiles

        for (int y = 0; y < board.boardSize.y; y++)
        {
            for (int x = 0; x < board.boardSize.x; x++)
            {
                // Get the position of the tile in the grid
                Vector3Int gridPosition = new(x, y, 0);
                Vector3 worldPosition = grid.CellToWorld(gridPosition);  // Convert grid position to world position

                // Spawn a tile at the grid position
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
                TileView tileView = tileObject.GetComponent<TileView>();
                
                Tile tile = board.tiles[x + y * board.boardSize.x];
                tileView.Initialize(tile);
                tileViews.Add(new Vector2Int(x, y), tileView);
            }
        }
    }
    public bool SpawnPawn(Pawn pawn, Vector2Int pos)
    {
        if (pawn == null)
        {
            // get pawn at pos and destroy the object
        }
        GameObject pawnObject = Instantiate(pawnPrefab);
        PawnView pawnView = pawnObject.GetComponent<PawnView>();
        pawnView.Initialize(pawn, GetTileView(pos));
        return true; //return if was successfully placed
    }
    
    void ClearTiles()
    {
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }
        tileViews.Clear();
    }

    void ClearPawns()
    {
        // clear pawns
        pawnViews.Clear();
    }

    public PawnView GetPawnView(Vector2Int pos)
    {
        return pawnViews[pos];
    }
    
    public TileView GetTileView(Vector2Int pos)
    {
        return tileViews[pos];
    }

}
