using System;
using UnityEngine;

[System.Serializable]
public class Tile
{
    // NOTE: kinda a vestigial class but still needed because boardDefs are still
    // scriptableObjects that need objects rather than structs
    public Vector2Int pos;
    public bool isPassable = true;
    public Player setupPlayer;

    public void Initialize(Vector2Int inPos)
    {
        pos = inPos;
    }

    public bool IsTileEligibleForPlayer(Player player)
    {
        return isPassable && setupPlayer == player;
    }
}

[Serializable]
public struct STile
{
    public Vector2Int pos;
    public bool isPassable;
    public int setupPlayer;
    
    public STile(Tile tile)
    {
        pos = tile.pos;
        isPassable = tile.isPassable;
        setupPlayer = (int)tile.setupPlayer;
    }
    
    public Tile ToUnity()
    {
        return new Tile
        {
            pos = pos,
            isPassable = isPassable,
            setupPlayer = (Player)setupPlayer
        };
    }
    
    public bool IsTileEligibleForPlayer(int player)
    {
        return isPassable && setupPlayer == player;
    }
}