
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class NetworkManager
{
    string serverIP = "127.0.0.1";
    int serverPort = 12345;

    TcpClient client;
    NetworkStream stream;

    Thread receiveThread;
    bool isConnected = false;

    ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public NetworkManager()
    {
        
    }

    public void ConnectToServer(string inServerIP, int inServerPort)
    {
        serverIP = inServerIP;
        serverPort = inServerPort;

        try
        {
            client = new TcpClient();
            client.Connect(serverIP, serverPort);
            stream = client.GetStream();
            isConnected = true;
            Console.WriteLine("Connected to server");

            OnConnected?.Invoke();

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            OnDisconnected?.Invoke();
            throw;
        }
    }

    public void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            stream?.Close();
            client?.Close();
            if (receiveThread is { IsAlive: true })
            {
                receiveThread.Abort();
            }
            Console.WriteLine("Disconnected from server.");
        }
    }

    public void SendMessageToServer(string message)
    {
        if (isConnected && stream != null)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "\n"); // Append newline for easier parsing
                stream.Write(data, 0, data.Length);
                stream.Flush();
                Console.WriteLine("Sent to server: " + message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Send error: " + e.Message);
            }
        }
        else
        {
            Console.WriteLine("Cannot send message. Not connected to server.");
        }
    }
    
    // Receive data from the server
    private void ReceiveData()
    {
        try
        {
            byte[] buffer = new byte[1024];
            int bytesRead;

            while (isConnected)
            {
                if (stream.DataAvailable)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Server disconnected.");
                        Disconnect();
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    // Assuming messages are separated by newline
                    string[] messages = message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string msg in messages)
                    {
                        messageQueue.Enqueue(msg.Trim());
                    }
                }
                else
                {
                    Thread.Sleep(100); // Prevents high CPU usage
                }
            }
        }
        catch (Exception e)
        {
            if (isConnected)
            {
                Console.WriteLine("Receive error: " + e.Message);
                Disconnect();
            }
        }
    }

    // Process messages from the server (to be called from main thread)
    public void ProcessIncomingMessages(Action<string> messageHandler)
    {
        while (messageQueue.TryDequeue(out string message))
        {
            messageHandler?.Invoke(message);
        }
    }
}

public class GameMessage
{
    public string type;
    public object data;
}