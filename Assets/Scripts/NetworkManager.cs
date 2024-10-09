
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class NetworkManager
{
    string serverIP = "127.0.0.1";
    int serverPort = 12345;

    TcpClient client;
    NetworkStream stream;

    bool isConnected = false;

    ConcurrentQueue<(MessageType, byte[])> messageQueue = new ConcurrentQueue<(MessageType, byte[])>();

    public event Action<string> OnWelcomeReceived;
    public event Action<string> OnEchoReceived;
    public event Action<string> OnMoveReceived;
    public event Action<string> OnErrorReceived;
    public event Action<string> OnUpdateReceived;
    
    CancellationTokenSource cts;

    public NetworkManager()
    {
        
    }

    public async Task ConnectToServerAsync(string inServerIP, int inServerPort, string alias)
    {
        serverIP = inServerIP;
        serverPort = inServerPort;

        try
        {
            client = new TcpClient();
            await client.ConnectAsync(serverIP, serverPort);
            stream = client.GetStream();
            isConnected = true;
            Console.WriteLine("Connected to server.");

            // Send the alias as the first message
            await SendMessageAsync(MessageType.ALIAS, alias);

            // Start the receive loop
            cts = new CancellationTokenSource();
            _ = ReceiveDataAsync(cts.Token);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Connection error: {e.Message}");
            OnErrorReceived?.Invoke("Connection error: " + e.Message);
            throw;
        }
    }
    public void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            cts.Cancel();
            stream?.Close();
            client?.Close();
            Console.WriteLine("Disconnected from server.");
        }
    }

    public async Task SendMessageAsync(MessageType type, string message)
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
    public async Task SendMessageAsync(MessageType type, byte[] data)
    {
        byte[] message = MessageSerializer.SerializeMessage(type, data);
        try
        {
            await stream.WriteAsync(message, 0, message.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }
    
    private async Task ReceiveDataAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && isConnected)
            {
                var (type, data) = await MessageDeserializer.DeserializeMessageAsync(stream);
                messageQueue.Enqueue((type, data));
                ProcessIncomingMessages();
            }
        }
        catch (Exception e)
        {
            if (isConnected)
            {
                Console.WriteLine($"Receive error: {e.Message}");
                OnErrorReceived?.Invoke("Receive error: " + e.Message);
                Disconnect();
            }
        }
    }

    public void ProcessIncomingMessages()
    {
        while (messageQueue.TryDequeue(out var message))
        {
            var (type, data) = message;
            switch (type)
            {
                case MessageType.WELCOME:
                    string welcome = Encoding.UTF8.GetString(data);
                    Console.WriteLine("Server: " + welcome);
                    OnWelcomeReceived?.Invoke(welcome);
                    break;

                case MessageType.ECHO:
                    string echo = Encoding.UTF8.GetString(data);
                    Console.WriteLine("Echo from server: " + echo);
                    OnEchoReceived?.Invoke(echo);
                    break;

                case MessageType.MOVE:
                    string move = Encoding.UTF8.GetString(data);
                    Console.WriteLine("Move received: " + move);
                    OnMoveReceived?.Invoke(move);
                    break;

                case MessageType.ERROR:
                    string error = Encoding.UTF8.GetString(data);
                    Console.WriteLine("Error from server: " + error);
                    OnErrorReceived?.Invoke(error);
                    break;

                case MessageType.UPDATE:
                    string update = Encoding.UTF8.GetString(data);
                    Console.WriteLine("Update from server: " + update);
                    OnErrorReceived?.Invoke(update);
                    break;

                default:
                    Console.WriteLine($"Unknown message type received: {type}");
                    OnErrorReceived?.Invoke("Unknown message type received.");
                    break;
            }
        }
    }
}

public class GameMessage
{
    public string type;
    public object data;
}

public enum MessageType : uint
{
    ALIAS = 1,          // Client sends their alias upon connection
    WELCOME = 2,        // Server sends a welcome message
    ECHO = 3,           // Server echoes a message
    MOVE = 4,           // Client sends a move
    ERROR = 5,          // Server sends an error message
    UPDATE = 6,          // Server sends game state updates
    NEWGAME = 7,        // Client requests to start a new game with a password
    JOINGAME = 8        // Client requests to join an existing game using a password
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