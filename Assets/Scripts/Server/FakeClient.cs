using System;
using System.Threading.Tasks;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

public class FakeClient : IGameClient
{
#pragma warning disable // Suppresses unused variable warning
    
    // Events
    public event Action<Response<string>> OnRegisterClientResponse;
    public event Action<Response<string>> OnDisconnect;
    public event Action<ResponseBase> OnErrorResponse;
    public event Action<Response<string>> OnRegisterNicknameResponse;
    public event Action<Response<SLobby>> OnGameLobbyResponse;
    public event Action<Response<string>> OnLeaveGameLobbyResponse;
    public event Action<Response<string>> OnJoinGameLobbyResponse;
    public event Action<Response<SLobby>> OnReadyLobbyResponse;
    public event Action<Response<SSetupParameters>> OnDemoStarted;
    public event Action OnLobbyResponse;
    
    // Internal state
    Guid clientId;
    bool isConnected = false;
    bool isNicknameRegistered = false;
    SLobby currentLobby;

    // simulated state of server lobby
    SLobby fakeServerLobby;
    
#pragma warning restore // Re-enables the warning
    
    public FakeClient()
    {
        clientId = Globals.LoadOrGenerateClientId();
        if (Globals.GetNickname() == null)
        {
            PlayerPrefs.SetString("nickname", "defaultNick");
        }
        Debug.Log("FakeClient: Initialized with clientId " + clientId);
    }

    public async Task ConnectToServer()
    {
        RegisterClientRequest registerClientRequest = new();
        isConnected = true;
        await SendFakeRequestToServer<string>(MessageType.REGISTERCLIENT, registerClientRequest);
    }

    public async Task SendRegisterNickname(string nicknameInput)
    {
        RegisterNicknameRequest registerNicknameRequest = new()
        {
            nickname = nicknameInput,
        };
        await SendFakeRequestToServer<string>(MessageType.REGISTERNICKNAME, registerNicknameRequest);
    }

    public async Task SendGameLobby()
    {
        GameLobbyRequest gameLobbyRequest = new()
        {
            gameMode = 0,
            sBoardDef = new SBoardDef(GameManager.instance.tempBoardDef),
        };
        await SendFakeRequestToServer<SLobby>(MessageType.GAMELOBBY, gameLobbyRequest);
    }

    public async Task SendGameLobbyLeaveRequest()
    {
        LeaveGameLobbyRequest leaveGameLobbyRequest = new();
        await SendFakeRequestToServer<string>(MessageType.LEAVEGAMELOBBY, leaveGameLobbyRequest);
    }

    public async Task SendGameLobbyJoinRequest(string password)
    {
        // NOTE: this should never be happening in offline mode
        JoinGameLobbyRequest joinGameLobbyRequest = new();
        await SendFakeRequestToServer<SLobby>(MessageType.JOINGAMELOBBY, joinGameLobbyRequest);
    }

    public async Task SendGameLobbyReadyRequest(bool ready)
    {
        ReadyGameLobbyRequest readyGameLobbyRequest = new()
        {
            ready = true,
        };
        await SendFakeRequestToServer<SLobby>(MessageType.READYLOBBY, readyGameLobbyRequest);
    }

    public async Task StartGameDemoRequest()
    {
        SSetupParameters setupParameters = new SSetupParameters(Player.RED, currentLobby.sBoardDef);
        StartGameRequest startGameRequest = new()
        {
            setupParameters = setupParameters,
        };
        await SendFakeRequestToServer<SSetupParameters>(MessageType.GAMESTART, startGameRequest);
    }


    async Task SendFakeRequestToServer<TResponse>(MessageType messageType, RequestBase requestData)
    {
        Debug.Log($"OFFLINE: Sending fake Request of type {messageType}");
        if (!isConnected)
        {
            return;
        }
        requestData.clientId = clientId;
        requestData.requestId = new Guid();
        ResponseBase response = null;
        switch (requestData)
        {
            case RegisterClientRequest registerClientRequest:
                response = new Response<string>
                {
                    requestId = requestData.requestId,
                    success = true,
                    responseCode = 0,
                    data = "Client registered successfully."
                };
                break;
            case RegisterNicknameRequest registerNicknameRequest:
                response = new Response<string>
                {
                    requestId = requestData.requestId,
                    success = true,
                    responseCode = 0,
                    data = registerNicknameRequest.nickname
                };
                break;
            case GameLobbyRequest gameLobbyRequest:
                fakeServerLobby = new()
                {
                    lobbyId = Guid.NewGuid(),
                    hostId = clientId,
                    guestId = Guid.Empty,
                    sBoardDef = gameLobbyRequest.sBoardDef,
                    gameMode = 0,
                    isGameStarted = false,
                    password = "offline",
                    hostReady = false,
                    guestReady = false,
                };
                response = new Response<SLobby>
                {
                    requestId = requestData.requestId,
                    success = true,
                    responseCode = 0,
                    data = fakeServerLobby,
                };
                break;
            case LeaveGameLobbyRequest leaveGameLobbyRequest:
                if (leaveGameLobbyRequest.clientId == fakeServerLobby.hostId)
                {
                    fakeServerLobby.hostReady = false;
                }
                else if (leaveGameLobbyRequest.clientId == fakeServerLobby.guestId)
                {
                    fakeServerLobby.guestReady = false;
                }
                response = new Response<string>
                {
                    requestId = requestData.requestId,
                    success = true,
                    responseCode = 0,
                    data = "Left the lobby successfully.",
                };
                break;
            case JoinGameLobbyRequest joinGameLobbyRequest:
                fakeServerLobby.guestId = joinGameLobbyRequest.clientId;
                fakeServerLobby.guestReady = false;
                response = new Response<SLobby>
                {
                    requestId =  requestData.requestId,
                    data = fakeServerLobby,
                };
                break;
            case ReadyGameLobbyRequest readyGameLobbyRequest:
                if (readyGameLobbyRequest.clientId == fakeServerLobby.hostId)
                {
                    fakeServerLobby.hostReady = true;
                }
                else if (readyGameLobbyRequest.clientId == fakeServerLobby.guestId)
                {
                    fakeServerLobby.guestReady = true;
                }
                response = new Response<SLobby>
                {
                    data = fakeServerLobby,
                };
                break;
            case StartGameRequest startGameRequest:
                fakeServerLobby.isGameStarted = true;
                response = new Response<SSetupParameters>
                {
                    data = startGameRequest.setupParameters,
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(requestData));
        }
        await Task.Delay(55);
        ProcessFakeResponse(response, messageType);
    }
    
    void ProcessFakeResponse(ResponseBase response, MessageType messageType)
    {
        Debug.Log($"OFFLINE: Processing fake response of type {messageType}");
        switch (messageType)
        {
            case MessageType.SERVERERROR:
                // TODO: not yet implemented
                break;
            case MessageType.REGISTERCLIENT:
                Response<string> registerClientResponse = (Response<string>)response;
                HandleRegisterClientResponse(registerClientResponse);
                break;
            case MessageType.REGISTERNICKNAME:
                Response<string> registerNicknameResponse = (Response<string>)response;
                HandleRegisterNicknameResponse(registerNicknameResponse);
                break;
            case MessageType.GAMELOBBY:
                Response<SLobby> startLobbyResponse = (Response<SLobby>)response;
                HandleGameLobbyResponse(startLobbyResponse);
                break;
            case MessageType.LEAVEGAMELOBBY:
                Response<string> leaveGameLobbyResponse = (Response<string>)response;
                HandleLeaveGameLobbyResponse(leaveGameLobbyResponse);
                break;
            case MessageType.JOINGAMELOBBY:
                // TODO: not yet implemented
                break;
            case MessageType.READYLOBBY:
                Response<SLobby> readyLobbyResponse = (Response<SLobby>)response;
                HandleReadyLobbyResponse(readyLobbyResponse);
                break;
            case MessageType.GAMESTART:
                Response<SSetupParameters> gameStartResponse = (Response<SSetupParameters>)response;
                HandleGameStartResponse(gameStartResponse);
                break;
            case MessageType.GAME:
                // TODO: not yet implemented
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null);
        }
    }
    
    // Helper methods to simulate server responses
    
    void HandleRegisterClientResponse(Response<string> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.Log("Invoking OnRegisterClientResponse");
            OnRegisterClientResponse?.Invoke(response);
        });
        
    }
    
    void HandleRegisterNicknameResponse(Response<string> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            PlayerPrefs.SetString("nickname", response.data);
            isNicknameRegistered = true;
            Debug.Log("Invoking OnRegisterNicknameResponse");
            OnRegisterNicknameResponse?.Invoke(response);
        });
        
        
    }
    
    void HandleGameLobbyResponse(Response<SLobby> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            currentLobby = response.data;
            Debug.Log("Invoking OnGameLobbyResponse");
            OnGameLobbyResponse?.Invoke(response);
        });
        
    }
    
    void HandleLeaveGameLobbyResponse(Response<string> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.Log("Invoking OnLeaveGameLobbyResponse");
            OnLeaveGameLobbyResponse?.Invoke(response);
        });
        
    }

    void HandleReadyLobbyResponse(Response<SLobby> response)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.Log("Invoking OnReadyLobbyResponse");
            OnReadyLobbyResponse?.Invoke(response);
        });
        
    }
    
    void HandleGameStartResponse(Response<SSetupParameters> gameStartResponse)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.Log($"Invoking OnDemoStarted to {OnDemoStarted?.GetInvocationList().Length} listeners");
            OnDemoStarted?.Invoke(gameStartResponse);
        });
        
    }
}
