using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.UI;

public class ServerTester : MonoBehaviour
{

    TcpClient client;
    NetworkStream stream;
    bool isConnected = false;

    public bool isNicknameRegistered = false;
    // Replace with your server's IP and port
    string serverIP = "127.0.0.1"; // Assuming the server is running locally
    int serverPort = 12345;

    // Store the client ID to handle reconnections
    Guid clientId;

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
                byte[] message = Globals.SerializeMessage(messageType, data);
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
                (MessageType messageType, byte[] data) = await Globals.DeserializeMessageAsync(stream);
                string jsonResponse = Encoding.UTF8.GetString(data);

                switch (messageType)
                {
                    case MessageType.REGISTERCLIENT:
                        {
                            Response<string> response = JsonConvert.DeserializeObject<Response<string>>(jsonResponse);
                            break;
                        }
                    case MessageType.SERVERERROR:
                        {
                            Response<string> response = JsonConvert.DeserializeObject<Response<string>>(jsonResponse);
                            Debug.LogError($"Server error: {response.data}");
                            HandleServerDisconnection();
                            break;
                        }
                    case MessageType.REGISTERNICKNAME:
                        {
                            Response<string> response = JsonConvert.DeserializeObject<Response<string>>(jsonResponse);
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
                    case MessageType.JOINGAMELOBBY:
                    case MessageType.GAME:
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
