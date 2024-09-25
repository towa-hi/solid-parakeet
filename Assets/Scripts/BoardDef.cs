using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Board", menuName = "Scriptable Objects/Board")]
public class BoardDef : ScriptableObject
{
    public Vector2Int boardSize;
    public Tile[] tiles;

    public void InitializeTiles()
    {
        tiles = new Tile[boardSize.x * boardSize.y];
        
        for (int y = 0; y < boardSize.y; y++)
        {
            for (int x = 0; x < boardSize.x; x++)
            {
                Vector2Int pos = new(x, y);
                Tile tile = new();
                tile.Initialize(pos);
                tiles[x + y * boardSize.x] = tile;  // Assign to the array
            }
        }
    }
}
