using System;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class Tile
{
    // NOTE: kinda a vestigial class but still needed because boardDefs are still
    // scriptableObjects that need objects rather than structs
    public Vector2Int pos;
    public bool isPassable = true;
    public Team setupTeam;
    public int autoSetupZone;
    
    public bool IsTileSetupAllowed(Team team)
    {
        return isPassable && setupTeam == team;
    }
}
