using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[CreateAssetMenu(fileName = "Board", menuName = "Scriptable Objects/Board")]
public class BoardDef : ScriptableObject
{
    public string boardName;
    public Vector2Int boardSize;
    public Tile[] tiles;

    public void Initialize()
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
    
    public List<Vector2Int> GetEligiblePositionsForPawn(Player player, PawnDef pawnDef, HashSet<Vector2Int> usedPositions)
    {
        // Determine the number of back rows based on pawn type
        int numberOfRows = Globals.GetNumberOfRowsForPawn(pawnDef);
        if (numberOfRows > 0)
        {
            // Get eligible positions within the specified back rows
            List<Vector2Int> eligiblePositions = tiles
                .Where(tile => tile.IsTileEligibleForPlayer(player)
                                   && IsTileInBackRows(player, tile.pos, numberOfRows)
                                   && !usedPositions.Contains(tile.pos))
                .Select(tile => tile.pos)
                .ToList();
            return eligiblePositions;
        }
        else
        {
            // For other pawns, use all eligible positions for the player
            List<Vector2Int> eligiblePositions = tiles
                .Where(tile => tile.IsTileEligibleForPlayer(player)
                                   && !usedPositions.Contains(tile.pos))
                .Select(tile => tile.pos)
                .ToList();
            return eligiblePositions;
        }
    }
    
    
    bool IsTileInBackRows(Player player, Vector2Int pos, int numberOfRows)
    {
        int backRowStartY;
        if (player == Player.RED)
        {
            backRowStartY = 0;
            return pos.y >= backRowStartY && pos.y < numberOfRows;
        }
        else
        {
            backRowStartY = boardSize.y - numberOfRows;
            return pos.y >= backRowStartY && pos.y < boardSize.y;
        }
    }
}

[Serializable]
public struct SBoardDef
{
    public string boardName;
    public SVector2Int boardSize;
    public STile[] tiles;
    
    public SBoardDef(BoardDef boardDef)
    {
        boardName = boardDef.boardName;
        boardSize = new SVector2Int(boardDef.boardSize);
        tiles = new STile[boardDef.tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = new STile(boardDef.tiles[i]);
        }
    }

    public BoardDef ToUnity()
    {
        BoardDef boardDef = ScriptableObject.CreateInstance<BoardDef>();
        boardDef.boardName = boardName;
        boardDef.boardSize = this.boardSize.ToUnity();
        boardDef.tiles = this.tiles.Select(sTile => sTile.ToUnity()).ToArray();
        return boardDef;
    }
    
    public readonly List<SVector2Int> GetEligiblePositionsForPawn(int player, SPawnDef pawnDef, HashSet<SVector2Int> usedPositions)
    {
        // Determine the number of back rows based on pawn type
        int numberOfRows = Globals.GetNumberOfRowsForPawn(pawnDef);
        if (numberOfRows > 0)
        {
            // Get eligible positions within the specified back rows
            SBoardDef def = this;
            List<SVector2Int> eligiblePositions = tiles
                .Where(tile => tile.IsTileEligibleForPlayer(player)
                               && def.IsTileInBackRows(player, tile.pos, numberOfRows)
                               && !usedPositions.Contains(tile.pos))
                .Select(tile => tile.pos)
                .ToList();
            return eligiblePositions;
        }
        else
        {
            // For other pawns, use all eligible positions for the player
            List<SVector2Int> eligiblePositions = tiles
                .Where(tile => tile.IsTileEligibleForPlayer(player)
                               && !usedPositions.Contains(tile.pos))
                .Select(tile => tile.pos)
                .ToList();
            return eligiblePositions;
        }
    }
    
    readonly bool IsTileInBackRows(int player, SVector2Int pos, int numberOfRows)
    {
        int backRowStartY;
        if (player == (int)Player.RED)
        {
            backRowStartY = 0;
            return pos.y >= backRowStartY && pos.y < numberOfRows;
        }
        else
        {
            backRowStartY = boardSize.y - numberOfRows;
            return pos.y >= backRowStartY && pos.y < boardSize.y;
        }
    }

    public readonly bool IsPosValid(SVector2Int pos)
    {
        return tiles.Any(tile => tile.pos == pos);
    }

    public STile GetTileFromPos(SVector2Int pos)
    {
        foreach (STile tile in tiles)
        {
            if (tile.pos == pos)
            {
                return tile;
            }
        }
        throw new KeyNotFoundException($"tile at {pos} not found");
    }
}
