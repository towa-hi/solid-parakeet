using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public BoardData boardData;
    public GameObject tilePrefab;
    Grid grid;

    Dictionary<Vector2Int, TileView> tileViews = new Dictionary<Vector2Int, TileView>();

    void Start()
    {
        grid = GetComponent<Grid>();  // Get the Grid component
        if (boardData != null && tilePrefab != null)
        {
            SpawnTiles();
        }
    }

    void SpawnTiles()
    {
        ClearBoard();  // Clear any previously spawned tiles

        for (int y = 0; y < boardData.boardSize.y; y++)
        {
            for (int x = 0; x < boardData.boardSize.x; x++)
            {
                // Get the position of the tile in the grid
                Vector3Int gridPosition = new Vector3Int(x, y, 0);
                Vector3 worldPosition = grid.CellToWorld(gridPosition);  // Convert grid position to world position

                // Spawn a tile at the grid position
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
                TileView tileView = tileObject.GetComponent<TileView>();
                
                TileData tileData = boardData.tiles[x + y * boardData.boardSize.x];
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
}
