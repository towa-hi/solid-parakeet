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
    }
}

public class GameServer
{
    readonly TcpListener listener;
    bool isRunning;

    readonly ConcurrentDictionary<Guid, ClientInfo> allClients;
    readonly ConcurrentDictionary<string, Lobby> allLobbies;

    // Add a cancellation token to gracefully stop the console command listener
    private CancellationTokenSource consoleCommandCancellation = new CancellationTokenSource();

    public GameServer(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        allClients = new ConcurrentDictionary<Guid, ClientInfo>();
        allLobbies = new ConcurrentDictionary<string, Lobby>();
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
        Console.WriteLine($"OnClientConnected {unregisteredClientInfo.ipAddress.ToString()} client started");
        try
        {
            // Expect client to register immediately after connecting
            (MessageType initialMessageType, byte[] initialData) = await MessageDeserializer.DeserializeMessageAsync(unregisteredClientInfo.stream);
            if (initialMessageType != MessageType.REGISTERCLIENT)
            {
                string errorMessage = $"OnClientConnected {unregisteredClientInfo.GetIdentifier()} Expected REGISTERCLIENT message type.";
                Console.WriteLine(errorMessage);
                await SendMessageAsync(unregisteredClientInfo, MessageType.SERVERERROR, new Response(false, 1, errorMessage));
                tcpClient.Close();
                return;
            }
            // Handle client registration
            await OnRegisterClient(unregisteredClientInfo, initialData);
            ClientInfo registeredClientInfo = unregisteredClientInfo;
            // Continue processing messages from the client
            while (isRunning && tcpClient.Connected)
            {
                try
                {
                    (MessageType type, byte[] data) = await MessageDeserializer.DeserializeMessageAsync(registeredClientInfo.stream);
                    await ProcessClientMessageAsync(registeredClientInfo, type, data);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"OnClientConnected {registeredClientInfo.GetIdentifier()} Error: {e.Message}");
                    break; // Exit on error
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"OnClientConnected {unregisteredClientInfo.ipAddress.ToString()} Error: {e.Message}");
        }
        finally
        {
            OnClientAbruptDisconnect(unregisteredClientInfo);
        }
    }

    void OnClientAbruptDisconnect(ClientInfo clientInfo)
    {
        Console.WriteLine($"OnClientAbruptDisconnect {clientInfo.GetIdentifier()} disconnected abruptly.");
        clientInfo.isConnected = false;
        allClients.TryRemove(clientInfo.clientId, out _);
        // // Start a timer to remove the client after 5 minutes if they don't reconnect
        // Task.Run(async () =>
        // {
        //     await Task.Delay(TimeSpan.FromSeconds(5));
        //     if (!clientInfo.isConnected)
        //     {
        //         if (allClients.TryRemove(clientInfo.clientId, out _))
        //         {
        //             Console.WriteLine($"Client '{clientInfo.GetIdentifier()}' has been deregistered after timeout.");
        //         }
        //     }
        // });
    }

    async Task ProcessClientMessageAsync(ClientInfo unregisteredClientInfo, MessageType messageType, byte[] data)
    {
        switch (messageType)
        {
            // Handle other message types as needed
            case MessageType.SERVERERROR:
                break;
            case MessageType.REGISTERCLIENT:
                break;
            case MessageType.REGISTERNICKNAME:
                await OnRegisterNickname(unregisteredClientInfo, data);
                break;
            case MessageType.GAMELOBBY:
                await OnGameLobby(unregisteredClientInfo, data);
                break;
            case MessageType.JOINGAMELOBBY:
            case MessageType.GAME:
            default:
                Console.WriteLine($"Unhandled message type: {messageType}");
                break;
        }
    }

    async Task OnRegisterClient(ClientInfo clientInfo, byte[] data)
    {
        Console.WriteLine($"OnRegisterClient unknown client started");
        string json = Encoding.UTF8.GetString(data);
        RegisterClientRequest registerClientRequest = JsonConvert.DeserializeObject<RegisterClientRequest>(json) ?? throw new InvalidOperationException();
        // Assign the clientId provided by client
        clientInfo.RegisterClient(registerClientRequest.clientId);
        // Check if a client with this clientId is already connected
        if (allClients.TryGetValue(clientInfo.clientId, out ClientInfo existingClient))
        {
            if (existingClient.isConnected)
            {
                // Handle the case where the clientId is already connected
                string errorMessage = $"OnRegisterClient {clientInfo.GetIdentifier()} clientId is already in use by another connected client.";
                Console.WriteLine(errorMessage);
                await SendMessageAsync(clientInfo, MessageType.REGISTERCLIENT, new Response(false, 2, errorMessage));
                clientInfo.tcpClient.Close();
            }
            else
            {
                // Reconnecting client
                existingClient.isConnected = true;
                existingClient.tcpClient = clientInfo.tcpClient;
                existingClient.stream = clientInfo.stream;
                existingClient.ipAddress = clientInfo.ipAddress;
                clientInfo = existingClient;
                string message = $"OnRegisterClient {clientInfo.GetIdentifier()} reconnected successfully.";
                Console.WriteLine(message);
                await SendMessageAsync(clientInfo, MessageType.REGISTERCLIENT, new Response(true, 1, message));
            }
        }
        else
        {
            // New client registration
            clientInfo.isRegistered = true;
            if (!allClients.TryAdd(clientInfo.clientId, clientInfo))
            {
                string errorMessage = $"OnRegisterClient {clientInfo.clientId} registration failed.";
                Console.WriteLine(errorMessage);
                await SendMessageAsync(clientInfo, MessageType.REGISTERCLIENT, new Response(false, 3, errorMessage));
                clientInfo.tcpClient.Close();
            }
            else
            {
                string message = $"OnRegisterClient {clientInfo.GetIdentifier()} registered successfully.";
                Console.WriteLine(message);
                await SendMessageAsync(clientInfo, MessageType.REGISTERCLIENT, new Response(true, 0, message));
            }
        }
    }

    async Task OnRegisterNickname(ClientInfo clientInfo, byte[] data)
    {
        Console.WriteLine($"OnRegisterNickname {clientInfo.GetIdentifier()} started");
        // Deserialize the registration data
        string json = Encoding.UTF8.GetString(data);
        Console.WriteLine($"OnRegisterNickname {clientInfo.GetIdentifier()} {json}");
        RegisterNicknameRequest registerNicknameRequest = JsonConvert.DeserializeObject<RegisterNicknameRequest>(json) ?? throw new InvalidOperationException();
        if (!IsNicknameValid(registerNicknameRequest.nickname))
        {
            string errorMessage = $"OnRegisterNickname {clientInfo.GetIdentifier()} invalid nickname {registerNicknameRequest.nickname}";
            Console.WriteLine(errorMessage);
            await SendMessageAsync(clientInfo, MessageType.REGISTERNICKNAME, new Response(false, 1, errorMessage));
            return;
        }
        clientInfo.nickname = registerNicknameRequest.nickname;
        string message = $"OnRegisterNickname {clientInfo.GetIdentifier()} set to {registerNicknameRequest.nickname}";
        Console.WriteLine(message);
        await SendMessageAsync(clientInfo, MessageType.REGISTERNICKNAME, new Response(true, 0, registerNicknameRequest.nickname));
    }

    async Task OnGameLobby(ClientInfo clientInfo, byte[] data)
    {
        Console.WriteLine($"OnGameLobby {clientInfo.GetIdentifier()} started");
        string json = Encoding.UTF8.GetString(data);
        Console.WriteLine($"OnGameLobby {clientInfo.GetIdentifier()} {json}");
        GameLobbyRequest? lobbyRequest = JsonConvert.DeserializeObject<GameLobbyRequest>(json);
        if (lobbyRequest == null)
        {
            string passwordCollisionMessage = $"OnGameLobby {clientInfo.GetIdentifier()} failed to read json {json}";
            await SendMessageAsync(clientInfo, MessageType.GAMELOBBY, new Response(false, 1, passwordCollisionMessage));
        }
        else
        {
            Lobby lobby = new Lobby(clientInfo.clientId, lobbyRequest.boardDef, lobbyRequest.gameMode);
            if (allLobbies.ContainsKey(lobby.password))
            {
                string passwordCollisionMessage = $"OnGameLobby {clientInfo.GetIdentifier()} already has a lobby with password {lobby.password}";
                await SendMessageAsync(clientInfo, MessageType.GAMELOBBY, new Response(false, 2, passwordCollisionMessage));
            }
            allLobbies[lobby.password] = lobby;
            await SendMessageAsync(clientInfo, MessageType.GAMELOBBY, new Response(true, 0, lobby.password));
        }
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

    ClientInfo GetClientInfo(Guid clientId)
    {
        return allClients.TryGetValue(clientId, out ClientInfo? info) ? info : null;
    }
}
