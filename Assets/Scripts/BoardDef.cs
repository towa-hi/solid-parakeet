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
    public bool isHex;
    public SMaxPawnsPerRank[] maxPawns;

    public void EditorInitialize()
    {
        boardSize = new Vector2Int(10, 10);
        tiles = new Tile[boardSize.x * boardSize.y];
        int index = 0;
        for (int y = 0; y < boardSize.y; y++)
        {
            for (int x = 0; x < boardSize.x; x++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                Tile tile = new Tile();
                tiles[index] = tile;
                tile.EditorInitialize(currentPos, true, Team.NONE);
                index++;
            }
        }
    }

    public void EditorInitializeMaxPawns()
    {
        maxPawns = new[]
        {
            new SMaxPawnsPerRank(Rank.THRONE, 1),
            new SMaxPawnsPerRank(Rank.ASSASSIN, 1),
            new SMaxPawnsPerRank(Rank.SCOUT, 8),
            new SMaxPawnsPerRank(Rank.SEER, 5),
            new SMaxPawnsPerRank(Rank.GRUNT, 4),
            new SMaxPawnsPerRank(Rank.KNIGHT, 4),
            new SMaxPawnsPerRank(Rank.WRAITH, 4),
            new SMaxPawnsPerRank(Rank.REAVER, 3),
            new SMaxPawnsPerRank(Rank.HERALD, 2),
            new SMaxPawnsPerRank(Rank.CHAMPION, 1),
            new SMaxPawnsPerRank(Rank.WARLORD, 1),
            new SMaxPawnsPerRank(Rank.TRAP, 6),
            new SMaxPawnsPerRank(Rank.UNKNOWN, 0),
        };
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
        boardSize = (Vector2Int)boardDef.boardSize;
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

    public readonly List<STile> GetEligibleTilesForPawnSetup(int player, Rank rank, HashSet<STile> usedTiles)
    {
        int setupZone = Rules.GetSetupZone(rank);
        List<STile> eligibleTiles = new();
        List<STile> preferredTiles = new();
        foreach (STile tile in tiles)
        {
            if (!tile.IsTileSetupAllowed(player))
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
        Debug.LogWarning($"GetEligibleTilesForPawnSetup  {rank} had to fallback to allTiles {setupZone}");
        return eligibleTiles;
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