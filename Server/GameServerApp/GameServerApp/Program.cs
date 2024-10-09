using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GameServerApp
{
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
        TcpListener listener;
        bool isRunning;

        ConcurrentDictionary<string, ClientInfo> clients;
        ConcurrentDictionary<Guid, GameSession> activeGames;

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
            while (isRunning)
            {
                try
                {
                    TcpClient tcpClient = listener.AcceptTcpClient();
                    Console.WriteLine("Client connected");
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(tcpClient);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        void HandleClient(object obj)
        {
            TcpClient tcpClient = (TcpClient)obj;
            NetworkStream stream = tcpClient.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;
            string alias = null;

            try
            {
                // Step 1: Receive the client's alias (assuming the first message is the alias)
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // Client disconnected immediately
                    Console.WriteLine("Client disconnected before sending alias.");
                    tcpClient.Close();
                    return;
                }

                alias = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Console.WriteLine($"Received alias: {alias}");

                // Step 2: Validate the alias
                if (string.IsNullOrWhiteSpace(alias) || clients.ContainsKey(alias))
                {
                    // Invalid or duplicate alias
                    string response = "Invalid or duplicate alias.";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                    stream.Write(responseBytes, 0, responseBytes.Length);
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
                    string response = "Failed to register alias.";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                    stream.Write(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine($"Failed to add client with alias '{alias}'. Disconnecting.");
                    tcpClient.Close();
                    return;
                }

                Console.WriteLine($"Client '{alias}' registered successfully.");

                // Step 4: Send a welcome message to the client
                string welcomeMessage = $"Welcome {alias}!";
                byte[] welcomeBytes = Encoding.UTF8.GetBytes(welcomeMessage + "\n");
                stream.Write(welcomeBytes, 0, welcomeBytes.Length);

                // Step 5: Continuously listen for client messages
                while (isRunning && tcpClient.Connected)
                {
                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            // Client has disconnected
                            Console.WriteLine($"Client '{alias}' has disconnected.");
                            break;
                        }

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        Console.WriteLine($"Received from '{alias}': {message}");

                        // Process the received message
                        ProcessClientMessage(clientInfo, message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading from client '{alias}': {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception handling client '{alias}': {ex.Message}");
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
                        // For example:
                        // if (activeGames.ContainsKey(removedClient.CurrentGameSessionId))
                        // {
                        //     HandleGameSessionTermination(removedClient.CurrentGameSessionId);
                        // }
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


        void ProcessClientMessage(ClientInfo client, string message)
        {
            // Implement your message processing logic here
            // For example, echo the message back to the client
            string response = $"Echo: {message}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
            try
            {
                client.stream.Write(responseBytes, 0, responseBytes.Length);
                Console.WriteLine($"Echoed back to '{client.alias}': {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending echo to '{client.alias}': {ex.Message}");
            }
        }
    }

    class ClientInfo
    {
        public string alias;
        public TcpClient tcpClient;
        public NetworkStream stream;
        public IPAddress ipAddress;
        
    }

    class GameSession
    {
        
    }
}