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
    public static Vector2Int pugatory = new(-666, -666);
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
        return orderedPawns;
    }

    public static PawnDef GetPawnDefFromName(string name)
    {
        // NOTE: VERY DANGEROUS
        PawnDef pawnDef = Resources.Load<PawnDef>($"Pawn/{name}");
        return pawnDef;
    }
    
    public static Dictionary<PawnDef, int> GetUnorderedPawnDefDict()
    {
        Dictionary<PawnDef, int> unorderedPawnDefDict = new();
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Flag"), 1);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Spy"), 1);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Bomb"), 6);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Marshal"), 1);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/General"), 1);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Colonel"), 2);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Major"), 3);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Captain"), 4);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Lieutenant"), 4);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Sergeant"), 4);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Miner"), 5);
        unorderedPawnDefDict.Add(Resources.Load<PawnDef>("Pawn/Scout"), 8);
        return unorderedPawnDefDict;
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
}



public class SetupParameters
{
    public Dictionary<PawnDef, int> maxPawnsDict;
    public BoardDef board;
    public Player player;
    
}

public class SSetupParameters
{
    public int player;
    public SBoardDef board;
    public SSetupPawnData[] maxPawnsDict;
    
    public SSetupParameters(SetupParameters setupParameters)
    {
        player = (int)setupParameters.player;
        board = new SBoardDef(setupParameters.board);
        maxPawnsDict = new SSetupPawnData[setupParameters.maxPawnsDict.Count];
        int index = 0;
        foreach ((PawnDef pawnDef, int max) in setupParameters.maxPawnsDict)
        {
            maxPawnsDict[index] = new SSetupPawnData
            {
                pawnDef = new SPawnDef(pawnDef),
                maxPawns = max,
            };
            index++;
        }
    }

    public SetupParameters ToUnity()
    {
        SetupParameters setupParameters = new()
        {
            player = (Player)player,
            board = board.ToUnity(),
            maxPawnsDict = new Dictionary<PawnDef, int>(),
        };
        foreach (SSetupPawnData setupPawnData in maxPawnsDict)
        {
            setupParameters.maxPawnsDict.Add(setupPawnData.pawnDef.ToUnity(), setupPawnData.maxPawns);
        }
        return setupParameters;
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
    Task SendSetupSubmissionRequest(List<SPawn> setupPawnList);
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
    public List<SPawn> pawns;
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
                break;

            // Get the tile at the current position
            STile? maybeTile = GetTileFromPos(new SVector2Int(currentPos));
            if (!maybeTile.HasValue)
                break;

            STile tile = maybeTile.Value;

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
                        if (!enemyEncountered)
                        {
                            enemyEncountered = true;
                        }
                        else
                        {
                            break; // Can't move past second enemy
                        }
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

    public SGameState Censor(int targetPlayer)
    {
        if (player != (int)Player.NONE)
        {
            throw new Exception("Censor can only be done on master game states!");
        }

        SGameState censoredGameState = new SGameState
        {
            player = targetPlayer,
            boardDef = boardDef,
        };
        SPawn[] censoredPawns = new SPawn[pawns.Length];
        for (int i = 0; i < pawns.Length; i++)
        {
            SPawn serverPawn = pawns[i];
            SPawn censoredPawn;
            if (serverPawn.player != targetPlayer && !serverPawn.isVisibleToOpponent)
            {
                censoredPawn = serverPawn.Censor();
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
}

public struct PawnChanges
{
    public Pawn pawn;
    public bool posChanged;
    public bool isSetupChanged;
    public bool isAliveChanged;
    public bool hasMovedChanged;
    public bool isVisibleToOpponentChanged;
    
}
