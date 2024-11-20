using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

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
    GAME, // request holds piece deployment or move data, response is a gamestate object
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
    public bool hostReady;
    public bool guestReady;
    public SLobby() { }

    public bool IsHost(Guid clientId)
    {
        return clientId == hostId;
    }
}

[Serializable]
public class SBoardDef
{
    public string boardName;
    public SVector2Int boardSize;
    public STile[] tiles;

    public SBoardDef() { }

    public SBoardDef(BoardDef boardDef)
    {
        boardName = boardDef.boardName;
        boardSize = new SVector2Int(boardDef.boardSize);
        tiles = new STile[boardDef.tiles.Length];
        tiles = boardDef.tiles.Select(tile => new STile(tile)).ToArray();
    }
    public BoardDef ToUnity()
    {
        BoardDef boardDef = ScriptableObject.CreateInstance<BoardDef>();
        boardDef.boardName = boardName;
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

[Serializable]
public class SPawnDef
{
    public string pawnName;
    public int power;
    
    // figure out how to link this to pawndef later
}

public class SetupParameters
{
    public Dictionary<PawnDef, int> maxPawnsDict;
    public BoardDef board;
    
    public SetupParameters(SSetupParameters serialized)
    {
        board = serialized.board.ToUnity();
        maxPawnsDict = new();
        foreach (SSetupPawnData data in serialized.setupPawnDatas)
        {
            PawnDef pawnDef = Globals.GetPawnDefFromName(data.pawnName);
            maxPawnsDict.Add(pawnDef, data.maxPawns);
        }
    }
}

public class SSetupParameters
{
    public List<SSetupPawnData> setupPawnDatas;
    public SBoardDef board;
    public Player player;
    
    public SSetupParameters(Player inPlayer, SBoardDef inBoard)
    {
        setupPawnDatas = new();
        Dictionary<PawnDef, int> maxPawnsDict = Globals.GetUnorderedPawnDefDict();
        foreach ((PawnDef pawnDef, int max) in maxPawnsDict)
        {
            SSetupPawnData setupPawnData = new()
            {
                pawnName = pawnDef.pawnName,
                maxPawns = max,
            };
            setupPawnDatas.Add(setupPawnData);
        }
        player = inPlayer;
        board = inBoard;
    }
}

public class SSetupPawnData
{
    public string pawnName;
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
    event Action<Response<SSetupParameters>> OnDemoStarted;
    event Action OnLobbyResponse;
    
    // Methods
    Task ConnectToServer();
    Task SendRegisterNickname(string nicknameInput);
    Task SendGameLobby();
    Task SendGameLobbyLeaveRequest();
    Task SendGameLobbyReadyRequest(bool ready);
    Task StartGameDemoRequest();
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

[Serializable]
public class QueuedMove
{
    public Pawn pawn;
    public Vector2Int pos;

    public QueuedMove(Pawn inPawn, Vector2Int inPos)
    {
        pawn = inPawn;
        pos = inPos;
    }
}