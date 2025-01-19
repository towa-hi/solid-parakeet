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
    
    public void EditorInitialize(Vector2Int inPos, bool inIsPassable, Team inSetupTeam)
    {
        pos = inPos;
        isPassable = inIsPassable;
        setupTeam = inSetupTeam;
    }

    public bool IsTileEligibleForPlayer(Team team)
    {
        return isPassable && setupTeam == team;
    }
}

[Serializable]
public struct STile
{
    public Vector2Int pos;
    public bool isPassable;
    public int setupTeam;
    public int autoSetupZone;
    
    public STile(Tile tile)
    {
        pos = tile.pos;
        isPassable = tile.isPassable;
        setupTeam = (int)tile.setupTeam;
        autoSetupZone = tile.autoSetupZone;
    }
    
    public Tile ToUnity()
    {
        return new Tile
        {
            pos = pos,
            isPassable = isPassable,
            setupTeam = (Team)setupTeam,
            autoSetupZone = autoSetupZone,
        };
    }
    
    public bool IsTileSetupAllowed(int team)
    {
        return isPassable && setupTeam == team;
    }
}