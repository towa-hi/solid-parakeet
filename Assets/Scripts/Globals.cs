using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using Random = System.Random;

public static class Globals
{
    public static Vector2Int PURGATORY = new(-666, -666);
    public static bool SETUPMUSTPLACEALLPAWNS = false;
    public static float PAWNMOVEDURATION = 0.5f;
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
    
    public static byte[] SerializeMessage(MessageType type, byte[] data)
    {
        using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
        {
            // Convert MessageType to bytes (4 bytes, little endian)
            byte[] typeBytes = BitConverter.GetBytes((uint)type);
            ms.Write(typeBytes, 0, typeBytes.Length);

            // Convert data length to bytes (4 bytes, little endian)
            byte[] lengthBytes = BitConverter.GetBytes((uint)data.Length);
            ms.Write(lengthBytes, 0, lengthBytes.Length);

            // Write data bytes
            ms.Write(data, 0, data.Length);

            return ms.ToArray();
        }
    }
    
    public static async Task<(MessageType, byte[])> DeserializeMessageAsync(NetworkStream stream)
    {
        byte[] header = new byte[8];
        int bytesRead = 0;
        while (bytesRead < 8)
        {
            int read = await stream.ReadAsync(header, bytesRead, 8 - bytesRead);
            if (read == 0)
                throw new Exception("Disconnected");
            bytesRead += read;
        }

        // Read message type
        MessageType type = (MessageType)BitConverter.ToUInt32(header, 0);

        // Read data length
        uint length = BitConverter.ToUInt32(header, 4);

        // Read data
        byte[] data = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            int read = await stream.ReadAsync(data, bytesRead, (int)(length - bytesRead));
            if (read == 0)
                throw new Exception("Disconnected during data reception");
            bytesRead += read;
        }

        return (type, data);
    }
    public static int[] ParsePassword(string password)
    {
        // Remove any non-digit and non-separator characters
        string cleanedPassword = Regex.Replace(password, "[^0-9, ]", "");

        // Split the string by commas or spaces
        string[] parts = cleanedPassword.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 5)
        {
            int[] passwordInts = new int[5];
            for (int i = 0; i < 5; i++)
            {
                if (!int.TryParse(parts[i], out passwordInts[i]))
                {
                    Debug.LogError($"Failed to parse part {i + 1}: '{parts[i]}'");
                    return null; // Parsing failed
                }
            }
            Debug.Log($"Parsed password with separators: [{string.Join(", ", passwordInts)}]");
            return passwordInts;
        }
        else if (cleanedPassword.Length == 5)
        {
            int[] passwordInts = new int[5];
            for (int i = 0; i < 5; i++)
            {
                char c = cleanedPassword[i];
                if (!char.IsDigit(c))
                {
                    Debug.LogError($"Non-digit character found at position {i + 1}: '{c}'");
                    return null; // Invalid character
                }
                passwordInts[i] = c - '0';
            }
            Debug.Log($"Parsed password without separators: [{string.Join(", ", passwordInts)}]");
            return passwordInts;
        }
        else
        {
            Debug.LogError($"Invalid password format. Expected 5 integers separated by commas/spaces or a continuous 5-digit number. Received: '{password}'");
            return null; // Invalid format
        }
    }

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
    event Action<Response<SGameState>> OnResolveResponse;
    
    
    // Methods
    Task ConnectToServer();
    Task SendRegisterNickname(string nicknameInput);
    Task SendGameLobby();
    Task SendGameLobbyLeaveRequest();
    Task SendGameLobbyReadyRequest(bool ready);
    Task SendStartGameDemoRequest();
    Task SendSetupSubmissionRequest(SPawn[] setupPawns);
    Task SendMove(SQueuedMove move);
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
public class QueuedMove
{
    public Player player;
    public Pawn pawn;
    public Vector2Int pos;

    public QueuedMove(Player inPlayer, Pawn inPawn, Vector2Int inPos)
    {
        player = inPlayer;
        pawn = inPawn;
        pos = inPos;
    }
}

public struct SQueuedMove
{
    public int player;
    public SPawn pawn;
    public SVector2Int pos;

    public SQueuedMove(int inPlayer, SPawn inPawn, SVector2Int inPos)
    {
        player = inPlayer;
        pawn = inPawn;
        pos = inPos;
    }
    
    public SQueuedMove(QueuedMove queuedMove)
    {
        player = (int)queuedMove.player;
        pawn = new SPawn(queuedMove.pawn);
        pos = new SVector2Int(queuedMove.pos);
    }
}
[Serializable]
public struct SGameState
{
    public int player;
    public SBoardDef boardDef;
    public SPawn[] pawns;

    public SGameState(int inPlayer, SBoardDef inBoardDef, SPawn[] inPawns)
    {
        player = inPlayer;
        boardDef = inBoardDef;
        pawns = inPawns;
    }
public STile[] GetMovableTiles(SPawn pawn)
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
            STile? maybeTile = GetTileFromPos(new SVector2Int(currentPos));
            if (!maybeTile.HasValue)
            {
                break;
            }
            // check if tile is passable
            STile tile = maybeTile.Value;
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

    
    public bool IsPosValid(SVector2Int pos)
    {
        return boardDef.tiles.Any(tile => tile.pos == pos);

    }

    public static SGameState Censor(SGameState masterGameState, int targetPlayer)
    {
        if (masterGameState.player != (int)Player.NONE)
        {
            throw new Exception("Censor can only be done on master game states!");
        }

        SGameState censoredGameState = new SGameState
        {
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
                if (serverPawn.isVisibleToOpponent)
                {
                    censoredPawn = serverPawn;
                }
                else
                {
                    censoredPawn = serverPawn.Censor();
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
    
    public SQueuedMove? GenerateValidMove(int targetPlayer)
    {
        List<SQueuedMove> allPossibleMoves = new List<SQueuedMove>();
        foreach (SPawn pawn in pawns)
        {
            if (pawn.player == targetPlayer)
            {
                STile[] movableTiles = GetMovableTiles(pawn);
                foreach (var tile in movableTiles)
                {
                    allPossibleMoves.Add(new SQueuedMove(targetPlayer, pawn, tile.pos));
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
        Debug.Log($"GenerateValidMove chose {randomMove.pawn.pawnId} {randomMove.pawn.def.pawnName} from {randomMove.pawn.pos} to {randomMove.pos}");
        return randomMove;
    }
    
    public STile? GetTileFromPos(SVector2Int pos)
    {
        foreach (STile tile in boardDef.tiles.Where(tile => tile.pos == pos))
        {
            return tile;
        }
        return null;
    }

    public SPawn? GetPawnFromPos(SVector2Int pos)
    {
        foreach (SPawn pawn in pawns.Where(pawn => pawn.pos == pos))
        {
            return pawn;
        }
        return null;
    }

    public SPawn? GetPawnFromId(Guid pawnId)
    {
        foreach (SPawn pawn in pawns)
        {
            if (pawn.pawnId == pawnId)
                return pawn;
        }
        return null;
    }
    
    static void RemovePawn(ref SGameState gameState, SPawn pawn)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawn.pawnId)
            {
                SPawn updatedPawn = pawn.Kill();
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }
    
    static void UpdatePawnPosition(ref SGameState gameState, SPawn pawn, SVector2Int newPos)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawn.pawnId)
            {
                SPawn updatedPawn = pawn.Move(newPos);
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }
    static void ResolveConflict(ref SGameState gameState, SPawn redPawn, SPawn bluePawn, SVector2Int conflictPos)
    {
        // Set isVisibleToOpponent to true for both pawns
        redPawn.isVisibleToOpponent = true;
        bluePawn.isVisibleToOpponent = true;

        // Update the pawns in the game state
        UpdatePawn(ref gameState, redPawn);
        UpdatePawn(ref gameState, bluePawn);

        // Determine the outcome based on pawn strengths or game rules
        int redRank = redPawn.def.power;
        int blueRank = bluePawn.def.power;

        // Handle special cases (e.g., Bombs, Miners, Marshal, Spy)
        if (redPawn.def.pawnName == "Bomb" && bluePawn.def.pawnName == "Miner")
        {
            Debug.Log($"ResolveConflict: blue {bluePawn.def.pawnName} defeated {redPawn.def.pawnName}");
            RemovePawn(ref gameState, redPawn);
            UpdatePawnPosition(ref gameState, bluePawn, conflictPos);
        }
        else if (bluePawn.def.pawnName == "Bomb" && redPawn.def.pawnName == "Miner")
        {
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} defeated blue {bluePawn.def.pawnName}");
            RemovePawn(ref gameState, bluePawn);
            UpdatePawnPosition(ref gameState, redPawn, conflictPos);
        }
        else if (redPawn.def.pawnName == "Marshal" && bluePawn.def.pawnName == "Spy")
        {
            Debug.Log($"ResolveConflict: blue {bluePawn.def.pawnName} defeated red {redPawn.def.pawnName}");
            RemovePawn(ref gameState, redPawn);
            UpdatePawnPosition(ref gameState, bluePawn, conflictPos);
        }
        else if (bluePawn.def.pawnName == "Marshal" && redPawn.def.pawnName == "Spy")
        {
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} defeated blue {bluePawn.def.pawnName}");
            RemovePawn(ref gameState, bluePawn);
            UpdatePawnPosition(ref gameState, redPawn, conflictPos);
        }
        else if (redRank > blueRank)
        {
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} defeated blue {bluePawn.def.pawnName}");
            // Red wins; remove blue pawn
            RemovePawn(ref gameState, bluePawn);
            UpdatePawnPosition(ref gameState, redPawn, conflictPos);
        }
        else if (blueRank > redRank)
        {
            Debug.Log($"ResolveConflict: blue {bluePawn.def.pawnName} defeated red {redPawn.def.pawnName}");
            // Blue wins; remove red pawn
            RemovePawn(ref gameState, redPawn);
            UpdatePawnPosition(ref gameState, bluePawn, conflictPos);
        }
        else
        {
            Debug.Log($"ResolveConflict: red {redPawn.def.pawnName} tied blue {bluePawn.def.pawnName}");
            // Both pawns are eliminated
            RemovePawn(ref gameState, redPawn);
            RemovePawn(ref gameState, bluePawn);
        }
    }

    static void UpdatePawn(ref SGameState gameState, SPawn pawn)
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
    
    static List<SVector2Int> GenerateMovementPath(SGameState gameState, SPawn pawn, SVector2Int targetPos)
    {
        List<SVector2Int> path = new List<SVector2Int>();
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

                STile? tile = gameState.GetTileFromPos(nextPos);
                if (!tile.HasValue || !tile.Value.isPassable)
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

    public static SGameState Resolve(SGameState gameState, SQueuedMove redMove, SQueuedMove blueMove)
{
    if (gameState.player != (int)Player.NONE)
    {
        throw new ArgumentException("GameState.player must be NONE as resolve can only happen on an uncensored board!");
    }

    // Create a deep copy of the game state to avoid mutating the original
    SGameState nextGameState = new SGameState()
    {
        player = (int)Player.NONE,
        boardDef = gameState.boardDef,
        pawns = (SPawn[])gameState.pawns.Clone(),
    };

    // Get the moving pawns
    SPawn? maybeRedPawn = nextGameState.GetPawnFromId(redMove.pawn.pawnId);
    SPawn? maybeBluePawn = nextGameState.GetPawnFromId(blueMove.pawn.pawnId);

    if (!maybeRedPawn.HasValue || !maybeBluePawn.HasValue)
    {
        throw new Exception("One of the moving pawns does not exist in the game state.");
    }

    SPawn redPawn = maybeRedPawn.Value;
    SPawn bluePawn = maybeBluePawn.Value;

    // Generate movement paths
    List<SVector2Int> redPath = GenerateMovementPath(gameState, redPawn, redMove.pos);
    List<SVector2Int> bluePath = GenerateMovementPath(gameState, bluePawn, blueMove.pos);

    // Determine maximum path length
    int maxPathLength = Math.Max(redPath.Count, bluePath.Count);

    // Initialize positions
    SVector2Int redCurrentPos = redPawn.pos;
    SVector2Int blueCurrentPos = bluePawn.pos;

    // Keep track of eliminated pawns to avoid conflicts with already eliminated pawns
    HashSet<Guid> eliminatedPawns = new HashSet<Guid>();

    // Simulate movement step by step
    for (int step = 0; step < maxPathLength; step++)
    {
        // Update positions if the pawn has more steps and is not eliminated
        if (!eliminatedPawns.Contains(redPawn.pawnId) && step < redPath.Count)
        {
            redCurrentPos = redPath[step];

            // Check for conflict with any enemy pawn at redCurrentPos
            SPawn? enemyPawn = nextGameState.GetPawnFromPos(redCurrentPos);
            if (enemyPawn.HasValue && enemyPawn.Value.player == (int)Player.BLUE && !eliminatedPawns.Contains(enemyPawn.Value.pawnId))
            {
                // Conflict occurs between redPawn and the enemy pawn
                ResolveConflict(ref nextGameState, redPawn, enemyPawn.Value, redCurrentPos);
                eliminatedPawns.Add(redPawn.pawnId);
                eliminatedPawns.Add(enemyPawn.Value.pawnId);
                continue; // Skip to next iteration
            }
        }

        if (!eliminatedPawns.Contains(bluePawn.pawnId) && step < bluePath.Count)
        {
            blueCurrentPos = bluePath[step];

            // Check for conflict with any enemy pawn at blueCurrentPos
            SPawn? enemyPawn = nextGameState.GetPawnFromPos(blueCurrentPos);
            if (enemyPawn.HasValue && enemyPawn.Value.player == (int)Player.RED && !eliminatedPawns.Contains(enemyPawn.Value.pawnId))
            {
                // Conflict occurs between bluePawn and the enemy pawn
                ResolveConflict(ref nextGameState, bluePawn, enemyPawn.Value, blueCurrentPos);
                eliminatedPawns.Add(bluePawn.pawnId);
                eliminatedPawns.Add(enemyPawn.Value.pawnId);
                continue; // Skip to next iteration
            }
        }

        // Check for collision between moving pawns at this step
        if (redCurrentPos == blueCurrentPos)
        {
            // Conflict occurs between redPawn and bluePawn
            ResolveConflict(ref nextGameState, redPawn, bluePawn, redCurrentPos);
            eliminatedPawns.Add(redPawn.pawnId);
            eliminatedPawns.Add(bluePawn.pawnId);
            break; // Conflict resolved, stop processing
        }

        // Check for path crossing
        if (step > 0)
        {
            SVector2Int redPrevPos = (step - 1 < redPath.Count) ? redPath[step - 1] : redPawn.pos;
            SVector2Int bluePrevPos = (step - 1 < bluePath.Count) ? bluePath[step - 1] : bluePawn.pos;

            if (redCurrentPos == bluePrevPos && blueCurrentPos == redPrevPos)
            {
                // Pawns cross paths; conflict occurs at crossing point
                ResolveConflict(ref nextGameState, redPawn, bluePawn, redCurrentPos);
                eliminatedPawns.Add(redPawn.pawnId);
                eliminatedPawns.Add(bluePawn.pawnId);
                break; // Conflict resolved, stop processing
            }
        }
    }

    // No conflicts detected; update positions
    if (!eliminatedPawns.Contains(redPawn.pawnId))
    {
        UpdatePawnPosition(ref nextGameState, redPawn, redPath.Last());
    }

    if (!eliminatedPawns.Contains(bluePawn.pawnId))
    {
        UpdatePawnPosition(ref nextGameState, bluePawn, bluePath.Last());
    }

    return nextGameState;
}


    
    public static SPawn[] GenerateValidSetup(int player, SSetupParameters setupParameters)
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
    public string ToString()
    {
        return $"{pawn.pawnId} {pawn.def.pawnName} posChanged: {posChanged} isSetupChanged: {isSetupChanged} isAliveChanged: {isAliveChanged} hasMovedChanged: {hasMovedChanged} isVisibleToOpponentChanged: {isVisibleToOpponentChanged}";
    }
}
