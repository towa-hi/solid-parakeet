using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        GameServer server = new GameServer(12345);
        server.Start();
        Console.WriteLine("Press ENTER to stop the server.");
        Console.ReadLine();
        server.Stop();
    }
}

public class GameServer
{
    readonly TcpListener listener;
    bool isRunning;

    readonly ConcurrentDictionary<string, ClientInfo> clients;
    readonly ConcurrentDictionary<Guid, GameSession> activeGames;

    public GameServer(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        clients = new ConcurrentDictionary<string, ClientInfo>();
        activeGames = new ConcurrentDictionary<Guid, GameSession>();
    }

    public void Start()
    {
        listener.Start();
        isRunning = true;
        Console.WriteLine("Game Server started.");
        ListenForClients();
    }

    public void Stop()
    {
        isRunning = false;
        listener.Stop();
        Console.WriteLine("Server stopped");
        foreach (var client in clients.Values)
        {
            try
            {
                SendErrorAsync(client.stream, "Server is shutting down").Wait();
                client.tcpClient.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        clients.Clear();
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
                    Console.WriteLine("Client connected");
                    _ = HandleClientAsync(tcpClient);
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
    
    async Task HandleClientAsync(TcpClient tcpClient)
    {
        NetworkStream stream = tcpClient.GetStream();
        string alias = null;
        try
        {
            // get client alias
            (MessageType type, byte[] data) = await MessageDeserializer.DeserializeMessageAsync(stream);
            if (type != MessageType.ALIAS)
            {
                await SendErrorAsync(stream, "First message must be Alias.");
                tcpClient.Close();
                return;
            }

            alias = Encoding.UTF8.GetString(data).Trim();
            Console.WriteLine($"Received alias: {alias}");

            // Step 2: Validate the alias
            if (string.IsNullOrWhiteSpace(alias) || clients.ContainsKey(alias))
            {
                await SendErrorAsync(stream, "Invalid or duplicate alias.");
                Console.WriteLine($"Alias '{alias}' is invalid or already in use. Disconnecting client.");
                tcpClient.Close();
                return;
            }

            // Step 3: Add the client to the clients dictionary
            ClientInfo clientInfo = new ClientInfo
            {
                alias = alias,
                tcpClient = tcpClient,
                stream = stream,
                ipAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address
            };

            if (!clients.TryAdd(alias, clientInfo))
            {
                // Failed to add client (shouldn't happen due to prior check)
                await SendErrorAsync(stream, "Failed to register alias.");
                Console.WriteLine($"Failed to add client with alias '{alias}'. Disconnecting.");
                tcpClient.Close();
                return;
            }

            Console.WriteLine($"Client '{alias}' registered successfully.");

            // Step 4: Send a welcome message to the client
            string welcomeMessage = $"Welcome {alias}!";
            await SendMessageAsync(stream, MessageType.WELCOME, welcomeMessage);

            // Step 5: Continuously listen for client messages
            while (isRunning && tcpClient.Connected)
            {
                try
                {
                    var (msgType, msgData) = await MessageDeserializer.DeserializeMessageAsync(stream);
                    Console.WriteLine($"Received from '{alias}': Type={msgType}, Data Length={msgData.Length}");

                    // Process the received message based on its type
                    await ProcessClientMessageAsync(clientInfo, msgType, msgData);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error reading from client '{alias}': {e.Message}");
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception handling client '{alias}': {e.Message}");
            throw;
        }
        finally
        {
            // Step 6: Clean up after client disconnects
            if (!string.IsNullOrEmpty(alias))
            {
                if (clients.TryRemove(alias, out ClientInfo removedClient))
                {
                    Console.WriteLine($"Client '{alias}' removed from active clients.");
                    // Optionally, handle game session termination or reassignment
                }
            }
            // Ensure the client is properly closed
            if (tcpClient.Connected)
            {
                tcpClient.Close();
            }
            Console.WriteLine($"Connection with client '{alias}' closed.");
        }
    }

    public async Task SendMessageAsync(NetworkStream stream, MessageType type, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        byte[] serializedMessage = MessageSerializer.SerializeMessage(type, data);
        try
        {
            await stream.WriteAsync(serializedMessage, 0, serializedMessage.Length);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }
    
    // Overload for byte array data
    public async Task SendMessageAsync(NetworkStream stream, MessageType type, byte[] data)
    {
        byte[] serializedMessage = MessageSerializer.SerializeMessage(type, data);
        try
        {
            await stream.WriteAsync(serializedMessage, 0, serializedMessage.Length);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }
    
    public async Task SendErrorAsync(NetworkStream stream, string errorMessage)
    {
        await SendMessageAsync(stream, MessageType.ERROR, errorMessage);
    }
    
    // Method to process client messages based on message type
    public async Task ProcessClientMessageAsync(ClientInfo client, MessageType type, byte[] data)
    {
        switch (type)
        {
            case MessageType.ECHO:
                // Echo the message back to the client
                string echoMessage = Encoding.UTF8.GetString(data);
                await SendMessageAsync(client.stream, MessageType.ECHO, echoMessage);
                break;

            case MessageType.MOVE:
                // Process the move
                string move = Encoding.UTF8.GetString(data).Trim();
                Console.WriteLine($"Processing move from '{client.alias}': {move}");
                // Implement your move processing logic here

                // For demonstration, echo back the move
                await SendMessageAsync(client.stream, MessageType.MOVE, move);
                break;

            case MessageType.ALIAS:
            case MessageType.WELCOME:
            case MessageType.ERROR:
            case MessageType.UPDATE:
            default:
                Console.WriteLine($"Unknown message type from '{client.alias}': {type}");
                await SendErrorAsync(client.stream, "Unknown message type.");
                break;
        }
    }
}

public class ClientInfo
{
    public string alias;
    public TcpClient tcpClient;
    public NetworkStream stream;
    public IPAddress ipAddress;
    
}

class GameSession
{
    
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

public enum MessageType : uint
{
    ALIAS = 1,          // Client sends their alias upon connection
    WELCOME = 2,        // Server sends a welcome message
    ECHO = 3,           // Server echoes a message
    MOVE = 4,           // Client sends a move
    ERROR = 5,          // Server sends an error message
    UPDATE = 6          // Server sends game state updates
}


