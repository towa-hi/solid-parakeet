using System;
using UnityEngine;

[System.Serializable]
public class Tile
{
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
    public SVector2Int pos;
    public bool isPassable;
    public int setupPlayer;
    
    public STile(Tile tile)
    {
        pos = new SVector2Int(tile.pos);
        isPassable = tile.isPassable;
        setupPlayer = (int)tile.setupPlayer;
    }
    public Tile ToUnity()
    {
        return new Tile
        {
            pos = this.pos.ToUnity(),
            isPassable = this.isPassable,
            setupPlayer = (Player)this.setupPlayer
        };
    }
    
    public bool IsTileEligibleForPlayer(int player)
    {
        return isPassable && setupPlayer == player;
    }
}