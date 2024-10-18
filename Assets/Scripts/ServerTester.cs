using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.UI;

public class ServerTester : MonoBehaviour
{
    public Button ConnectButton;
    public Button SendNickButton;
    public Button StartLobbyButton;
    
    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected = false;

    // Replace with your server's IP and port
    private string serverIP = "127.0.0.1"; // Assuming the server is running locally
    private int serverPort = 12345;

    // Store the client ID to handle reconnections
    private Guid clientId;

    // TaskCompletionSources for synchronization
    private TaskCompletionSource<Response> acknowledgmentReceived = new TaskCompletionSource<Response>();

    void Awake()
    {
        ConnectButton.onClick.AddListener(OnConnectButton);
        SendNickButton.onClick.AddListener(OnSendNickButton);
        StartLobbyButton.onClick.AddListener(OnStartLobbyButton);
    }

    public void OnConnectButton()
    {
        LoadOrGenerateClientId();
        ConnectToServer();
    }

    public void OnSendNickButton()
    {
        RegisterNickname("hi lad");
    }

    public void OnStartLobbyButton()
    {
        
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
            await RegisterClient();
            
            _ = ReadResponsesFromServer();
            
            

        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to server: {e.Message}");
            HandleServerDisconnection();
        }
    }

    async Task RegisterClient()
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
    
    async Task RegisterNickname(string nickname)
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

                switch (messageType)
                {
                    case MessageType.REGISTERCLIENT:
                        // Handle acknowledgment
                        string jsonAck = Encoding.UTF8.GetString(data);
                        Debug.Log(jsonAck);
                        Response responseAck = JsonConvert.DeserializeObject<Response>(jsonAck);
                        // Signal that acknowledgment is received
                        acknowledgmentReceived.TrySetResult(responseAck);
                        break;

                    case MessageType.SERVERERROR:
                        // Handle server error messages
                        string jsonError = Encoding.UTF8.GetString(data);
                        Debug.Log(jsonError);
                        Response responseError = JsonConvert.DeserializeObject<Response>(jsonError);
                        Debug.LogError($"Server error: {responseError.data}");
                        HandleServerDisconnection();
                        break;

                    default:
                        Debug.Log($"Received message of type {messageType}");
                        break;
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
    SERVERERROR, // only called when error is server fault
    REGISTERCLIENT, // response only, just an ack 
    REGISTERNICKNAME, // request registration data
    CHANGENICKNAME,
    GAMELOBBY, // request has password
    JOINGAMELOBBY, // request has password
    GAME, // request holds piece deployment or move data, response is a gamestate object
}

public class Response
{
    public bool success;
    public int responseCode;
    public object data;
}

public class Request
{
    public MessageType messageType;
    public object data;
}

public struct RegisterClientRequest
{
    public Guid clientId { get; set; }
}

public struct RegisterNicknameRequest
{
    public Guid clientId { get; set; }
    public string nickname { get; set; }
}

public struct NewGameRequest
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
