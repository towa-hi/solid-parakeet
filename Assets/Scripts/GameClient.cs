using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class GameClient
{
    //NetworkManager networkManager;
    
    // put this in it's own struct later
    Guid clientId;
    TcpClient client;
    NetworkStream stream;
    bool isConnected = false;
    string serverIP = "127.0.0.1"; // Assuming the server is running locally
    int serverPort = 12345;
    public bool isNicknameRegistered = false;

    public event Action<Response<string>> OnRegisterClientResponse;
    public event Action<Response<string>> OnDisconnect;
    public event Action<ResponseBase> OnErrorResponse;
    public event Action<Response<string>> OnRegisterNicknameResponse;
    public event Action<Response<SLobby>> OnGameLobbyResponse;
    public event Action OnLobbyResponse;
    public RequestManager requestManager;
    
    public GameClient(NetworkManager inNetworkManager)
    {
        requestManager = new RequestManager();
    }
    
    public async Task ConnectToServer()
    {
        clientId = Globals.LoadOrGenerateClientId();
        if (Globals.GetNickname() == null)
        {
            PlayerPrefs.SetString("nickname", "defaultNick");
        }
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

    public async Task SendRegisterNickname(string nicknameInput)
    {
        if (!Globals.IsNicknameValid(nicknameInput))
        {
            Debug.LogError($"Nickname not valid: {nicknameInput}");
            return;
        }
        try
        {
            RegisterNicknameRequest registerNicknameRequest = new RegisterNicknameRequest
            {
                nickname = nicknameInput,
                clientId = clientId
            };
            await SendRequestToServer<string>(MessageType.REGISTERNICKNAME, registerNicknameRequest);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error registering nickname: {e.Message}");
            //HandleServerDisconnection();
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
            await SendRequestToServer<string>(MessageType.REGISTERCLIENT, registerClientRequest);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    async Task SendRequestToServer<TResponse>(MessageType messageType, RequestBase requestData)
    {
        if (isConnected)
        {
            try
            {
                Guid requestId = requestManager.AddRequest<TResponse>();
                requestData.requestId = requestId;
                requestData.messageType = messageType;

                string json = JsonConvert.SerializeObject(requestData);
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] message = Globals.SerializeMessage(messageType, data);
                await stream.WriteAsync(message, 0, message.Length);
                await stream.FlushAsync();
                Debug.Log($"Sent: '{json}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending request to server: {e.Message}");
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

                ResponseBase responseBase = JsonConvert.DeserializeObject<ResponseBase>(jsonResponse);
                if (responseBase == null)
                {
                    Debug.LogError("Failed to deserialize response base.");
                    continue;
                }

                // Handle the response
                HandleResponse(responseBase, jsonResponse, messageType);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading from server: {e.Message}");
                HandleServerDisconnection();
            }
        }
    }
    
    void HandleResponse(ResponseBase responseBase, string jsonResponse, MessageType messageType)
    {
        if (requestManager.TryGetResponseType(responseBase.requestId, out var responseType))
        {
            // Deserialize the response into the appropriate type
            var genericResponseType = typeof(Response<>).MakeGenericType(responseType);
            var response = JsonConvert.DeserializeObject(jsonResponse, genericResponseType) as ResponseBase;

            if (response == null)
            {
                Debug.LogError("Failed to deserialize response into expected type.");
                return;
            }

            // Remove the request from pending requests
            requestManager.RemoveRequest(responseBase.requestId);

            // Process the response based on the message type
            ProcessResponse(response, messageType);
        }
        else
        {
            Debug.LogWarning($"Received response with unknown requestId: {responseBase.requestId}");
        }
    }
    
    void ProcessResponse(ResponseBase response, MessageType messageType)
    {
        switch (response)
        {
            case Response<string> stringResponse when messageType == MessageType.REGISTERCLIENT:
                HandleRegisterClientResponse(stringResponse);
                break;
            case Response<string> stringResponse when messageType == MessageType.REGISTERNICKNAME:
                HandleRegisterNicknameResponse(stringResponse);
                break;
            case Response<SLobby> lobbyResponse when messageType == MessageType.GAMELOBBY:
                HandleGameLobbyResponse(lobbyResponse);
                break;
            default:
                Debug.LogWarning($"Unhandled response type for message: {messageType}");
                break;
        }
    }
    
    void HandleRegisterClientResponse(Response<string> response)
    {
        if (response.success)
        {
            // Update game state if necessary
            // Raise event to notify UI or other components
            OnRegisterClientResponse?.Invoke(response);
            SendRegisterNickname(Globals.GetNickname());
        }
        else
        {
            // Handle error
            Debug.LogError("Register client failed: " + response.message);
            OnErrorResponse?.Invoke(response);
        }
    }

    void HandleRegisterNicknameResponse(Response<string> response)
    {
        if (response.success)
        {
            PlayerPrefs.SetString("nickname", response.data);
            isNicknameRegistered = true;
            // Update game state
            // Raise event
            OnRegisterNicknameResponse?.Invoke(response);
        }
        else
        {
            isNicknameRegistered = false;
            // Handle error
            Debug.LogError("Register nickname failed: " + response.message);
            OnErrorResponse?.Invoke(response);
        }
    }

    void HandleGameLobbyResponse(Response<SLobby> response)
    {
        if (response.success)
        {
            // Update game state with lobby information
            // Raise event
            OnGameLobbyResponse?.Invoke(response);
        }
        else
        {
            // Handle error
            Debug.LogError("Game lobby response error: " + response.message);
            OnErrorResponse?.Invoke(response);
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
}

public class ResponseBase
{
    public Guid requestId;
    public bool success;
    public int responseCode;
    public string message;
}

public class Response<T> : ResponseBase
{
    public T data;
    
    public Response() { }
    
    public Response(bool inSuccess, int inResponseCode, T inData)
    {
        success = inSuccess;
        responseCode = inResponseCode;
        data = inData;
    }

}

public class RequestBase
{
    public Guid requestId;
    public Guid clientId;
    public MessageType messageType;
    public object data;
}

public class RegisterClientRequest : RequestBase
{
}

public class RegisterNicknameRequest : RequestBase
{
    public string nickname { get; set; }
}

public class GameLobbyRequest : RequestBase
{
    public int gameMode { get; set; }
    public SBoardDef sBoardDef { get; set; }
}

public class RequestManager
{
    ConcurrentDictionary<Guid, Type> pendingRequests = new();

    public Guid AddRequest<TResponse>()
    {
        Guid requestId = Guid.NewGuid();
        pendingRequests.TryAdd(requestId, typeof(TResponse));
        // add timeout logic later
        return requestId;
    }

    public bool TryGetResponseType(Guid requestId, out Type responseType)
    {
        return pendingRequests.TryGetValue(requestId, out responseType);
    }

    public void RemoveRequest(Guid requestId)
    {
        pendingRequests.Remove(requestId, out _);
    }
}
