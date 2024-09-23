using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager instance;
    
    public Board board;
    public GameObject tilePrefab;
    Grid grid;

    readonly Dictionary<Vector2Int, TileView> tileViews = new();

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
        LoadBoardData(board);
    }

    void LoadBoardData(Board inBoard)
    {
        board = inBoard;
        SpawnTiles();
    }
    
    void SpawnTiles()
    {
        ClearBoard();  // Clear any previously spawned tiles

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
                
                TileData tileData = board.tiles[x + y * board.boardSize.x];
                tileView.Initialize(tileData);
                tileViews.Add(new Vector2Int(x, y), tileView);
            }
        }
    }
    
    void ClearBoard()
    {
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }
        tileViews.Clear();
    }

    public TileView GetTileView(Vector2Int pos)
    {
        return tileViews[pos];
    }
}
