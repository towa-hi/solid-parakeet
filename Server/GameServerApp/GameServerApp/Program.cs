using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
        GameServer server = new GameServer(12345);
        server.Start();
        Console.ReadLine();
    }
}

public class GameServer
{
    readonly TcpListener listener;
    bool isRunning;

    readonly ConcurrentDictionary<Guid, ClientInfo> allClients;

    // Add a cancellation token to gracefully stop the console command listener
    private CancellationTokenSource consoleCommandCancellation = new CancellationTokenSource();

    public GameServer(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        allClients = new ConcurrentDictionary<Guid, ClientInfo>();
    }

    public void Start()
    {
        listener.Start();
        isRunning = true;
        Console.WriteLine("Game Server started.");
        ListenForClients();

        // Start the console command listener on a new thread
        Thread consoleThread = new Thread(ProcessConsoleCommands);
        consoleThread.Start();

        // Handle Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Cancel the default behavior
            Console.WriteLine("Shutting down the server...");
            Stop();
        };
    }

    public void Stop()
    {
        isRunning = false;
        listener.Stop();
        Console.WriteLine("Server stopped");

        foreach (var client in allClients.Values)
        {
            try
            {
                string errorMessage = $"Error: Server stopped.";
                Console.WriteLine(errorMessage);
                SendMessageAsync(client, MessageType.SERVERERROR, new Response(false, 0, errorMessage)).Wait();
                client.tcpClient.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error closing client connection: {e.Message}");
            }
        }
        allClients.Clear();

        // Signal the console thread to stop
        Console.WriteLine("Press ENTER to exit.");
    }

    async Task SendMessageAsync(ClientInfo clientInfo, MessageType type, Response response)
    {
        Console.WriteLine($"SendMessageAsync: '{clientInfo.ipAddress}' '{type.ToString()}' '{response.GetHeaderAsString()}'");
        string jsonResponse = JsonConvert.SerializeObject(response);
        byte[] data = Encoding.UTF8.GetBytes(jsonResponse);
        byte[] serializedMessage = MessageSerializer.SerializeMessage(type, data);
        try
        {
            await clientInfo.stream.WriteAsync(serializedMessage, 0, serializedMessage.Length);
            await clientInfo.stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to client '{clientInfo.clientId}': {ex.Message}");
        }
    }

    void ListenForClients()
    {
        Task.Run(async () =>
        {
            while (isRunning)
            {
                try
                {
                    TcpClient tcpClient = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"Client connected with ip: '{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString()}'");
                    _ = OnClientConnected(tcpClient);
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Console.WriteLine($"Exception in accepting client: {e.Message}");
                    }
                }
            }
        });
    }

    async Task OnClientConnected(TcpClient tcpClient)
    {
        ClientInfo unregisteredClientInfo = new ClientInfo(tcpClient);
        try
        {
            while (isRunning && tcpClient.Connected)
            {
                try
                {
                    (MessageType type, byte[] data) = await MessageDeserializer.DeserializeMessageAsync(unregisteredClientInfo.stream);
                    await ProcessClientMessageAsync(unregisteredClientInfo, type, data);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error reading from client '{unregisteredClientInfo.ipAddress}': {e.Message}");
                    break; // Exit on error
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception in OnClientConnected: {e.Message}");
        }
        finally
        {
            OnClientAbruptDisconnect(unregisteredClientInfo);
        }
    }

    void OnClientAbruptDisconnect(ClientInfo clientInfo)
    {
        Console.WriteLine($"Client '{clientInfo.GetIdentifier()}' disconnected abruptly.");
        clientInfo.isConnected = false;

        // Start a timer to remove the client after 5 minutes if they don't reconnect
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (!clientInfo.isConnected)
            {
                if (allClients.TryRemove(clientInfo.clientId, out _))
                {
                    Console.WriteLine($"Client '{clientInfo.GetIdentifier()}' has been deregistered after timeout.");
                }
            }
        });
    }

    async Task ProcessClientMessageAsync(ClientInfo unregisteredClientInfo, MessageType messageType, byte[] data)
    {
        switch (messageType)
        {
            // Handle other message types as needed
            case MessageType.SERVERERROR:
                break;
            case MessageType.REGISTERCLIENT:
                OnRegisterClient(unregisteredClientInfo, data);
                break;
            case MessageType.REGISTERNICKNAME:
                await OnRegisterNickname(data);
                break;
            case MessageType.CHANGENICKNAME:
            case MessageType.GAMELOBBY:
            case MessageType.JOINGAMELOBBY:
            case MessageType.GAME:
            default:
                Console.WriteLine($"Unhandled message type: {messageType}");
                break;
        }
    }

    void OnRegisterClient(ClientInfo unregisteredClient, byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        RegisterClientRequest registerClientRequest = JsonConvert.DeserializeObject<RegisterClientRequest>(json) ?? throw new InvalidOperationException();
        if (allClients.ContainsKey(registerClientRequest.clientId))
        {
            // client is already registered
            return;
        }
        unregisteredClient.RegisterClient(registerClientRequest.clientId);
        allClients[registerClientRequest.clientId] = unregisteredClient;
        string ackMessage = $"Client '{unregisteredClient.GetIdentifier()}' registered.";
        Console.WriteLine(ackMessage);
    }

    async Task OnRegisterNickname(byte[] data)
    {
        // Deserialize the registration data
        string json = Encoding.UTF8.GetString(data);
        Console.WriteLine("OnRegisterNickname: " + json);
        RegisterNicknameRequest registerNicknameRequest = JsonConvert.DeserializeObject<RegisterNicknameRequest>(json) ?? throw new InvalidOperationException();
        ClientInfo clientInfo = allClients[registerNicknameRequest.clientId];
        if (!IsNicknameValid(registerNicknameRequest.nickname))
        {
            await SendMessageAsync(clientInfo, MessageType.SERVERERROR, new Response(false, 1, "Invalid nickname."));
            clientInfo.tcpClient.Close();
            return;
        }
        //if clients list already has id check if same person and reregister
        if (allClients.ContainsKey(registerNicknameRequest.clientId))
        {
            
        }
        else
        {
            
        }
        if (registerNicknameRequest.clientId == Guid.Empty || !allClients.TryGetValue(registerNicknameRequest.clientId, out clientInfo))
        {
            // New client registration
            clientInfo.isRegistered = true;
            clientInfo.nickname = registerNicknameRequest.nickname;
            clientInfo.clientId = registerNicknameRequest.clientId != Guid.Empty ? registerNicknameRequest.clientId : Guid.NewGuid();
            if (allClients.TryAdd(clientInfo.clientId, clientInfo))
            {
                Console.WriteLine($"New client registered: {clientInfo.GetIdentifier()}");
            }
            else
            {
                Console.WriteLine($"Failed to add client {clientInfo.clientId} to the client list.");
                await SendMessageAsync(clientInfo, MessageType.SERVERERROR, new Response(false, 2, "Failed to register client."));
                clientInfo.tcpClient.Close();
                return;
            }
        }
        else
        {
            // Existing client reconnecting
            clientInfo.isConnected = true;
            clientInfo.nickname = registerNicknameRequest.nickname;
            Console.WriteLine($"Client reconnected: {clientInfo.GetIdentifier()}");
        }

        // Send acknowledgment to the client
        string ackMessage = $"Client '{clientInfo.nickname}' connected successfully.";
        await SendMessageAsync(clientInfo, MessageType.REGISTERNICKNAME, new Response(true, 0, ackMessage));
            
    }
    
    const int ALIASMAXLENGTH = 20;
    static bool IsNicknameValid(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }
        if (alias.Length > ALIASMAXLENGTH)
        {
            return false;
        }
        return true;
    }

    private void ProcessConsoleCommands()
    {
        Console.WriteLine("Console Command Listener started. Type 'help' for a list of commands.");

        while (isRunning)
        {
            Console.Write("> ");
            string input = null;

            try
            {
                input = Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading console input: {ex.Message}");
                continue;
            }

            if (input == null)
                continue;

            string command = input.Trim().ToLower();

            try
            {
                switch (command)
                {
                    case "list":
                        ListConnectedClients();
                        break;
                    case "help":
                        ShowHelp();
                        break;
                    case "exit":
                        Console.WriteLine("Shutting down the server...");
                        Stop();
                        break;
                    default:
                        Console.WriteLine($"Unknown command: '{command}'. Type 'help' for a list of commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing command '{command}': {ex.Message}");
            }
        }

        Console.WriteLine("Console Command Listener stopped.");
    }

    private void ListConnectedClients()
    {
        try
        {
            if (allClients.IsEmpty)
            {
                Console.WriteLine("No clients are currently connected.");
                return;
            }

            Console.WriteLine("Connected Clients:");
            Console.WriteLine("-------------------");
            foreach (var client in allClients.Values)
            {
                string status = client.isConnected ? "Connected" : "Disconnected";
                Console.WriteLine($"Client ID: {client.clientId}");
                Console.WriteLine($"Nickname: {client.nickname}");
                Console.WriteLine($"IP Address: {client.ipAddress}");
                Console.WriteLine($"Status: {status}");
                Console.WriteLine("-------------------");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing connected clients: {ex.Message}");
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  list  - Lists all connected clients.");
        Console.WriteLine("  help  - Shows this help message.");
        Console.WriteLine("  exit  - Shuts down the server.");
    }
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
    public bool isInSession;

    public ClientInfo(TcpClient inTcpClient)
    {
        isConnected = true;
        clientId = Guid.Empty;
        isRegistered = false;
        tcpClient = inTcpClient;
        stream = tcpClient.GetStream();
        ipAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;
        isInSession = false;
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

public class RegisterClientRequest
{
    public Guid clientId { get; set; }
}

public class RegisterNicknameRequest
{
    public Guid clientId { get; set; }
    public string nickname { get; set; }
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

    public Response(bool inSuccess, int inResponseCode, object inData)
    {
        success = inSuccess;
        responseCode = inResponseCode;
        data = inData;
    }

    public string GetHeaderAsString()
    {
        return $"'{success}' '{responseCode}'";
    }
}

