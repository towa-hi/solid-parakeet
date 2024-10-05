using System.Collections.Generic;using UnityEngine;

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
