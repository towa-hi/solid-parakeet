using System.Net;
using System.Net.Sockets;

public static class Globals
{
    public static Random random = new Random();
    
    
    public static string GeneratePassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[Globals.random.Next(s.Length)]).ToArray());
    }
}

public static class MessageSerializer
{
    public static byte[] SerializeMessage(MessageType type, byte[] data)
    {
        using MemoryStream ms = new MemoryStream();
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

public static class MessageDeserializer
{
    public static async Task<(MessageType, byte[])> DeserializeMessageAsync(NetworkStream stream)
    {
        byte[] header = new byte[8];
        int bytesRead = 0;
        while (bytesRead < 8)
        {
            int read = await stream.ReadAsync(header, bytesRead, 8 - bytesRead);
            if (read == 0)
            {
                throw new Exception("Disconnected");
            }
            bytesRead += read;
        }
        MessageType type = (MessageType)BitConverter.ToUInt32(header, 0);
        uint length = BitConverter.ToUInt32(header, 4);
        byte[] data = new byte[length];
        bytesRead = 0;
        while (bytesRead < length)
        {
            int read = await stream.ReadAsync(data, bytesRead, (int)(length - bytesRead));
            if (read == 0)
            {
                throw new Exception("Disconnected");
            }
            bytesRead += read;
        }
        return (type, data);
    }
}

public enum MessageType : uint
{
    SERVERERROR, // only called when error is server fault
    REGISTERCLIENT, // response only, just an ack 
    REGISTERNICKNAME, // request registration data
    GAMELOBBY, // request has password
    LEAVEGAMELOBBY,
    JOINGAMELOBBY, // request has password
    READYLOBBY,
    GAMESTART,
    GAME, // request holds piece deployment or move data, response is a gamestate object
    
}


public class ClientInfo
{
    public bool isConnected;
    public Guid clientId;
    public bool isRegistered;
    public string nickname = "";
    public TcpClient tcpClient;
    public NetworkStream stream;
    public IPAddress ipAddress;
    public Guid? lobbyId;

    public ClientInfo(TcpClient inTcpClient)
    {
        isConnected = true;
        clientId = Guid.Empty;
        isRegistered = false;
        tcpClient = inTcpClient;
        stream = tcpClient.GetStream();
        ipAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;
        lobbyId = null;
    }

    public void RegisterClient(Guid inClientId)
    {
        if (isRegistered)
        {
            throw new Exception("Client is already registered");
        }

        if (inClientId == Guid.Empty)
        {
            throw new Exception("ClientId cannot be empty");
        }
        clientId = inClientId;
        isRegistered = true;
    }
    public string GetIdentifier()
    {
        return $"id: '{clientId.ToString()}' alias: '{nickname}', ip: '{ipAddress}'";
    }
}

// networking stuff start

public class ResponseBase
{
    public Guid requestId { get; set; }
    public bool success { get; set; }
    public int responseCode { get; set; }
    public string message { get; set; }
}

public class Response<T> : ResponseBase
{
    public T data { get; set; }

    public Response(Guid inRequestId, bool inSuccess, int inResponseCode, T inData)
    {
        requestId = inRequestId;
        success = inSuccess;
        responseCode = inResponseCode;
        data = inData;
    }

    public string GetHeaderAsString()
    {
        return $"'{success}' '{responseCode}'";
    }
}

public class RequestBase
{
    public MessageType messageType { get; set; }
    public Guid requestId { get; set; }
    public Guid clientId { get; set; }
}

public class RegisterClientRequest : RequestBase
{
    public RegisterClientRequest() { }
}

public class RegisterNicknameRequest : RequestBase
{
    public string nickname { get; set; }
    
    public RegisterNicknameRequest() { }
}

public class GameLobbyRequest : RequestBase
{
    public int gameMode { get; set; }
    public SBoardDef sBoardDef { get; set; }
    
    public GameLobbyRequest() { }
}

public class LeaveGameLobbyRequest : RequestBase
{
    public LeaveGameLobbyRequest() { }
}

public class JoinGameLobbyRequest : RequestBase
{
    public JoinGameLobbyRequest() { }
}

public class ReadyGameLobbyRequest : RequestBase
{
    public bool ready { set; get; }
    
    public ReadyGameLobbyRequest() { }
}


// networking stuff end

// gameplay stuff start


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
}

[Serializable]
public class SBoardDef
{
    public string boardName;
    public SVector2Int boardSize;
    public STile[] tiles;
}


[Serializable]
public class SVector2Int
{
    public int x;
    public int y;
}

[Serializable]
public class STile
{
    public SVector2Int pos;
    public bool isPassable;
    public int setupPlayer;
    
}
