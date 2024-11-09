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
