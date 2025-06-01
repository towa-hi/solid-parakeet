using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;

[CreateAssetMenu(fileName = "Board", menuName = "Scriptable Objects/Board")]
public class BoardDef : ScriptableObject
{
    public string boardName;
    public Vector2Int boardSize;
    public Tile[] tiles;
    public bool isHex;
    public SMaxPawnsPerRank[] maxPawns;
    public Vector3 center;
    
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

    public byte[] GetHash()
    {
        // 1) Serialize boardSize, isHex, and each Tile’s data in a stable order
        using MemoryStream ms = new MemoryStream();
        using (BinaryWriter bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // Board size
            bw.Write(boardSize.x);
            bw.Write(boardSize.y);
            // Hex flag
            bw.Write(isHex);
            // Tiles: sort by position so every client does the same thing
            foreach (Tile tile in tiles
                         .OrderBy(t => t.pos.x)
                         .ThenBy(t => t.pos.y))
            {
                // Position
                bw.Write(tile.pos.x);
                bw.Write(tile.pos.y);
                bw.Write(tile.isPassable);
                bw.Write((int)tile.setupTeam);
                bw.Write(tile.autoSetupZone);
            }
        }
        // 2) Compute SHA‐256 over the resulting byte array
        using SHA256 sha = SHA256.Create();
        return sha.ComputeHash(ms.ToArray());  // 32‐byte digest
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