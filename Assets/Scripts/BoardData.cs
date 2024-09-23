using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Board", menuName = "Scriptable Objects/Board")]
public class BoardData : ScriptableObject
{
    public Vector2Int boardSize;
    public TileData[] tiles;

    public void InitializeTiles()
    {
        tiles = new TileData[boardSize.x * boardSize.y];
        
        for (int y = 0; y < boardSize.y; y++)
        {
            for (int x = 0; x < boardSize.x; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                TileData newTile = new TileData();
                newTile.Initialize(pos);
                tiles[x + y * boardSize.x] = newTile;  // Assign to the array
            }
        }
    }
    
}
