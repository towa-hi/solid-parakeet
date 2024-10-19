using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Globals
{
    // Static instance of GameInputActions to be shared among all Hoverable instances
    public static readonly InputSystem_Actions inputActions = new();

    public static Dictionary<string, string> pawnSprites = new Dictionary<string, string>
    {
        { "Bomb", "bomb" },
        { "Captain", "6"},
        { "Colonel", "8"},
        { "Flag", "flag"},
        { "General", "9"},
        { "Lieutenant", "5"},
        { "Major", "7"},
        { "Marshal", "10"},
        { "Miner", "m"},
        { "Scout", "s"},
        { "Sergeant", "4"},
        { "Spy", "dagger"},
    };
    
}

public enum AppState
{
    MAIN,
    GAME,
}
public enum GamePhase
{
    UNINITIALIZED,
    SETUP,
    MOVE,
    RESOLVE,
    END
}

public enum Player
{
    NONE,
    RED,
    BLUE
}


[Serializable]
public class SLobby
{
    public Guid lobbyId;
    public Guid hostId;
    public Guid guestId;
    public SBoardDef sBoardDef;
    public int gameMode;
    public bool isGameStarted;
    public string password;
    public SLobby() { }
}

[Serializable]
public class SBoardDef
{
    public SVector2Int boardSize;
    public STile[] tiles;

    public SBoardDef() { }

    public SBoardDef(BoardDef boardDef)
    {
        boardSize = new SVector2Int(boardDef.boardSize);
        tiles = new STile[boardDef.tiles.Length];
        tiles = boardDef.tiles.Select(tile => new STile(tile)).ToArray();
    }
    public BoardDef ToUnity()
    {
        BoardDef boardDef = ScriptableObject.CreateInstance<BoardDef>();
        boardDef.boardSize = this.boardSize.ToUnity();
        boardDef.tiles = this.tiles.Select(sTile => sTile.ToUnity()).ToArray();
        return boardDef;
    }
}


[Serializable]
public class SVector2Int
{
    public int x;
    public int y;
    
    public SVector2Int() { }
    
    public SVector2Int(Vector2Int vector)
    {
        x = vector.x;
        y = vector.y;
    }
    public Vector2Int ToUnity()
    {
        return new Vector2Int(this.x, this.y);
    }
}

[Serializable]
public class STile
{
    public SVector2Int pos;
    public bool isPassable;
    public int setupPlayer;
    
    public STile () { }
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
}