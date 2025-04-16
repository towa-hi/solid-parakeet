using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

[CreateAssetMenu(fileName = "Board", menuName = "Scriptable Objects/Board")]
public class BoardDef : ScriptableObject
{
    public string boardName;
    public Vector2Int boardSize;
    public Tile[] tiles;
    public bool isHex;
    public SMaxPawnsPerRank[] maxPawns;
    
    public Tile GetTileByPos(Vector2Int pos)
    {
        return Array.Find(tiles, tile => tile.pos == pos);
    }
    
    public List<Tile> GetEmptySetupTiles(Team team, Rank rank, HashSet<Tile> usedTiles)
    {
        int setupZone = Rules.GetSetupZone(rank);
        List<Tile> eligibleTiles = new();
        List<Tile> preferredTiles = new();
        foreach (Tile tile in tiles)
        {
            if (!tile.IsTileSetupAllowed(team))
            {
                continue;
            }
            if (usedTiles.Contains(tile))
            {
                continue;
            }
            eligibleTiles.Add(tile);
            if (setupZone <= tile.autoSetupZone)
            {
                preferredTiles.Add(tile);
            }
        }
        if (preferredTiles.Count != 0)
        {
            return preferredTiles;
        }
        Debug.LogWarning($"{rank} had to fallback to allTiles {setupZone}");
        return eligibleTiles;
    }
    
    public bool IsPosValid(Vector2Int pos)
    {
        return tiles.Any(tile => tile.pos == pos);
    }
}

[Serializable]
public struct SBoardDef
{
    public string boardName;
    public Vector2Int boardSize;
    public STile[] tiles;
    public bool isHex;
    
    public SBoardDef(BoardDef boardDef)
    {
        boardName = boardDef.boardName;
        boardSize = boardDef.boardSize;
        tiles = new STile[boardDef.tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = new STile(boardDef.tiles[i]);
        }
        isHex = boardDef.isHex;
    }

    public BoardDef ToUnity()
    {
        BoardDef boardDef = ScriptableObject.CreateInstance<BoardDef>();
        boardDef.boardName = boardName;
        boardDef.boardSize = boardSize;
        boardDef.tiles = tiles.Select(sTile => sTile.ToUnity()).ToArray();
        boardDef.isHex = isHex;
        return boardDef;
    }

    public readonly List<STile> GetEmptySetupTiles(int team, Rank rank, HashSet<STile> usedTiles)
    {
        int setupZone = Rules.GetSetupZone(rank);
        List<STile> eligibleTiles = new();
        List<STile> preferredTiles = new();
        foreach (STile tile in tiles)
        {
            if (!tile.IsTileSetupAllowed(team))
            {
                continue;
            }
            if (usedTiles.Contains(tile))
            {
                continue;
            }
            eligibleTiles.Add(tile);
            if (setupZone <= tile.autoSetupZone)
            {
                preferredTiles.Add(tile);
            }
        }
        if (preferredTiles.Count != 0)
        {
            return preferredTiles;
        }
        Debug.LogWarning($"{rank} had to fallback to allTiles {setupZone}");
        return eligibleTiles;
    }
    
    public readonly bool IsPosValid(Vector2Int pos)
    {
        return tiles.Any(tile => tile.pos == pos);
    }

    public readonly STile GetTileByPos(Vector2Int pos)
    {
        return Array.Find(tiles, tile => tile.pos == pos);
    }
}

[Serializable]
public struct SMaxPawnsPerRank
{
    public Rank rank;
    public int max;

    public SMaxPawnsPerRank(Rank inRank, int inMax)
    {
        rank = inRank;
        max = inMax;
    }
}