using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class Globals
{
    public static Vector2Int PURGATORY = new(-666, -666);
    public static bool SETUPMUSTPLACEALLPAWNS = false;
    public static float PAWNMOVEDURATION = 0.5f;
    // Static instance of GameInputActions to be shared among all Hoverable instances
    public static readonly InputSystem_Actions inputActions = new();
    

    public static bool IsNicknameValid(string nickname)
    {
        // Check length constraints
        if (string.IsNullOrEmpty(nickname) || nickname.Length >= 16)
            return false;

        // Check for alphanumeric characters and spaces only
        return Regex.IsMatch(nickname, @"^[a-zA-Z0-9 ]+$");
    }

    public static bool IsPasswordValid(string password)
    {
        return true;
    }
    
    public static Guid LoadOrGenerateClientId()
    {
        if (PlayerPrefs.HasKey("ClientId"))
        {
            string clientIdStr = PlayerPrefs.GetString("ClientId");
            if (Guid.TryParse(clientIdStr, out Guid parsedId))
            {
                Debug.Log($"Loaded existing ClientId: {parsedId}");
                return parsedId;
            }
            else
            {
                Guid newId = Guid.NewGuid();
                PlayerPrefs.SetString("ClientId", newId.ToString());
                PlayerPrefs.Save();
                Debug.Log($"Generated new ClientId (invalid stored ID was replaced): {newId}");
                return newId;
            }
        }
        else
        {
            Guid newId = Guid.NewGuid();
            PlayerPrefs.SetString("ClientId", newId.ToString());
            PlayerPrefs.Save();
            Debug.Log($"Generated new ClientId: {newId}");
            return newId;
        }
    }
    
    public static string GetNickname()
    {
        string nick = PlayerPrefs.GetString("nickname");
        if (nick == String.Empty)
        {
            PlayerPrefs.SetString("nickname", "defaultnick");
        }
        return PlayerPrefs.GetString("nickname");
    }
    
    public static List<KeyValuePair<PawnDef, int>> GetOrderedPawnList()
    {
        // Return the pawn entries in the specific order
        // You can adjust the order here as needed
        List<KeyValuePair<PawnDef, int>> orderedPawns = new List<KeyValuePair<PawnDef, int>>();

        // Assuming you have variables for each PawnDef as in SetupParameters
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Flag"), 1));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Spy"), 1));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Bomb"), 6));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Marshal"), 1));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/General"), 1));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Colonel"), 2));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Major"), 3));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Captain"), 4));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Lieutenant"), 4));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Sergeant"), 4));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Miner"), 5));
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Scout"), 8));
        // we add unknown because this list is used by the frontend
        orderedPawns.Add(new KeyValuePair<PawnDef, int>(Resources.Load<PawnDef>("Pawn/Unknown"), 0));
        return orderedPawns;
    }
    
    public static SSetupPawnData[] GetMaxPawnsArray()
    {
        SSetupPawnData[] maxPawnsArray = new SSetupPawnData[]
        {
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Flag")), maxPawns = 1 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Spy")), maxPawns = 1 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Bomb")), maxPawns = 6 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Marshal")), maxPawns = 1 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/General")), maxPawns = 1 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Colonel")), maxPawns = 2 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Major")), maxPawns = 3 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Captain")), maxPawns = 4 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Lieutenant")), maxPawns = 4 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Sergeant")), maxPawns = 4 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Miner")), maxPawns = 5 },
            new SSetupPawnData { pawnDef = new SPawnDef(Resources.Load<PawnDef>("Pawn/Scout")), maxPawns = 8 },
            // unknown is not in here
        };
        return maxPawnsArray;
    }
    
    public static float EaseOutQuad(float t)
    {
        return t * (2 - t);
    }
    
    public static int GetNumberOfRowsForPawn(PawnDef pawnDef)
    {
        switch (pawnDef.name)
        {
            case "Flag":
                // Rule 1: Flag goes in the back row
                return 1;
            case "Spy":
                // Rule 2: Spy goes somewhere in the two furthest back rows
                return 2;
            case "Bomb":
            case "Marshal":
            case "General":
                // Rule 3 & 4: Bombs, Marshal, General in three furthest back rows
                return 3;
            default:
                // Other pawns have no specific back row requirement
                return 0;
        }
    }
    
    public static int GetNumberOfRowsForPawn(SPawnDef pawnDef)
    {
        switch (pawnDef.pawnName)
        {
            case "Flag":
                // Rule 1: Flag goes in the back row
                return 1;
            case "Spy":
                // Rule 2: Spy goes somewhere in the two furthest back rows
                return 2;
            case "Bomb":
            case "Marshal":
            case "General":
                // Rule 3 & 4: Bombs, Marshal, General in three furthest back rows
                return 3;
            default:
                // Other pawns have no specific back row requirement
                return 0;
        }
    }
    
    public static string ShortGuid(Guid guid)
    {
        return guid.ToString().Substring(0, 4);
    }
}

public enum MessageGenre : uint
{
    SERVERERROR, // only called when error is server fault, disconnects the client forcibly
    REGISTERCLIENT, // request: clientId only, response: none 
    REGISTERNICKNAME, // request: nickname, response: success
    GAMELOBBY, // request: lobby parameters, response: lobby
    LEAVEGAMELOBBY, // request: clientId only, response: leave notification
    JOINGAMELOBBY, // request: password, response: lobby 
    READYLOBBY,
    GAMESTART,
    GAMESETUP, // request holds piece deployment or move data, response is a gamestate object
    SETUPFINISHED,
    MOVE,
    RESOLVE,
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
    public bool hostReady;
    public bool guestReady;
    public SLobby() { }

    public bool IsHost(Guid clientId)
    {
        return clientId == hostId;
    }
}



// [Serializable]
// public struct SVector2Int : IEquatable<SVector2Int>
// {
//     public int x;
//     public int y;
//     
//     public override bool Equals(object obj)
//     {
//         return obj is SVector2Int other && Equals(other);
//     }
//
//     public static explicit operator Vector2Int(SVector2Int sVector2Int)
//     {
//         Debug.LogWarning("Casting to Vector2Int should be done with ToUnity() mainly for logging purposes");
//         return new Vector2Int(sVector2Int.x, sVector2Int.y);
//     }
//
//     public static explicit operator SVector2Int(Vector2Int vector2Int)
//     {
//         return new SVector2Int(vector2Int.x, vector2Int.y);
//     }
//     
//     public SVector2Int(int inX, int inY)
//     {
//         x = inX;
//         y = inY;
//     }
//     
//     public readonly Vector2Int ToUnity()
//     {
//         return new Vector2Int(x, y);
//     }
//     
//     // Implement IEquatable for performance
//     public bool Equals(SVector2Int other)
//     {
//         return x == other.x && y == other.y;
//     }
//
//     // Override GetHashCode
//     public override int GetHashCode()
//     {
//         return HashCode.Combine(x, y);
//     }
//
//     // Define the == operator
//     public static bool operator ==(SVector2Int left, SVector2Int right)
//     {
//         return left.Equals(right);
//     }
//
//     // Define the != operator
//     public static bool operator !=(SVector2Int left, SVector2Int right)
//     {
//         return !(left == right);
//     }
//
//     public override string ToString()
//     {
//         return $"({x}, {y})";
//     }
// }

public struct SSetupParameters
{
    public int player;
    public SBoardDef board;
    public SSetupPawnData[] maxPawnsDict;
    

    public static bool IsSetupValid(int targetPlayer, SSetupParameters setupParameters, SSetupPawn[] setupPawns)
    {
        // Convert the SSetupPawnData array to a dictionary for easier lookup
        Dictionary<SPawnDef, int> pawnCounts = new Dictionary<SPawnDef, int>();
        foreach (var pawnData in setupParameters.maxPawnsDict)
        {
            pawnCounts[pawnData.pawnDef] = pawnData.maxPawns;
        }
        // Iterate over the provided pawns
        foreach (SSetupPawn setupPawn in setupPawns)
        {
            // Check if the pawnDef is in the max pawns dictionary
            if (!pawnCounts.ContainsKey(setupPawn.def))
            {
                Debug.LogError($"PawnDef '{setupPawn.def.pawnName}' not found in max pawns data.");
                return false;
            }
            pawnCounts[setupPawn.def] -= 1;
            // If count goes negative, there are too many pawns of this type
            if (pawnCounts[setupPawn.def] < 0)
            {
                Debug.LogError($"Too many pawns of type '{setupPawn.def.pawnName}'.");
                return false;
            }
            // Check if the pawn is on a valid tile
            if (!setupParameters.board.IsPosValid(setupPawn.pos))
            {
                Debug.LogError($"Pawn '{setupPawn.def.pawnName}' is on an invalid position {setupPawn.pos}.");
                return false;
            }
        }
        if (!Globals.SETUPMUSTPLACEALLPAWNS)
        {
            return setupPawns.Any(pawn => pawn.def.pawnName == "Flag");
        }
        else
        {
            // Check if there are any remaining pawns that haven't been placed
            foreach (var kvp in pawnCounts)
            {
                if (kvp.Value > 0)
                {
                    Debug.LogError($"Not all pawns of type '{kvp.Key.pawnName}' have been placed. {kvp.Value} remaining.");
                    return false;
                }
            }
            Debug.Log("Setup is valid.");
            return true;
        }
    }
    
}

public class SSetupPawnData
{
    public SPawnDef pawnDef;
    public int maxPawns;
}

public interface IGameClient
{
    // Events
    event Action<Response<string>> OnRegisterClientResponse;
    event Action<Response<string>> OnDisconnect;
    event Action<ResponseBase> OnErrorResponse;
    event Action<Response<string>> OnRegisterNicknameResponse;
    event Action<Response<SLobby>> OnGameLobbyResponse;
    event Action<Response<string>> OnLeaveGameLobbyResponse;
    event Action<Response<string>> OnJoinGameLobbyResponse;
    event Action<Response<SLobby>> OnReadyLobbyResponse;
    event Action<Response<SSetupParameters>> OnDemoStartedResponse;
    event Action<Response<bool>> OnSetupSubmittedResponse;
    event Action<Response<SGameState>> OnSetupFinishedResponse;
    event Action<Response<bool>> OnMoveResponse;
    event Action<Response<SResolveReceipt>> OnResolveResponse;
    
    
    // Methods
    void ConnectToServer();
    void SendRegisterNickname(string nicknameInput);
    void SendGameLobby();
    void SendGameLobbyLeaveRequest();
    void SendGameLobbyReadyRequest(bool ready);
    void SendStartGameDemoRequest();
    void SendSetupSubmissionRequest(SSetupPawn[] setupPawnList);
    void SendMove(SQueuedMove move);
}


public class ResponseBase
{
    public Guid requestId;
    public bool success;
    public int responseCode;
    public string message;
}

public class Response<T> : ResponseBase
{
    public T data;
    
    public Response() { }
    
    public Response(bool inSuccess, int inResponseCode, T inData)
    {
        success = inSuccess;
        responseCode = inResponseCode;
        data = inData;
    }

}

public class RequestBase
{
    public Guid requestId;
    public Guid clientId;
}

public class RegisterClientRequest : RequestBase
{
}

public class RegisterNicknameRequest : RequestBase
{
    public string nickname { get; set; }
}

public class GameLobbyRequest : RequestBase
{
    public int gameMode { get; set; }
    public SBoardDef sBoardDef { get; set; }
}

public class LeaveGameLobbyRequest : RequestBase
{
    
}

public class JoinGameLobbyRequest : RequestBase
{
    
}

public class ReadyGameLobbyRequest : RequestBase
{
    public bool ready { set; get; }
}

public class StartGameRequest : RequestBase
{
    public SSetupParameters setupParameters;
}

public class SetupRequest : RequestBase
{
    public int player;
    public SSetupPawn[] setupPawns;
}

public class MoveRequest : RequestBase
{
    public SQueuedMove move;
}


[Serializable]
public struct SQueuedMove
{
    public int player;
    public Guid pawnId;
    public Vector2Int initialPos;
    public Vector2Int pos;

    public SQueuedMove(in SPawn pawn, in Vector2Int inPos)
    {
        player = pawn.player;
        pawnId = pawn.pawnId;
        initialPos = pawn.pos;
        pos = inPos;
    }

    public SQueuedMove(Pawn pawn, Vector2Int inPos)
    {
        player = (int)pawn.player;
        pawnId = pawn.pawnId;
        initialPos = pawn.pos;
        pos = inPos;
    }
    
    public SQueuedMove(int inPlayer, Guid inPawnId, Vector2Int inInitialPos, Vector2Int inPos)
    {
        player = inPlayer;
        pawnId = inPawnId;
        initialPos = inInitialPos;
        pos = inPos;
    }
    
}


[Serializable]
public struct SGameState
{
    public int winnerPlayer;
    public int player;
    public SBoardDef boardDef;
    public SPawn[] pawns;
    
    public SGameState(int inPlayer, SBoardDef inBoardDef, SPawn[] inPawns)
    {
        winnerPlayer = (int)Player.NONE;
        player = inPlayer;
        boardDef = inBoardDef;
        pawns = inPawns;
    }
    
    public readonly STile[] GetMovableTiles(in SPawn pawn)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0),   // Right
            new Vector2Int(-1, 0),  // Left
            new Vector2Int(0, 1),   // Up
            new Vector2Int(0, -1)   // Down
        };
        // Define the maximum possible number of movable tiles
        int maxMovableTiles = boardDef.tiles.Length; // Adjust based on your board size
        STile[] movableTiles = new STile[maxMovableTiles];
        int tileCount = 0;

        if (pawn.def.pawnName == "Unknown")
        {
            throw new Exception("GetMovableTiles requires a pawnDef");
        }

        // Determine pawn movement range
        int pawnMovementRange = pawn.def.pawnName switch
        {
            "Scout" => 11,
            "Bomb" or "Flag" => 0,
            _ => 1,
        };
        foreach (Vector2Int dir in directions)
        {
            Vector2Int currentPos = pawn.pos;
            bool enemyEncountered = false;

            for (int i = 0; i < pawnMovementRange; i++)
            {
                currentPos += dir;
                if (enemyEncountered)
                {
                    break;
                }
                // Check if the position is within the board bounds
                if (!IsPosValid(currentPos))
                {
                    break;
                }
                // Get the tile at the current position
                STile tile = GetTileByPos(currentPos);
                // check if tile is passable
                if (!tile.isPassable)
                {
                    break;
                }
                // Check if the tile is occupied by another pawn
                SPawn? pawnOnPos = GetPawnByPos(currentPos);
                if (pawnOnPos.HasValue)
                {
                    if (pawnOnPos.Value.player == pawn.player)
                    {
                        // Cannot move through own pawns
                        break;
                    }
                    else
                    {
                        // Tile is occupied by an enemy pawn
                        movableTiles[tileCount++] = tile;

                        if (pawnMovementRange > 1)
                        {
                            enemyEncountered = true;
                        }
                        else
                        {
                            break; // Non-scouts cannot move further
                        }
                    }
                }
                else
                {
                    // Tile is unoccupied
                    movableTiles[tileCount++] = tile;
                }
            }
        }
        // Create a final array of the correct size
        STile[] result = new STile[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            result[i] = movableTiles[i];
        }
        return result;
    }

    public static bool IsMoveValid(in SGameState gameState, in SQueuedMove move)
    {
        if (gameState.boardDef.IsPosValid(move.pos))
        {
            STile destinationTile = gameState.GetTileByPos(move.pos);
            if (!destinationTile.isPassable)
            {
                // move is to impassable tile
                return false;
            }
        }
        else
        {
            return false;
        }
        try
        {
            SPawn movingPawn = gameState.GetPawnById(move.pawnId);
            if (move.player != movingPawn.player)
            {
                return false;
            }
            var maybePawn = gameState.GetPawnByPos(move.pos);
            if (maybePawn.HasValue)
            {
                SPawn obstructingPawn = maybePawn.Value;
                if (obstructingPawn.player == move.player)
                {
                    return false;
                }
            }
            bool isInMovableTiles = false;
            foreach (STile tile in gameState.GetMovableTiles(movingPawn))
            {
                if (tile.pos == move.pos)
                {
                    isInMovableTiles = true;
                }
            }
            if (!isInMovableTiles)
            {
                return false;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        return true;
    }
    
    public readonly bool IsPosValid(Vector2Int pos)
    {
        return boardDef.tiles.Any(tile => tile.pos == pos);

    }

    public static SGameState Censor(in SGameState masterGameState, int targetPlayer)
    {
        bool cheatMode = false;
        if (masterGameState.player != (int)Player.NONE)
        {
            throw new Exception("Censor can only be done on master game states!");
        }
        SGameState censoredGameState = new SGameState
        {
            winnerPlayer = masterGameState.winnerPlayer,
            player = targetPlayer,
            boardDef = masterGameState.boardDef,
        };
        SPawn[] censoredPawns = new SPawn[masterGameState.pawns.Length];
        for (int i = 0; i < masterGameState.pawns.Length; i++)
        {
            SPawn serverPawn = masterGameState.pawns[i];
            SPawn censoredPawn;
            if (serverPawn.player != targetPlayer)
            {
                if (cheatMode)
                {
                    censoredPawn = serverPawn;
                    censoredPawn.isVisibleToOpponent = true;
                }
                else
                {
                    if (serverPawn.isVisibleToOpponent)
                    {
                        censoredPawn = serverPawn;
                    }
                    else
                    {
                        censoredPawn = serverPawn.Censor();
                    }
                }
            }
            else
            {
                censoredPawn = serverPawn;
            }
            censoredPawns[i] = censoredPawn;
        }
        censoredGameState.pawns = censoredPawns;
        return censoredGameState;
    }
    
    public static SQueuedMove? GenerateValidMove(in SGameState gameState, int targetPlayer)
    {
        // NOTE: gameState should be censored if it isn't already for fairness
        List<SQueuedMove> allPossibleMoves = new List<SQueuedMove>();
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.player == targetPlayer)
            {
                STile[] movableTiles = gameState.GetMovableTiles(pawn);
                foreach (var tile in movableTiles)
                {
                    allPossibleMoves.Add(new SQueuedMove(targetPlayer, pawn.pawnId, pawn.pos, tile.pos));
                }
            }
        }
        if (allPossibleMoves.Count == 0)
        {
            return null;
        }
        System.Random random = new();
        int randomIndex = random.Next(0, allPossibleMoves.Count);
        SQueuedMove randomMove = allPossibleMoves[randomIndex];
        SPawn randomPawn = gameState.GetPawnById(randomMove.pawnId);
        Debug.Log($"GenerateValidMove chose {randomMove.pawnId} {randomPawn.def.pawnName} from {randomPawn.pos} to {randomMove.pos}");
        if (!IsMoveValid(gameState, randomMove))
        {
            return null;
        }
        return randomMove;
    }
    
    public readonly STile GetTileByPos(Vector2Int pos)
    {
        foreach (STile tile in boardDef.tiles.Where(tile => tile.pos == pos))
        {
            return tile;
        }
        throw new ArgumentOutOfRangeException($"GetTileByPos tile on pos {pos.ToString()} not found!");
    }

    public readonly SPawn? GetPawnByPos(in Vector2Int pos)
    {
        Vector2Int i = pos;
        foreach (SPawn pawn in pawns.Where(pawn => pawn.pos == i))
        {
            return pawn;
        }
        return null;
    }

    public readonly SPawn GetPawnById(Guid pawnId)
    {
        foreach (SPawn pawn in pawns)
        {
            if (pawn.pawnId == pawnId)
            {
                return pawn;
            }
        }
        throw new ArgumentOutOfRangeException($"GetPawnById pawnId {pawnId} not found!");
    }
    
    static void UpdatePawnIsAlive(ref SGameState gameState, in Guid pawnId, bool inIsAlive)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                Vector2Int inPos = inIsAlive ? oldPawn.pos : Globals.PURGATORY;
                SPawn updatedPawn = new()
                {
                    pawnId = oldPawn.pawnId,
                    def = oldPawn.def,
                    player = oldPawn.player,
                    pos = inPos, // sets
                    isSetup = oldPawn.isSetup,
                    isAlive = inIsAlive, // sets
                    hasMoved = oldPawn.hasMoved,
                    isVisibleToOpponent = oldPawn.isVisibleToOpponent,
                };
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }

    static void UpdateRevealPawn(ref SGameState gameState, in Guid pawnId, in bool inIsVisibleToOpponent)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                SPawn updatedPawn = new()
                {
                    pawnId = oldPawn.pawnId,
                    def = oldPawn.def,
                    player = oldPawn.player,
                    pos = oldPawn.pos,
                    isSetup = oldPawn.isSetup,
                    isAlive = oldPawn.isAlive,
                    hasMoved = oldPawn.hasMoved,
                    isVisibleToOpponent = inIsVisibleToOpponent, // sets
                };
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }
    
    static void UpdatePawnPosition(ref SGameState gameState, in Guid pawnId, in Vector2Int inPos)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                SPawn updatedPawn = new()
                {
                    pawnId = oldPawn.pawnId,
                    def = oldPawn.def,
                    player = oldPawn.player,
                    pos = inPos, // sets
                    isSetup = oldPawn.isSetup,
                    isAlive = oldPawn.isAlive,
                    hasMoved = true, // sets true because moved
                    isVisibleToOpponent = oldPawn.isVisibleToOpponent,
                };
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }
    
    static SConflictReceipt ResolveConflict(in SPawn redPawn, in SPawn bluePawn)
    {
        // Determine the outcome based on pawn strengths or game rules
        int redRank = redPawn.def.power;
        int blueRank = bluePawn.def.power;
        bool redDies;
        bool blueDies;
        // Handle special cases (e.g., Bombs, Miners, Marshal, Spy)
        if (redPawn.def.pawnName == "Bomb" && bluePawn.def.pawnName == "Miner")
        {
            redDies = true;
            blueDies = false;
            Debug.Log($"ResolveConflict: blue {bluePawn.def.pawnName} defeated {redPawn.def.pawnName}");
        }
        else if (bluePawn.def.pawnName == "Bomb" && redPawn.def.pawnName == "Miner")
        {
            redDies = false;
            blueDies = true;
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} defeated blue {bluePawn.def.pawnName}");
        }
        else if (redPawn.def.pawnName == "Marshal" && bluePawn.def.pawnName == "Spy")
        {
            redDies = true;
            blueDies = false;
            Debug.Log($"ResolveConflict: blue {bluePawn.def.pawnName} defeated red {redPawn.def.pawnName}");
        }
        else if (bluePawn.def.pawnName == "Marshal" && redPawn.def.pawnName == "Spy")
        {
            redDies = false;
            blueDies = true;
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} defeated blue {bluePawn.def.pawnName}");
        }
        else if (redRank > blueRank)
        {
            redDies = false;
            blueDies = true;
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} defeated blue {bluePawn.def.pawnName}");
        }
        else if (blueRank > redRank)
        {
            redDies = true;
            blueDies = false;
            Debug.Log($"ResolveConflict: blue {bluePawn.def.pawnName} defeated red {redPawn.def.pawnName}");
        }
        else
        {
            redDies = true;
            blueDies = true;
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} tied blue {bluePawn.def.pawnName}");
        }
        return new SConflictReceipt()
        {
            redPawnId = redPawn.pawnId,
            bluePawnId = bluePawn.pawnId,
            redDies = redDies,
            blueDies = blueDies,
        };
    }

    static void UpdatePawn(ref SGameState gameState, ref SPawn pawn)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawn.pawnId)
            {
                gameState.pawns[i] = pawn;
                break;
            }
        }
    }
    
    static List<Vector2Int> GenerateMovementPath(in SGameState gameState, in Guid pawnId, in Vector2Int targetPos)
    {
        List<Vector2Int> path = new();
        SPawn pawn = gameState.GetPawnById(pawnId);
        Vector2Int currentPos = pawn.pos;

        if (pawn.def.pawnName == "Scout")
        {
            Vector2Int currentUnityPos = currentPos;
            Vector2Int targetUnityPos = targetPos;

            // Calculate the difference
            int deltaX = targetUnityPos.x - currentUnityPos.x;
            int deltaY = targetUnityPos.y - currentUnityPos.y;

            // Ensure movement is in a straight line (either horizontal or vertical)
            if (deltaX != 0 && deltaY != 0)
            {
                throw new Exception("Invalid move: Scout must move in a straight line (horizontal or vertical).");
            }

            // Determine the direction
            int stepX = deltaX != 0 ? Math.Sign(deltaX) : 0;
            int stepY = deltaY != 0 ? Math.Sign(deltaY) : 0;
            Vector2Int direction = new Vector2Int(stepX, stepY);

            // Calculate the number of steps
            int steps = Math.Abs(deltaX != 0 ? deltaX : deltaY);

            for (int i = 1; i <= steps; i++)
            {
                Vector2Int nextPosUnity = currentUnityPos + direction * i;
                Vector2Int nextPos = nextPosUnity;
                if (!gameState.boardDef.IsPosValid(nextPos))
                {
                    break;
                }
                path.Add(nextPos);
                // Check if the path is blocked
                SPawn? pawnOnPos = gameState.GetPawnByPos(nextPos);
                if (pawnOnPos.HasValue)
                {
                    if (pawnOnPos.Value.player == pawn.player)
                    {
                        // Cannot move through own pawns
                        path.RemoveAt(path.Count - 1); // Remove the blocked position
                        break;
                    }
                    else if (nextPos != targetPos)
                    {
                        // Enemy pawn is blocking the path before the target position
                        path.RemoveAt(path.Count - 1); // Remove the blocked position
                        break;
                    }
                    // If enemy pawn is at the target position, we can include it
                    // Conflict will be resolved during movement simulation
                }
                STile tile = gameState.GetTileByPos(nextPos);
                if (tile.isPassable)
                {
                    // Cannot move through impassable tiles
                    path.RemoveAt(path.Count - 1); // Remove the blocked position
                    break;
                }
            }
        }
        else
        {
            // Non-scouts move only one tile
            path.Add(targetPos);
        }

        return path;
    }

    public static SResolveReceipt Resolve(in SGameState gameState, in SQueuedMove redMove, in SQueuedMove blueMove)
    {
        if (gameState.player != (int)Player.NONE)
        {
            throw new ArgumentException("GameState.targetPlayer must be NONE as resolve can only happen on an uncensored board!");
        }
        

        // process movement
        SPawn redMovePawn = gameState.GetPawnById(redMove.pawnId);
        SPawn blueMovePawn = gameState.GetPawnById(blueMove.pawnId);

        SPawn? maybePawnOnRedMovePos = gameState.GetPawnByPos(redMove.pos);
        SPawn? maybePawnOnBlueMovePos = gameState.GetPawnByPos(blueMove.pos);
        
        bool redGotDodgedByBlue = false;
        bool blueGotDodgedByRed = false;
        if (maybePawnOnRedMovePos.HasValue && maybePawnOnRedMovePos.Value.pawnId == blueMovePawn.pawnId)
        {
            redGotDodgedByBlue = true;
        }
        if (maybePawnOnBlueMovePos.HasValue && maybePawnOnBlueMovePos.Value.pawnId == redMovePawn.pawnId)
        {
            blueGotDodgedByRed = true;
        }

        // if redGotDodgedByBlue and BlueGotDodgedByRed we just run redConflict and then redConflictAfterDeaths
        // and then redMoveAfterConflict or blueMoveAfterConflict depending on who survived
        
        // if blueGotDodgedByRed, this sequence should be done first
        SEventState? redConflict = null;
        HashSet<SEventState> redConflictAfterDeaths = new();
        SEventState? redMoveAfterConflict = null;
        // if redGotDodgedByBlue, this sequence should be done first
        SEventState? blueConflict = null;
        HashSet<SEventState> blueConflictAfterDeaths = new();
        SEventState? blueMoveAfterConflict = null;
        bool redMoveDeferred = false;
        bool blueMoveDeferred = false;
        // if red and blue swapped places
        if (redGotDodgedByBlue && blueGotDodgedByRed)
        {
            SConflictReceipt swapConflictResult = ResolveConflict(redMovePawn, blueMovePawn);
            SEventState blueAndRedSwappedConflictEvent = new()
            {
                player = (int)Player.NONE,
                eventType = (int)ResolveEvent.SWAPCONFLICT,
                pawnId = redMovePawn.pawnId,
                defenderPawnId = blueMovePawn.pawnId,
                originalPos = redMovePawn.pos, // arbitrary
                targetPos = blueMovePawn.pos, // arbitrary
            };
            redConflict = blueAndRedSwappedConflictEvent; // arbitrary for swap
            if (swapConflictResult.redDies)
            {
                SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                redConflictAfterDeaths.Add(redDies); // arbitrary for swap
                // if blue survived, move blue to reds position
                if (!swapConflictResult.blueDies)
                {
                    SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                    blueMoveAfterConflict = blueMovesToRedPos;
                }
            }
            if (swapConflictResult.blueDies)
            {
                SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                redConflictAfterDeaths.Add(blueDies); // arbitrary for swap
                // if red survived, move red to desired position
                if (!swapConflictResult.redDies)
                {
                    SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                    redMoveAfterConflict = redMovesToBluePos;
                }
            }
        }
        // if blue got dodged, red gets to move first
        else if (blueGotDodgedByRed)
        {
            blueMoveDeferred = true;
            // do red move
            if (maybePawnOnRedMovePos.HasValue)
            {
                SPawn blueDefender = maybePawnOnRedMovePos.Value;
                SConflictReceipt redAttackStationaryConflict = ResolveConflict(redMovePawn, blueDefender);
                SEventState redStartedConflict = new()
                {
                    player = redMovePawn.player,
                    eventType = (int)ResolveEvent.CONFLICT,
                    pawnId = redMovePawn.pawnId,
                    defenderPawnId = blueDefender.pawnId,
                    originalPos = redMovePawn.pos,
                    targetPos = blueDefender.pos,
                };
                redConflict = redStartedConflict;
                if (redAttackStationaryConflict.redDies)
                {
                    SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                    redConflictAfterDeaths.Add(redDies);
                }
                if (redAttackStationaryConflict.blueDies)
                {
                    SEventState blueDies = SEventState.CreateDeathEvent(blueDefender);
                    redConflictAfterDeaths.Add(blueDies);
                    if (!redAttackStationaryConflict.redDies)
                    {
                        SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                        redMoveAfterConflict = redMovesToBluePos;
                    }
                }
            }
            else
            {
                SEventState redPeacefullyMoves = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                redMoveAfterConflict = redPeacefullyMoves;
            }
            // move blue into reds former position
            SEventState bluePeacefullyMoves = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
            blueMoveAfterConflict = bluePeacefullyMoves;
        }
        // if red got dodged, blue gets to move first
        else if (redGotDodgedByBlue)
        {
            redMoveDeferred = true;
            // do blue move
            if (maybePawnOnBlueMovePos.HasValue)
            {
                SPawn redDefender = maybePawnOnBlueMovePos.Value;
                SConflictReceipt blueAttackStationaryConflict = ResolveConflict(redDefender, blueMovePawn);
                SEventState blueStartedConflict = new()
                {
                    player = blueMovePawn.player,
                    eventType = (int)ResolveEvent.CONFLICT,
                    pawnId = blueMovePawn.pawnId,
                    defenderPawnId = redDefender.pawnId,
                    originalPos = blueMovePawn.pos,
                    targetPos = blueMove.pos,
                };
                blueConflict = blueStartedConflict;
                if (blueAttackStationaryConflict.redDies)
                {
                    SEventState redDies = SEventState.CreateDeathEvent(redDefender);
                    blueConflictAfterDeaths.Add(redDies);
                    // if blue survived, move to attacker (blue) desired position
                    if (!blueAttackStationaryConflict.blueDies)
                    {
                        SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                        blueMoveAfterConflict = blueMovesToRedPos;
                    }
                }
                if (blueAttackStationaryConflict.blueDies)
                {
                    SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                    blueConflictAfterDeaths.Add(blueDies);
                    // we don't queue up a move because the attacker (blue) died
                }
            }
            else
            {
                SEventState bluePeacefullyMoves = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                blueMoveAfterConflict = bluePeacefullyMoves;
            }
            // move red into blues former position
            SEventState redPeacefullyMoves = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
            redMoveAfterConflict = redPeacefullyMoves;
        }
        // arbitrary order since two potential conflicts cant interfere with each other
        else
        {
            if (redMove.pos == blueMove.pos)
            {
                SConflictReceipt collisionConflictResult = ResolveConflict(redMovePawn, blueMovePawn);
                SEventState collisionConflict = new()
                {
                    player = redMovePawn.player,
                    eventType = (int)ResolveEvent.CONFLICT,
                    pawnId = redMovePawn.pawnId,
                    defenderPawnId = blueMovePawn.pawnId,
                    originalPos = redMovePawn.pos,
                    targetPos = redMove.pos,
                };
                redConflict = collisionConflict; // arbitrary
                if (collisionConflictResult.redDies)
                {
                    SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                    redConflictAfterDeaths.Add(redDies); // arbitrary for swap
                    // if blue survived, move blue to reds position
                    if (!collisionConflictResult.blueDies)
                    {
                        SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                        blueMoveAfterConflict = blueMovesToRedPos;
                    }
                }
                if (collisionConflictResult.blueDies)
                {
                    SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                    redConflictAfterDeaths.Add(blueDies); // arbitrary for swap
                    // if red survived, move red to desired position
                    if (!collisionConflictResult.redDies)
                    {
                        SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                        redMoveAfterConflict = redMovesToBluePos;
                    }
                }
            }
            else
            {
                // if red encounters a blue pawn
                if (maybePawnOnRedMovePos.HasValue)
                {
                    SPawn blueDefender = maybePawnOnRedMovePos.Value;
                    SConflictReceipt redAttackStationaryConflict = ResolveConflict(redMovePawn, blueDefender);
                    SEventState redStartedConflict = new()
                    {
                        player = redMovePawn.player,
                        eventType = (int)ResolveEvent.CONFLICT,
                        pawnId = redMovePawn.pawnId,
                        defenderPawnId = blueDefender.pawnId,
                        originalPos = redMovePawn.pos,
                        targetPos = blueDefender.pos,
                    };
                    redConflict = redStartedConflict;
                    if (redAttackStationaryConflict.redDies)
                    {
                        SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                        redConflictAfterDeaths.Add(redDies);
                        // we don't queue up a move because the attacker (blue) died
                    }
                    if (redAttackStationaryConflict.blueDies)
                    {
                        SEventState blueDies = SEventState.CreateDeathEvent(blueDefender);
                        redConflictAfterDeaths.Add(blueDies);
                        if (!redAttackStationaryConflict.redDies)
                        {
                            SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                            redMoveAfterConflict = redMovesToBluePos;
                        }
                    }
                }
                else
                {
                    SEventState redPeacefullyMoves = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                    redMoveAfterConflict = redPeacefullyMoves;
                }
                // if blue encounters a red pawn
                if (maybePawnOnBlueMovePos.HasValue)
                {
                    SPawn redDefender = maybePawnOnBlueMovePos.Value;
                    SConflictReceipt blueAttackStationaryConflict = ResolveConflict(redDefender, blueMovePawn);
                    SEventState blueStartedConflict = new()
                    {
                        player = blueMovePawn.player,
                        eventType = (int)ResolveEvent.CONFLICT,
                        pawnId = blueMovePawn.pawnId,
                        defenderPawnId = redDefender.pawnId,
                        originalPos = blueMovePawn.pos,
                        targetPos = blueMove.pos,
                    };
                    blueConflict = blueStartedConflict;
                    if (blueAttackStationaryConflict.redDies)
                    {
                        SEventState redDies = SEventState.CreateDeathEvent(redDefender);
                        blueConflictAfterDeaths.Add(redDies);
                        // if blue survived, move to attacker (blue) desired position
                        if (!blueAttackStationaryConflict.blueDies)
                        {
                            SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                            blueMoveAfterConflict = blueMovesToRedPos;
                        }
                    }
                    if (blueAttackStationaryConflict.blueDies)
                    {
                        SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                        blueConflictAfterDeaths.Add(blueDies);
                        // we don't queue up a move because the attacker (blue) died
                    }
                }
                else
                {
                    SEventState bluePeacefullyMoves = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                    blueMoveAfterConflict = bluePeacefullyMoves;
                }
            }
        }
        
        SEventState[] receipts = new SEventState[6];
        SGameState nextGameState = new()
        {
            player = (int)Player.NONE,
            boardDef = gameState.boardDef,
            pawns = (SPawn[])gameState.pawns.Clone(),
        };
        
        // apply events to state
        
        if (redGotDodgedByBlue && blueGotDodgedByRed)
        {
            int eventIndex = 0;
            Debug.Assert(redConflict.HasValue); // swap conflicts are done on red conflict. swaps always cause conflict
            UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
            UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
            receipts[eventIndex] = redConflict.Value;
            eventIndex++;
            Debug.Assert(redConflict.Value.eventType == (int)ResolveEvent.SWAPCONFLICT);
            Debug.Assert(redConflictAfterDeaths.Count >= 1); // someone has to die
            // in frontend we run the battle routine, backend just applies the result
            foreach (SEventState deathEvent in redConflictAfterDeaths)
            {
                // in frontend we move pawn from deathEvent.originalPos to targetPos (purgatory) visually
                UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                receipts[eventIndex] = deathEvent;
                eventIndex++;
            }
            // in frontend we wait for event to end
            if (redMoveAfterConflict.HasValue)
            {
                // in frontend we move pawn from redMoveAfterConflict.originalPos to targetPos visually
                UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = redMoveAfterConflict.Value;
                eventIndex++;
            }
            // wait
            if (blueMoveAfterConflict.HasValue)
            {
                UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = blueMoveAfterConflict.Value;
                eventIndex++;
            }
            bool thisCantHappen = redMoveAfterConflict.HasValue && blueMoveAfterConflict.HasValue;
            Debug.Assert(!thisCantHappen);
        }
        else if (blueGotDodgedByRed)
        {
            int eventIndex = 0;
            Debug.Assert(blueMoveDeferred); // blue moves after
            Debug.Assert(!blueConflict.HasValue); // blue got cucked out of a conflict
            Debug.Assert(blueConflictAfterDeaths.Count == 0); // blue didnt kill anyone
            Debug.Assert(blueMoveAfterConflict.HasValue); // blue got to move into reds tile
            if (redConflict.HasValue)
            {
                UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
                UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
                receipts[eventIndex] = redConflict.Value;
                eventIndex++;
                Debug.Assert(redConflictAfterDeaths.Count >= 1); // someone has to die
                // battle routine
                foreach (SEventState deathEvent in redConflictAfterDeaths)
                {
                    UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                    receipts[eventIndex] = deathEvent;
                    eventIndex++;
                }
                // wait
                if (redMoveAfterConflict.HasValue)
                {
                    // in frontend we move pawn from redMoveAfterConflict.originalPos to targetPos visually
                    UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = redMoveAfterConflict.Value;
                    eventIndex++;
                }
            }
            else
            {
                // peacefully move red
                UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = redMoveAfterConflict.Value;
                eventIndex++;
            }
            // peacefully move blue into reds former position
            UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
            receipts[eventIndex] = blueMoveAfterConflict.Value;
            eventIndex++;
        }
        else if (redGotDodgedByBlue)
        {
            int eventIndex = 0;
            Debug.Assert(redMoveDeferred); // red moves after
            Debug.Assert(!redConflict.HasValue); // red got cucked out of a conflict
            Debug.Assert(redConflictAfterDeaths.Count == 0); // red didnt kill anyone
            Debug.Assert(redMoveAfterConflict.HasValue); // red got to move into blues tile
            if (blueConflict.HasValue)
            {
                UpdateRevealPawn(ref nextGameState, blueConflict.Value.pawnId, true);
                UpdateRevealPawn(ref nextGameState, blueConflict.Value.defenderPawnId, true);
                receipts[eventIndex] = blueConflict.Value;
                eventIndex++;
                Debug.Assert(blueConflictAfterDeaths.Count >= 1); // someone has to die
                // battle routine
                foreach (SEventState deathEvent in blueConflictAfterDeaths)
                {
                    UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                    receipts[eventIndex] = deathEvent;
                    eventIndex++;
                }
                // wait
                if (blueMoveAfterConflict.HasValue)
                {
                    // in frontend we move pawn from blueMoveAfterConflict.originalPos to targetPos visually
                    UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = blueMoveAfterConflict.Value;
                    eventIndex++;
                }
            }
            else
            {
                // peacefully move blue
                UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = blueMoveAfterConflict.Value;
                eventIndex++;
            }
            // peacefully move red into blues former position
            UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
            receipts[eventIndex] = redMoveAfterConflict.Value;
            eventIndex++;
        }
        else
        {
            if (redMove.pos == blueMove.pos)
            {
                int eventIndex = 0;
                Debug.Assert(redConflict.HasValue);
                Debug.Assert(redConflictAfterDeaths.Count >= 1);
                UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
                UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
                receipts[eventIndex] = redConflict.Value;
                eventIndex++;
                // battle
                foreach (SEventState deathEvent in redConflictAfterDeaths)
                {
                    UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                    receipts[eventIndex] = deathEvent;
                    eventIndex++;
                }
                // wait
                if (redMoveAfterConflict.HasValue)
                {
                    Debug.Assert(blueMoveAfterConflict == null);
                    UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMove.pos);
                    receipts[eventIndex] = redMoveAfterConflict.Value;
                    eventIndex++;
                }
                if (blueMoveAfterConflict.HasValue)
                {
                    Debug.Assert(redMoveAfterConflict == null);
                    UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMove.pos);
                    receipts[eventIndex] = blueMoveAfterConflict.Value;
                    eventIndex++;
                }
                
            }
            else
            {
                int eventIndex = 0;
                // we cant guarantee any conflicts or moves will exist 
                if (redConflict.HasValue)
                {
                    Debug.Assert(redConflictAfterDeaths.Count >= 1); // someone has to die
                    UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
                    UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
                    receipts[eventIndex] = redConflict.Value;
                    eventIndex++;
                    // battle routine
                    foreach (SEventState deathEvent in redConflictAfterDeaths)
                    {
                        UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                        receipts[eventIndex] = deathEvent;
                        eventIndex++;
                    }

                    // wait
                    if (redMoveAfterConflict.HasValue)
                    {
                        // in frontend we move pawn from redMoveAfterConflict.originalPos to targetPos visually
                        UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                        receipts[eventIndex] = redMoveAfterConflict.Value;
                        eventIndex++;
                    }
                }
                else
                {
                    // peacefully move red
                    Debug.Assert(redMoveAfterConflict.HasValue);
                    UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = redMoveAfterConflict.Value;
                    eventIndex++;
                }

                if (blueConflict.HasValue)
                {
                    Debug.Assert(blueConflictAfterDeaths.Count >= 1); // someone has to die
                    UpdateRevealPawn(ref nextGameState, blueConflict.Value.pawnId, true);
                    UpdateRevealPawn(ref nextGameState, blueConflict.Value.defenderPawnId, true);
                    receipts[eventIndex] = blueConflict.Value;
                    eventIndex++;
                    // battle routine
                    foreach (SEventState deathEvent in blueConflictAfterDeaths)
                    {
                        UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                        receipts[eventIndex] = deathEvent;
                        eventIndex++;
                    }

                    // wait
                    if (blueMoveAfterConflict.HasValue)
                    {
                        // in frontend we move pawn from blueMoveAfterConflict.originalPos to targetPos visually
                        UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                        receipts[eventIndex] = blueMoveAfterConflict.Value;
                        eventIndex++;
                    }
                }
                else
                {
                    // peacefully move blue
                    Debug.Assert(blueMoveAfterConflict.HasValue);
                    UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = blueMoveAfterConflict.Value;
                    eventIndex++;
                }
            }
        }

        if (!IsStateValid(nextGameState))
        {
            throw new Exception("Resolve nextGameState is not valid");
        }
        SEventState[] trimmedReceipts = receipts.Reverse().SkipWhile(x => x.pawnId == Guid.Empty).Reverse().ToArray();
        nextGameState.winnerPlayer = GetStateWinner(nextGameState);
        SResolveReceipt finalReceipt = new()
        {
            player = (int)Player.NONE,
            gameState = nextGameState,
            events = trimmedReceipts,
        };
        return finalReceipt;
    }

    public static bool IsStateValid(in SGameState gameState)
    {
        bool pawnOverlapDetected = false;
        HashSet<SPawn> overlappingPawns = new();
        Dictionary<Vector2Int, SPawn> pawnPositions = new();
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.isAlive)
            {
                if (pawnPositions.ContainsKey(pawn.pos))
                {
                    pawnOverlapDetected = true;
                    overlappingPawns.Add(pawnPositions[pawn.pos]);
                    overlappingPawns.Add(pawn);
                }
                else
                {
                    pawnPositions.Add(pawn.pos, pawn);
                }
            }
            else
            {
                if (pawn.pos != Globals.PURGATORY)
                {
                    Debug.LogError($"Dead pawn {pawn.pawnId} not in PURGATORY");
                    return false;
                }
            }
        }
        if (pawnOverlapDetected)
        {
            string error = overlappingPawns.Aggregate("", (current, pawn) => current + $"{pawn.pawnId} {pawn.def.pawnName} {pawn.pos.ToString()}");
            Debug.LogError($"IsStateValid Pawn overlap detected: {error}");
            return false;
        }
        return true;
    }

    public static SSetupPawn[] GenerateValidSetup(int targetPlayer, in SSetupParameters setupParameters)
    {
        if (targetPlayer == (int)Player.NONE)
        {
            throw new Exception("Player can't be none");
        }
        List<SSetupPawn> setupPawns = new();
        HashSet<Vector2Int> usedPositions = new();
        List<Vector2Int> allEligiblePositions = new();
        foreach (STile sTile in setupParameters.board.tiles)
        {
            if (sTile.IsTileEligibleForPlayer(targetPlayer))
            {
                allEligiblePositions.Add(sTile.pos);
            }
        }
        foreach (SSetupPawnData setupPawnData in setupParameters.maxPawnsDict)
        {
            List<Vector2Int> eligiblePositions = setupParameters.board.GetEligiblePositionsForPawn(targetPlayer, setupPawnData.pawnDef, usedPositions);
            if (eligiblePositions.Count < setupPawnData.maxPawns)
            {
                eligiblePositions = allEligiblePositions.Except(usedPositions).ToList();
            }
            for (int i = 0; i < setupPawnData.maxPawns; i++)
            {
                if (eligiblePositions.Count == 0)
                {
                    break;
                }
                int index = UnityEngine.Random.Range(0, eligiblePositions.Count);
                Vector2Int pos = eligiblePositions[index];
                eligiblePositions.RemoveAt(index);
                usedPositions.Add(pos);
                SSetupPawn sSetupPawn = new()
                {
                    player = targetPlayer,
                    def = setupPawnData.pawnDef,
                    pos = pos,
                };
                setupPawns.Add(sSetupPawn);
            }
        }
        return setupPawns.ToArray();
    }

    public static int GetStateWinner(in SGameState gameState)
    {
        // Define constants for return values
        const int NO_WINNER = 0;
        const int RED_WIN = 1;
        const int BLUE_WIN = 2;
        const int TIE = 4;

        SPawn redFlag = gameState.pawns.FirstOrDefault(p => p.player == (int)Player.RED && p.def.pawnName.Equals("Flag", StringComparison.OrdinalIgnoreCase));
        SPawn blueFlag = gameState.pawns.FirstOrDefault(p => p.player == (int)Player.BLUE && p.def.pawnName.Equals("Flag", StringComparison.OrdinalIgnoreCase));
        bool redCanMove = false;
        bool blueCanMove = false;
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.isAlive)
            {
                var movableTiles = gameState.GetMovableTiles(pawn);
                if (movableTiles.Length > 0)
                {
                    if (pawn.player == (int)Player.BLUE)
                    {
                        blueCanMove = true;
                    }
                    if (pawn.player == (int)Player.RED)
                    {
                        redCanMove = true;
                    }
                }
            }
        }
        // Determine win conditions
        bool redWinCondition = !blueFlag.isAlive || !blueCanMove;
        bool blueWinCondition = !redFlag.isAlive || !redCanMove;
        if (!blueCanMove)
        {
            Debug.Log("Blue cant move so win condition set");
        }
        if (!redCanMove)
        {
            Debug.Log("Red cant move so win condition set");
        }
        // Determine the winner based on conditions
        if (redWinCondition && blueWinCondition)
        {
            return TIE;
        }
        else if (redWinCondition)
        {
            return RED_WIN;
        }
        else if (blueWinCondition)
        {
            return BLUE_WIN;
        }
        else
        {
            return NO_WINNER;
        }
    }
}

public struct PawnChanges
{
    public Pawn pawn;
    public bool posChanged;
    public bool isSetupChanged;
    public bool isAliveChanged;
    public bool hasMovedChanged;
    public bool isVisibleToOpponentChanged;

    public bool IsChanged()
    {
        if (posChanged)
        {
            return true;
        }
        if (isSetupChanged)
        {
            return true;
        }
        if (isAliveChanged)
        {
            return true;
        }
        if (hasMovedChanged)
        {
            return true;
        }
        if (isVisibleToOpponentChanged)
        {
            return true;
        }
        return false;
    }
    
    public override string ToString()
    {
        return $"{pawn.pawnId} {pawn.def.pawnName} posChanged: {posChanged} isSetupChanged: {isSetupChanged} isAliveChanged: {isAliveChanged} hasMovedChanged: {hasMovedChanged} isVisibleToOpponentChanged: {isVisibleToOpponentChanged}";
    }
}

public struct SConflictReceipt
{
    public Guid redPawnId;
    public Guid bluePawnId;
    public bool redDies;
    public bool blueDies;

    public override string ToString()
    {
        return $"red id: {redPawnId} blue id: {bluePawnId} redDies: {redDies} blueDies: {blueDies}";
    }
}

public struct SResolveReceipt
{
    public int player;
    public SGameState gameState;
    public SEventState[] events;
}

public struct SEventState
{
    public int player;
    public int eventType;
    public Guid pawnId;
    public Guid defenderPawnId;
    public Vector2Int originalPos;
    public Vector2Int targetPos;

    public static SEventState CreateDeathEvent(SPawn inPawn)
    {
        SEventState deathEvent = new()
        {
            player = inPawn.player,
            eventType = (int)ResolveEvent.DEATH,
            pawnId = inPawn.pawnId,
            defenderPawnId = Guid.Empty,
            originalPos = inPawn.pos,
            targetPos = Globals.PURGATORY,
        };
        return deathEvent;
    }
    
    public static SEventState CreateMoveEvent(SPawn inPawn, Vector2Int inTargetPos)
    {
        if (inPawn.pawnId == Guid.Empty)
        {
            throw new Exception("inPawn pawnId cant be empty");
        }
        SEventState moveEvent = new()
        {
            player = inPawn.player,
            eventType = (int)ResolveEvent.MOVE,
            pawnId = inPawn.pawnId,
            defenderPawnId = Guid.Empty,
            originalPos = inPawn.pos,
            targetPos = inTargetPos,
        };
        return moveEvent;
    }
    
    public override string ToString()
    {
        string baseString = $"{(ResolveEvent)eventType} {(Player)player} {Globals.ShortGuid(pawnId)} ";
        switch ((ResolveEvent)eventType)
        {
            case ResolveEvent.MOVE:
                return baseString + $"{originalPos}  to {targetPos}";
            case ResolveEvent.CONFLICT:
                return baseString + $"vs {Globals.ShortGuid(defenderPawnId)}";
            case ResolveEvent.SWAPCONFLICT:
                return baseString + $"vs {Globals.ShortGuid(defenderPawnId)}";
            case ResolveEvent.DEATH:
                return baseString;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum ResolveEvent
{
    MOVE,
    CONFLICT,
    SWAPCONFLICT,
    DEATH,
}

public struct SSetupPawn
{
    public int player;
    public SPawnDef def;
    public Vector2Int pos;

    public SSetupPawn(Pawn pawn)
    {
        player = (int)pawn.player;
        def = new SPawnDef(pawn.def);
        pos = pawn.pos;
    }
}