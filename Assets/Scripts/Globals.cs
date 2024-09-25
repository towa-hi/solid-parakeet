using UnityEngine;

public static class Globals
{
    
}

public enum TileSetup
{
    NONE,
    RED,
    BLUE,
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
    RED,
    BLUE
}