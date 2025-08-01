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
        byte[] fullHash = sha.ComputeHash(ms.ToArray());  // 32‐byte digest
        byte[] truncatedHash = new byte[16];
        Array.Copy(fullHash, truncatedHash, 16);
        return truncatedHash;

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