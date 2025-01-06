using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Board", menuName = "Scriptable Objects/Board")]
public class BoardDef : ScriptableObject
{
    public string boardName;
    public Vector2Int boardSize;
    public Tile[] tiles;
}

[Serializable]
public struct SBoardDef
{
    public string boardName;
    public Vector2Int boardSize;
    public STile[] tiles;
    
    public SBoardDef(BoardDef boardDef)
    {
        boardName = boardDef.boardName;
        boardSize = (Vector2Int)boardDef.boardSize;
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
        boardDef.boardSize = boardSize;
        boardDef.tiles = tiles.Select(sTile => sTile.ToUnity()).ToArray();
        return boardDef;
    }
    
    public readonly List<Vector2Int> GetEligiblePositionsForPawn(int player, SPawnDef pawnDef, HashSet<Vector2Int> usedPositions)
    {
        Debug.Log($"EligiblePos for {pawnDef.pawnName}");
        // Determine the number of back rows based on pawn type
        int numberOfRows = Globals.GetNumberOfRowsForPawn(pawnDef);
        if (numberOfRows > 0)
        {
            Debug.Log($" number of rows not zero: {numberOfRows}");
            // Get eligible positions within the specified back rows
            SBoardDef def = this;
            List<Vector2Int> eligiblePositions = tiles
                .Where(tile => tile.IsTileEligibleForPlayer(player)
                               && def.IsTileInBackRows(player, tile.pos, numberOfRows)
                               && !usedPositions.Contains(tile.pos))
                .Select(tile => tile.pos)
                .ToList();
            foreach (var thing in eligiblePositions)
            {
                Debug.Log(thing);
            }
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
    
    readonly bool IsTileInBackRows(int player, Vector2Int pos, int numberOfRows)
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

    public readonly bool IsPosValid(Vector2Int pos)
    {
        return tiles.Any(tile => tile.pos == pos);
    }

    public STile GetTileFromPos(Vector2Int pos)
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
