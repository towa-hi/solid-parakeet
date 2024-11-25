using System;
using System.Collections.Generic;
using System.Linq;
//using System.Net.Sockets;
using System.Text.RegularExpressions;
using NUnit.Framework;
//using System.Threading.Tasks;
//using JetBrains.Annotations;
using UnityEngine;
using Random = System.Random;

public static class Globals
{
    public static Vector2Int PURGATORY = new(-666, -666);
    public static bool SETUPMUSTPLACEALLPAWNS = false;
    public static float PAWNMOVEDURATION = 0.5f;
    // Static instance of GameInputActions to be shared among all Hoverable instances
    public static readonly InputSystem_Actions inputActions = new();
    //
    // public static Dictionary<string, string> pawnSprites = new Dictionary<string, string>
    // {
    //     { "Bomb", "bomb" },
    //     { "Captain", "6"},
    //     { "Colonel", "8"},
    //     { "Flag", "flag"},
    //     { "General", "9"},
    //     { "Lieutenant", "5"},
    //     { "Major", "7"},
    //     { "Marshal", "10"},
    //     { "Miner", "m"},
    //     { "Scout", "s"},
    //     { "Sergeant", "4"},
    //     { "Spy", "dagger"},
    // };
    //
    // public static byte[] SerializeMessage(MessageType type, byte[] data)
    // {
    //     using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
    //     {
    //         // Convert MessageType to bytes (4 bytes, little endian)
    //         byte[] typeBytes = BitConverter.GetBytes((uint)type);
    //         ms.Write(typeBytes, 0, typeBytes.Length);
    //
    //         // Convert data length to bytes (4 bytes, little endian)
    //         byte[] lengthBytes = BitConverter.GetBytes((uint)data.Length);
    //         ms.Write(lengthBytes, 0, lengthBytes.Length);
    //
    //         // Write data bytes
    //         ms.Write(data, 0, data.Length);
    //
    //         return ms.ToArray();
    //     }
    // }
    //
    // public static async Task<(MessageType, byte[])> DeserializeMessageAsync(NetworkStream stream)
    // {
    //     byte[] header = new byte[8];
    //     int bytesRead = 0;
    //     while (bytesRead < 8)
    //     {
    //         int read = await stream.ReadAsync(header, bytesRead, 8 - bytesRead);
    //         if (read == 0)
    //             throw new Exception("Disconnected");
    //         bytesRead += read;
    //     }
    //
    //     // Read message type
    //     MessageType type = (MessageType)BitConverter.ToUInt32(header, 0);
    //
    //     // Read data length
    //     uint length = BitConverter.ToUInt32(header, 4);
    //
    //     // Read data
    //     byte[] data = new byte[length];
    //     bytesRead = 0;
    //     while (bytesRead < length)
    //     {
    //         int read = await stream.ReadAsync(data, bytesRead, (int)(length - bytesRead));
    //         if (read == 0)
    //             throw new Exception("Disconnected during data reception");
    //         bytesRead += read;
    //     }
    //
    //     return (type, data);
    // }
    // public static int[] ParsePassword(string password)
    // {
    //     // Remove any non-digit and non-separator characters
    //     string cleanedPassword = Regex.Replace(password, "[^0-9, ]", "");
    //
    //     // Split the string by commas or spaces
    //     string[] parts = cleanedPassword.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
    //
    //     if (parts.Length == 5)
    //     {
    //         int[] passwordInts = new int[5];
    //         for (int i = 0; i < 5; i++)
    //         {
    //             if (!int.TryParse(parts[i], out passwordInts[i]))
    //             {
    //                 Debug.LogError($"Failed to parse part {i + 1}: '{parts[i]}'");
    //                 return null; // Parsing failed
    //             }
    //         }
    //         Debug.Log($"Parsed password with separators: [{string.Join(", ", passwordInts)}]");
    //         return passwordInts;
    //     }
    //     else if (cleanedPassword.Length == 5)
    //     {
    //         int[] passwordInts = new int[5];
    //         for (int i = 0; i < 5; i++)
    //         {
    //             char c = cleanedPassword[i];
    //             if (!char.IsDigit(c))
    //             {
    //                 Debug.LogError($"Non-digit character found at position {i + 1}: '{c}'");
    //                 return null; // Invalid character
    //             }
    //             passwordInts[i] = c - '0';
    //         }
    //         Debug.Log($"Parsed password without separators: [{string.Join(", ", passwordInts)}]");
    //         return passwordInts;
    //     }
    //     else
    //     {
    //         Debug.LogError($"Invalid password format. Expected 5 integers separated by commas/spaces or a continuous 5-digit number. Received: '{password}'");
    //         return null; // Invalid format
    //     }
    // }

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
}

public enum MessageType : uint
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

public enum AppState
{
    MAIN,
    GAME,
}
public enum GamePhase
{
    UNINITIALIZED,
    SETUP,
    WAITING,
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
    public bool hostReady;
    public bool guestReady;
    public SLobby() { }

    public bool IsHost(Guid clientId)
    {
        return clientId == hostId;
    }
}



[Serializable]
public struct SVector2Int : IEquatable<SVector2Int>
{
    public override bool Equals(object obj)
    {
        return obj is SVector2Int other && Equals(other);
    }

    public int x;
    public int y;
    
    public SVector2Int(Vector2Int vector)
    {
        x = vector.x;
        y = vector.y;
    }

    public SVector2Int(int inX, int inY)
    {
        x = inX;
        y = inY;
    }
    
    public readonly Vector2Int ToUnity()
    {
        return new Vector2Int(x, y);
    }
    
    // Implement IEquatable for performance
    public bool Equals(SVector2Int other)
    {
        return x == other.x && y == other.y;
    }

    // Override GetHashCode
    public override int GetHashCode()
    {
        return HashCode.Combine(x, y);
    }

    // Define the == operator
    public static bool operator ==(SVector2Int left, SVector2Int right)
    {
        return left.Equals(right);
    }

    // Define the != operator
    public static bool operator !=(SVector2Int left, SVector2Int right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"({x}, {y})";
    }
}

public struct SSetupParameters
{
    public int player;
    public SBoardDef board;
    public SSetupPawnData[] maxPawnsDict;
    

    public static bool IsSetupValid(int targetPlayer, SSetupParameters setupParameters, SPawn[] pawns)
    {
        // Convert the SSetupPawnData array to a dictionary for easier lookup
        Dictionary<SPawnDef, int> pawnCounts = new Dictionary<SPawnDef, int>();
        foreach (var pawnData in setupParameters.maxPawnsDict)
        {
            pawnCounts[pawnData.pawnDef] = pawnData.maxPawns;
        }
        // Iterate over the provided pawns
        foreach (SPawn pawn in pawns)
        {
            if (!pawn.isAlive || pawn.player != targetPlayer)
            {
                continue;
            }
            // Check if the pawnDef is in the max pawns dictionary
            if (!pawnCounts.ContainsKey(pawn.def))
            {
                Debug.LogError($"PawnDef '{pawn.def.pawnName}' not found in max pawns data.");
                return false;
            }
            pawnCounts[pawn.def] -= 1;
            // If count goes negative, there are too many pawns of this type
            if (pawnCounts[pawn.def] < 0)
            {
                Debug.LogError($"Too many pawns of type '{pawn.def.pawnName}'.");
                return false;
            }
            // Check if the pawn is on a valid tile
            if (!setupParameters.board.IsPosValid(pawn.pos))
            {
                Debug.LogError($"Pawn '{pawn.def.pawnName}' is on an invalid position {pawn.pos}.");
                return false;
            }
            STile tile = setupParameters.board.GetTileFromPos(pawn.pos);
            if (!tile.IsTileEligibleForPlayer(targetPlayer))
            {
                Debug.LogError($"Tile at position {pawn.pos} is not eligible for player '{targetPlayer}'.");
                return false;
            }
        }
        if (!Globals.SETUPMUSTPLACEALLPAWNS)
        {
            return pawns.Where(pawn => pawn.def.pawnName == "Flag").Any(pawn => pawn.isAlive && pawn.isSetup);
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
    void SendSetupSubmissionRequest(SPawn[] setupPawns);
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
    public SPawn[] pawns;
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
    public SVector2Int initialPos;
    public SVector2Int pos;

    public SQueuedMove(in SPawn pawn, in SVector2Int inPos)
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
        initialPos = new SVector2Int(pawn.pos);
        pos = new SVector2Int(inPos);
    }
    
    public SQueuedMove(int inPlayer, Guid inPawnId, SVector2Int inInitialPos, SVector2Int inPos)
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
            Vector2Int currentPos = pawn.pos.ToUnity();
            bool enemyEncountered = false;

            for (int i = 0; i < pawnMovementRange; i++)
            {
                currentPos += dir;
                if (enemyEncountered)
                {
                    break;
                }
                // Check if the position is within the board bounds
                if (!IsPosValid(new SVector2Int(currentPos)))
                {
                    break;
                }
                // Get the tile at the current position
                STile tile = GetTileFromPos(new SVector2Int(currentPos));
                // check if tile is passable
                if (!tile.isPassable)
                {
                    break;
                }
                // Check if the tile is occupied by another pawn
                SPawn? pawnOnPos = GetPawnFromPos(new SVector2Int(currentPos));
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
            STile destinationTile = gameState.GetTileFromPos(move.pos);
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
            SPawn movingPawn = gameState.GetPawnFromId(move.pawnId);
            if (move.player != movingPawn.player)
            {
                return false;
            }
            var maybePawn = gameState.GetPawnFromPos(move.pos);
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
    
    public readonly bool IsPosValid(SVector2Int pos)
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
        Random random = new();
        int randomIndex = random.Next(0, allPossibleMoves.Count);
        SQueuedMove randomMove = allPossibleMoves[randomIndex];
        SPawn randomPawn = gameState.GetPawnFromId(randomMove.pawnId);
        Debug.Log($"GenerateValidMove chose {randomMove.pawnId} {randomPawn.def.pawnName} from {randomPawn.pos} to {randomMove.pos}");
        if (!IsMoveValid(gameState, randomMove))
        {
            return null;
        }
        return randomMove;
    }
    
    public readonly STile GetTileFromPos(SVector2Int pos)
    {
        foreach (STile tile in boardDef.tiles.Where(tile => tile.pos == pos))
        {
            return tile;
        }
        throw new ArgumentOutOfRangeException($"GetTileFromPos tile on pos {pos.ToString()} not found!");
    }

    public readonly SPawn? GetPawnFromPos(in SVector2Int pos)
    {
        SVector2Int i = pos;
        foreach (SPawn pawn in pawns.Where(pawn => pawn.pos == i))
        {
            return pawn;
        }
        return null;
    }

    public readonly SPawn GetPawnFromId(Guid pawnId)
    {
        foreach (SPawn pawn in pawns)
        {
            if (pawn.pawnId == pawnId)
            {
                return pawn;
            }
        }
        throw new ArgumentOutOfRangeException($"GetPawnFromId pawnId {pawnId} not found!");
    }
    
    static void UpdatePawnIsAlive(ref SGameState gameState, in Guid pawnId, bool inIsAlive)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                SVector2Int inPos = inIsAlive ? oldPawn.pos : new SVector2Int(Globals.PURGATORY);
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
    
    static void UpdatePawnPosition(ref SGameState gameState, in Guid pawnId, in SVector2Int inPos)
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
    
    static SConflictReceipt ResolveConflict(in SGameState gameState, in Guid redPawnId, in Guid bluePawnId, in SVector2Int conflictPos)
    {
        if (gameState.player != (int)Player.NONE)
        {
            throw new ArgumentException("ResolveConflict gameState must not be censored");
        }
        // Set isVisibleToOpponent to true for both pawns
        SPawn redPawn = gameState.GetPawnFromId(redPawnId);
        SPawn bluePawn = gameState.GetPawnFromId(bluePawnId);
        
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
            redPawnId = redPawnId,
            bluePawnId = bluePawnId,
            redDies = redDies,
            blueDies = blueDies,
            conflictPos = conflictPos,
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
    
    static List<SVector2Int> GenerateMovementPath(in SGameState gameState, in Guid pawnId, in SVector2Int targetPos)
    {
        List<SVector2Int> path = new List<SVector2Int>();
        SPawn pawn = gameState.GetPawnFromId(pawnId);
        SVector2Int currentPos = pawn.pos;

        if (pawn.def.pawnName == "Scout")
        {
            Vector2Int currentUnityPos = currentPos.ToUnity();
            Vector2Int targetUnityPos = targetPos.ToUnity();

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
                SVector2Int nextPos = new SVector2Int(nextPosUnity);
                if (!gameState.boardDef.IsPosValid(nextPos))
                {
                    break;
                }
                path.Add(nextPos);
                // Check if the path is blocked
                SPawn? pawnOnPos = gameState.GetPawnFromPos(nextPos);
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
                STile tile = gameState.GetTileFromPos(nextPos);
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
            throw new ArgumentException("GameState.player must be NONE as resolve can only happen on an uncensored board!");
        }
        
        // Create a deep copy of the game state to avoid mutating the original
        SGameState nextGameState = new()
        {
            player = (int)Player.NONE,
            boardDef = gameState.boardDef,
            pawns = (SPawn[])gameState.pawns.Clone(),
        };

        Dictionary<Guid, SVector2Int> pawnNewPositions = new Dictionary<Guid, SVector2Int>();
        HashSet<Guid> pawnsToKill = new HashSet<Guid>();
        HashSet<Guid> pawnsToReveal = new HashSet<Guid>();
        HashSet<SConflictReceipt> conflicts = new HashSet<SConflictReceipt>();
        pawnNewPositions[redMove.pawnId] = redMove.pos;
        pawnNewPositions[blueMove.pawnId] = blueMove.pos;
        bool deferRedMove = false;
        bool deferBlueMove = false;
        SPawn redMovePawn = gameState.GetPawnFromId(redMove.pawnId);
        SPawn blueMovePawn = gameState.GetPawnFromId(blueMove.pawnId);
        // move scouts first (simultaneously)
        // TODO: TEST CODE WIP
        // instead of receipt system we have a series of moves for the client to execute
        // case 1: two scouts attack different pawns at the same time
        // 0 MOVE RED SCOUT TO DESIRED POS
        // 0 MOVE BLUE SCOUT TO DESIRED POS
        // 1 CONFLICT
        // 1 CONFLICT
        // case 2: both scouts go to the same tile
        // 0 MOVE RED SCOUT TO DESIRED POS
        // 0 MOVE BLUE SCOUT TO DESIRED POS
        // 1 CONFLICT
        // case 3: red scout moves into blue scouts position but blue scout is moving somewhere else
        // 0 MOVE RED SCOUT TO DESIRED POS
        // 0 MOVE BLUE SCOUT TO DESIRED POS
        // case 4: red scout moves into blue non-scout pawn that is attacking a different pawn
        // 0 MOVE RED SCOUT TO DESIRED POS
        // 1 CONFLICT
        // if the scout won the conflict, the blue move is negated else blue pawn moves now
        // 2 MOVE BLUE PAWN TO DESIRED POS
        // 3 CONFLICT
        // case 5: non scout pawn swaps with non scout pawn
        // 0 CONFLICT
        // 1 WINNER MOVES TO DESIRED POS
        // case 6: scout swaps with scout
        // 0 MOVE RED SCOUT TO DESIRED POS
        // 0 MOVE BLUE SCOUT TO DESIRED POS
        
        bool deferRedScoutMove;
        bool deferBlueScoutMove;
        if (redMovePawn.def.pawnName == "Scout")
        {
            // if position is occupied
            SPawn? maybePawnObstructingRedScout = gameState.GetPawnFromPos(redMove.pos);
            if (maybePawnObstructingRedScout.HasValue)
            {
                SPawn pawnObstructingRedScout = maybePawnObstructingRedScout.Value;
                if (pawnObstructingRedScout.pawnId == blueMove.pawnId && pawnObstructingRedScout.def.pawnName == "Scout")
                {
                    deferRedScoutMove = true;
                }
                else
                {
                    Debug.Assert(pawnObstructingRedScout.player != redMovePawn.player);
                    Debug.Assert(pawnObstructingRedScout.isAlive);
                    int order = 0;
                    SConflictReceipt receipt = ResolveConflict(nextGameState, redMovePawn.pawnId, pawnObstructingRedScout.pawnId, redMove.pos);
                    if (receipt.redDies)
                    {
                        UpdatePawnIsAlive(ref nextGameState, redMovePawn.pawnId, false);
                    }
                    if (receipt.blueDies)
                    {
                        UpdatePawnIsAlive(ref nextGameState, pawnObstructingRedScout.pawnId, false);
                        UpdatePawnPosition(ref nextGameState, redMovePawn.pawnId, redMove.pos);
                    }
                }
            }
        }
        if (blueMovePawn.def.pawnName == "Scout")
        {
            // if position is occupied
            SPawn? maybePawnObstructingBlueScout = gameState.GetPawnFromPos(blueMove.pos);
            if (maybePawnObstructingBlueScout.HasValue)
            {
                SPawn pawnObstructingBlueScout = maybePawnObstructingBlueScout.Value;
                if (pawnObstructingBlueScout.pawnId == blueMove.pawnId && pawnObstructingBlueScout.def.pawnName == "Scout")
                {
                    deferBlueScoutMove = true;
                }
                else
                {
                    Debug.Assert(pawnObstructingBlueScout.player != blueMovePawn.player);
                    Debug.Assert(pawnObstructingBlueScout.isAlive);
                    int order = 0;
                    SConflictReceipt receipt = ResolveConflict(nextGameState, pawnObstructingBlueScout.pawnId, blueMovePawn.pawnId, blueMove.pos);
                    if (receipt.redDies)
                    {
                        UpdatePawnIsAlive(ref nextGameState, pawnObstructingBlueScout.pawnId, false);
                        UpdatePawnPosition(ref nextGameState, blueMovePawn.pawnId, blueMove.pos);
                    }
                    if (receipt.blueDies)
                    {
                        UpdatePawnIsAlive(ref nextGameState, blueMovePawn.pawnId, false);
                    }
                }
            }
        }
        // if defer red, blue moves like normal
        
        
        // Case A: if red and blue move to the same pos
        if (redMove.pos == blueMove.pos)
        {
            //Resolve once
            int order = 0;
            SConflictReceipt receipt = ResolveConflict(gameState, redMove.pawnId, blueMove.pawnId, redMove.pos);
            pawnsToReveal.Add(redMove.pawnId);
            pawnsToReveal.Add(blueMove.pawnId);
            if (receipt.redDies)
            {
                pawnsToKill.Add(redMove.pawnId);
            }
            if (receipt.blueDies)
            {
                pawnsToKill.Add(blueMove.pawnId);
            }
        }
        // Case B: if red and blue move into each others pos
        else if (redMove.pos == blueMove.initialPos && blueMove.pos == redMove.initialPos)
        {
            int order = 0;
            SConflictReceipt receipt = ResolveConflict(gameState, redMove.pawnId, blueMove.pawnId, redMove.pos);
            conflicts.Add(receipt);
            pawnsToReveal.Add(redMove.pawnId);
            pawnsToReveal.Add(blueMove.pawnId);
            if (receipt.redDies)
            {
                pawnsToKill.Add(redMove.pawnId);
            }
            if (receipt.blueDies)
            {
                pawnsToKill.Add(blueMove.pawnId);
            }
        }
        // Case C: potentially two unrelated conflicts happening at once
        else
        {
            // Case C1: if red moved to a location with a blue pawn 
            SPawn? maybePawnObstructingRed = gameState.GetPawnFromPos(redMove.pos);
            if (maybePawnObstructingRed.HasValue)
            {
                SPawn pawnObstructingRed = maybePawnObstructingRed.Value;
                // if this pawn is also moving
                if (pawnObstructingRed.pawnId == blueMove.pawnId && gameState.GetPawnFromId(redMove.pawnId).def.pawnName != "Scout")
                {
                    // give blue a chance to do it's move first
                    deferRedMove = true;
                }
                else
                {
                    SConflictReceipt receipt = ResolveConflict(nextGameState, redMove.pawnId, pawnObstructingRed.pawnId, redMove.pos);
                    conflicts.Add(receipt);
                    pawnsToReveal.Add(redMove.pawnId);
                    pawnsToReveal.Add(pawnObstructingRed.pawnId);
                    if (receipt.redDies)
                    {
                        pawnsToKill.Add(redMove.pawnId);
                    }
                    if (receipt.blueDies)
                    {
                        pawnsToKill.Add(pawnObstructingRed.pawnId);
                    }
                }
            }
            // Case B2: if blue moved into a location with a red pawn 
            SPawn? maybePawnObstructingBlue = gameState.GetPawnFromPos(blueMove.pos);
            if (maybePawnObstructingBlue.HasValue)
            {
                SPawn pawnObstructingBlue = maybePawnObstructingBlue.Value;
                // if this pawn is also moving
                if (pawnObstructingBlue.pawnId == redMove.pawnId && gameState.GetPawnFromId(blueMove.pawnId).def.pawnName != "Scout")
                {
                    // give blue a chance to do it's move first
                    deferBlueMove = true;
                }
                else
                {
                    // Resolve conflict
                    SConflictReceipt receipt = ResolveConflict(nextGameState, pawnObstructingBlue.pawnId, blueMove.pawnId, blueMove.pos);
                    conflicts.Add(receipt);
                    pawnsToReveal.Add(blueMove.pawnId);
                    pawnsToReveal.Add(pawnObstructingBlue.pawnId);
                    if (receipt.blueDies)
                    {
                        pawnsToKill.Add(blueMove.pawnId);
                    }
                    if (receipt.redDies)
                    {
                        pawnsToKill.Add(pawnObstructingBlue.pawnId);
                    }
                }
            }
        }
        if (deferRedMove)
        {
            Debug.Log("we let blue move first because it's attacking a piece that isn't moving");
            SPawn? maybePawnObstructingBlue = gameState.GetPawnFromPos(blueMove.pos);
            if (maybePawnObstructingBlue.HasValue)
            {
                SPawn pawnObstructingBlue = maybePawnObstructingBlue.Value;
                // we know for sure pawnObstructingBlue isn't moving
                SConflictReceipt receipt = ResolveConflict(nextGameState, pawnObstructingBlue.pawnId, blueMove.pawnId, blueMove.pos);
                conflicts.Add(receipt);
                pawnsToReveal.Add(blueMove.pawnId);
                pawnsToReveal.Add(pawnObstructingBlue.pawnId);
                if (receipt.blueDies)
                {
                    pawnsToKill.Add(blueMove.pawnId);
                }
                if (receipt.redDies)
                {
                    pawnsToKill.Add(pawnObstructingBlue.pawnId);
                }
            }
        }
        if (deferBlueMove)
        {
            Debug.Log("we let RED move first because it's attacking a piece that isn't moving");
            SPawn? maybePawnObstructingRed = gameState.GetPawnFromPos(redMove.pos);
            if (maybePawnObstructingRed.HasValue)
            {
                SPawn pawnObstructingRed = maybePawnObstructingRed.Value;
                if (pawnObstructingRed.player == redMove.player)
                {
                    // the move was invalid!
                    throw new Exception("Red move was invalid because Red pawn is on position");
                }
                // we know for sure pawnObstructingBlue isn't moving
                SConflictReceipt receipt = ResolveConflict(nextGameState, redMove.pawnId, pawnObstructingRed.pawnId, redMove.pos);
                conflicts.Add(receipt);
                if (receipt.redDies)
                {
                    pawnsToKill.Add(redMove.pawnId);
                }
                if (receipt.blueDies)
                {
                    pawnsToKill.Add(pawnObstructingRed.pawnId);
                }
            }
        }
        foreach (Guid pawnId in pawnsToReveal)
        {
            UpdateRevealPawn(ref nextGameState, pawnId, true);
        }
        foreach (var kvp in pawnNewPositions)
        {
            // update the positions regardless of correctness for now
            UpdatePawnPosition(ref nextGameState, kvp.Key, kvp.Value);
        }
        foreach (var pawnId in pawnsToKill)
        {
            // kill pawns that lost their conflict
            UpdatePawnIsAlive(ref nextGameState, pawnId, false);
        }
        nextGameState.winnerPlayer = GetStateWinner(nextGameState);
        if (!IsStateValid(nextGameState))
        {
            throw new Exception("Cannot return invalid state");
        }

        SResolveReceipt finalReceipt = new SResolveReceipt()
        {
            player = (int)Player.NONE,
            blueQueuedMove = blueMove,
            redQueuedMove = redMove,
            gameState = nextGameState,
            receipts = conflicts.ToArray(),
        };
        
        return finalReceipt;
    }

    public static bool IsStateValid(in SGameState gameState)
    {
        bool pawnOverlapDetected = false;
        HashSet<SPawn> overlappingPawns = new();
        Dictionary<SVector2Int, SPawn> pawnPositions = new();
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
                if (pawn.pos != new SVector2Int(Globals.PURGATORY))
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

    public static SPawn[] GenerateValidSetup(int player, in SSetupParameters setupParameters)
    {
        if (player == (int)Player.NONE)
        {
            throw new Exception("Player can't be none");
        }
        List<SPawn> sPawns = new();
        HashSet<SVector2Int> usedPositions = new();
        List<SVector2Int> allEligiblePositions = new();
        foreach (STile sTile in setupParameters.board.tiles)
        {
            if (sTile.IsTileEligibleForPlayer(player))
            {
                allEligiblePositions.Add(sTile.pos);
            }
        }
        foreach (SSetupPawnData setupPawnData in setupParameters.maxPawnsDict)
        {
            List<SVector2Int> eligiblePositions = setupParameters.board.GetEligiblePositionsForPawn(player, setupPawnData.pawnDef, usedPositions);
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
                SVector2Int pos = eligiblePositions[index];
                eligiblePositions.RemoveAt(index);
                usedPositions.Add(pos);
                SPawn newPawn = new()
                {
                    pawnId = Guid.NewGuid(),
                    def = setupPawnData.pawnDef,
                    player = player,
                    pos = pos,
                    isSetup = false,
                    isAlive = true,
                    hasMoved = false,
                    isVisibleToOpponent = false,
                };
                sPawns.Add(newPawn);
            }
        }
        return sPawns.ToArray();
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

public struct SMoveResult
{
    public SQueuedMove originalMove;
    public bool didConflictHappen;
    public bool didConflictWin;
    
    public Guid otherPawn;
}

public struct SConflictReceipt
{
    public Guid redPawnId;
    public Guid bluePawnId;
    public bool redDies;
    public bool blueDies;
    public SVector2Int conflictPos;

    public override string ToString()
    {
        return $"red id: {redPawnId} blue id: {bluePawnId} redDies: {redDies} blueDies: {blueDies} conflictPos: {conflictPos}";
    }
}


public struct SResolveReceipt
{
    public int player;
    public SQueuedMove redQueuedMove;
    public SQueuedMove blueQueuedMove;
    public SConflictReceipt[] receipts;
    public SGameState gameState;
}