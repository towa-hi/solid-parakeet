using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.UI;

public class ServerTester : MonoBehaviour
{

    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected = false;

    public bool isNicknameRegistered = false;
    // Replace with your server's IP and port
    private string serverIP = "127.0.0.1"; // Assuming the server is running locally
    private int serverPort = 12345;

    // Store the client ID to handle reconnections
    private Guid clientId;

    public string nickname;
    


    public void OnConnectButton()
    {
        LoadOrGenerateClientId();
        ConnectToServer();
    }

    public void OnSendNickButton()
    {
        string testNickname = clientId.ToString().Substring(0, 4) + nickname;
        _ = SendRegisterNickname(testNickname);
    }
    
    public void OnStartLobbyButton()
    {
        _ = SendGameLobby();
    }

    void LoadOrGenerateClientId()
    {
        if (PlayerPrefs.HasKey("ClientId"))
        {
            string clientIdStr = PlayerPrefs.GetString("ClientId");
            if (Guid.TryParse(clientIdStr, out Guid parsedId))
            {
                clientId = parsedId;
                Debug.Log($"Loaded existing ClientId: {clientId}");
            }
            else
            {
                clientId = Guid.NewGuid();
                PlayerPrefs.SetString("ClientId", clientId.ToString());
                PlayerPrefs.Save();
                Debug.Log($"Generated new ClientId (invalid stored ID was replaced): {clientId}");
            }
        }
        else
        {
            clientId = Guid.NewGuid();
            PlayerPrefs.SetString("ClientId", clientId.ToString());
            PlayerPrefs.Save();
            Debug.Log($"Generated new ClientId: {clientId}");
        }
    }

    async void ConnectToServer()
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(serverIP, serverPort);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("Connected to server.");
            // Start reading messages from the server
            await SendRegisterClient();
            
            _ = ReadResponsesFromServer();
            
            

        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to server: {e.Message}");
            HandleServerDisconnection();
        }
    }

    async Task SendRegisterClient()
    {
        try
        {
            RegisterClientRequest registerClientRequest = new RegisterClientRequest
            {
                clientId = clientId
            };
            await SendRequestToServer(MessageType.REGISTERCLIENT, registerClientRequest);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    async Task SendRegisterNickname(string nickname)
    {
        try
        {
            RegisterNicknameRequest registerNicknameRequest = new RegisterNicknameRequest
            {
                nickname = nickname,
                clientId = clientId
            };
            await SendRequestToServer(MessageType.REGISTERNICKNAME, registerNicknameRequest);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error registering nickname: {e.Message}");
            //HandleServerDisconnection();
        }
    }

    async Task SendGameLobby()
    {
        GameLobbyRequest gameLobbyRequest = new GameLobbyRequest
        {
            clientId = clientId,
            gameMode = 0,
            sBoardDef = new SBoardDef(GameManager.instance.tempBoardDef)
        };
        await SendRequestToServer(MessageType.GAMELOBBY, gameLobbyRequest);
    }
    
    async Task SendRequestToServer(MessageType messageType, object objectData)
    {
        if (isConnected)
        {
            try
            {
                string json = JsonConvert.SerializeObject(objectData);
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] message = MessageSerializer.SerializeMessage(messageType, data);
                await stream.WriteAsync(message, 0, message.Length);
                await stream.FlushAsync();
                Debug.Log($"Sent: '{json}'");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
    
    async Task ReadResponsesFromServer()
    {
        while (isConnected)
        {
            try
            {
                var (messageType, data) = await MessageDeserializer.DeserializeMessageAsync(stream);
                string jsonResponse = Encoding.UTF8.GetString(data);

                switch (messageType)
                {
                    case MessageType.REGISTERCLIENT:
                        {
                            var response = JsonConvert.DeserializeObject<Response<string>>(jsonResponse);
                            break;
                        }
                    case MessageType.SERVERERROR:
                        {
                            var response = JsonConvert.DeserializeObject<Response<string>>(jsonResponse);
                            Debug.LogError($"Server error: {response.data}");
                            HandleServerDisconnection();
                            break;
                        }
                    case MessageType.REGISTERNICKNAME:
                        {
                            var response = JsonConvert.DeserializeObject<Response<string>>(jsonResponse);
                            if (response.success)
                            {
                                Debug.Log("Nickname set to " + response.data);
                                nickname = response.data;
                                isNicknameRegistered = true;
                            }
                            else
                            {
                                Debug.LogError("Server rejected nickname with response code " + response.responseCode);
                                isNicknameRegistered = false;
                            }
                            break;
                        }
                    case MessageType.GAMELOBBY:
                        {
                            Debug.Log($"Deserializing GAMELOBBY message: {jsonResponse}");
                            var response = JsonConvert.DeserializeObject<Response<SLobby>>(jsonResponse);
                            if (response != null && response.success)
                            {
                                SLobby sLobby = response.data;

                                BoardDef boardDef = sLobby.sBoardDef.ToUnity();
                                // TODO: do something with boardDef
                            }
                            else if (response != null)
                            {
                                Debug.LogError($"Server rejected lobby with response code {response.responseCode}");
                            }
                            else
                            {
                                Debug.LogError("Failed to deserialize GameLobbyResponse.");
                            }
                            break;
                        }
                    default:
                        {
                            Debug.Log($"Received message of type {messageType}");
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading from server: {e.Message}");
                HandleServerDisconnection();
            }
        }
    }

    void HandleServerDisconnection()
    {
        if (isConnected)
        {
            isConnected = false;
            Debug.LogWarning("Disconnected from server.");

            // Clean up resources
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }

            if (client != null)
            {
                client.Close();
                client = null;
            }

            // Notify the user
            Debug.LogWarning("Server connection lost. Please check your network connection or try reconnecting.");
        }
    }

    private void OnApplicationQuit()
    {
        isConnected = false;
        if (stream != null)
        {
            stream.Close();
            stream = null;
        }
        if (client != null)
        {
            client.Close();
            client = null;
        }
    }
}

// Additional classes and enums (should match your server code)

public enum MessageType : uint
{
    SERVERERROR, // only called when error is server fault, disconnects the client forcibly
    REGISTERCLIENT, // request: clientId only, response: none 
    REGISTERNICKNAME, // request: nickname, response: success
    GAMELOBBY, // request: lobby parameters, response: lobby
    JOINGAMELOBBY, // request: password, response: lobby 
    GAME, // request holds piece deployment or move data, response is a gamestate object
}

public class Response<T>
{
    public bool success;
    public int responseCode;
    public T data;
    
    public Response(bool inSuccess, int inResponseCode, T inData)
    {
        success = inSuccess;
        responseCode = inResponseCode;
        data = inData;
    }

    public string GetHeaderAsString()
    {
        return $"'{success}' '{responseCode}'";
    }

    public static Response<T> StringDataToResponse(byte[] data)
    {
        string jsonResponse = Encoding.UTF8.GetString(data);
        Response<T> response = JsonConvert.DeserializeObject<Response<T>>(jsonResponse);
        return response;
    }
}

public class Request
{
    public MessageType messageType;
    public object data;
}

public class RegisterClientRequest
{
    public Guid clientId { get; set; }
}

public class RegisterNicknameRequest
{
    public Guid clientId { get; set; }
    public string nickname { get; set; }
}

public class GameLobbyRequest
{
    public Guid clientId { get; set; }
    public int gameMode { get; set; }
    public SBoardDef sBoardDef { get; set; }
}


public class NewGameRequest
{
    public Guid clientId { get; set; }
    public int gameMode { get; set; }
}


public static class MessageSerializer
{
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
}
