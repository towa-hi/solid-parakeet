using System;
using System.Collections.Generic;
using System.Linq;
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
    public event Action<Response<SLobbyParameters>> OnDemoStartedResponse;
    public event Action<Response<bool>> OnSetupSubmittedResponse;
    public event Action<Response<SGameState>> OnSetupFinishedResponse;
    public event Action<Response<bool>> OnMoveResponse;
    public event Action<Response<SResolveReceipt>> OnResolveResponse;

    // Internal state
    Guid clientId;
    bool isConnected;
    bool isNicknameRegistered;
    SLobby currentLobby;

    // simulated state of server lobby
    SLobby fakeServerLobby;
    // SSetupPawn[] blueSetupPawns;
    // SSetupPawn[] redSetupPawns;
    SSetupPawn[] hostSetupPawns;
    SSetupPawn[] guestSetupPawns;
    // SQueuedMove redQueuedMove;
    // SQueuedMove blueQueuedMove;
    SQueuedMove hostQueuedMove;
    SQueuedMove guestQueuedMove;
    SGameState masterGameState;
    
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

    public void ConnectToServer()
    {
        RegisterClientRequest registerClientRequest = new();
        isConnected = true;
        SendFakeRequestToServer(MessageGenre.REGISTERCLIENT, registerClientRequest);
    }

    public void SendRegisterNickname(string nicknameInput)
    {
        RegisterNicknameRequest registerNicknameRequest = new()
        {
            nickname = nicknameInput,
        };
        SendFakeRequestToServer(MessageGenre.REGISTERNICKNAME, registerNicknameRequest);
    }

    public void SendGameLobby(SLobbyParameters lobbyParameters)
    {
        GameLobbyRequest gameLobbyRequest = new()
        {
            gameMode = 0,
            lobbyParameters = lobbyParameters,
        };
        SendFakeRequestToServer(MessageGenre.GAMELOBBY, gameLobbyRequest);
    }

    public void SendGameLobbyLeaveRequest()
    {
        LeaveGameLobbyRequest leaveGameLobbyRequest = new();
        SendFakeRequestToServer(MessageGenre.LEAVEGAMELOBBY, leaveGameLobbyRequest);
    }

    public void SendGameLobbyJoinRequest(string password)
    {
        // NOTE: this should never be happening in offline mode
        JoinGameLobbyRequest joinGameLobbyRequest = new();
        SendFakeRequestToServer(MessageGenre.JOINGAMELOBBY, joinGameLobbyRequest);
    }

    public void SendGameLobbyReadyRequest(bool ready)
    {
        ReadyGameLobbyRequest readyGameLobbyRequest = new()
        {
            ready = true,
        };
        SendFakeRequestToServer(MessageGenre.READYLOBBY, readyGameLobbyRequest);
    }

    public void SendStartGameDemoRequest()
    {
        StartGameRequest startGameRequest = new();
        SendFakeRequestToServer(MessageGenre.GAMESTART, startGameRequest);
    }
    
    public void SendSetupSubmissionRequest(SSetupPawn[] setupPawnList)
    {
        SetupRequest setupRequest = new()
        {
            setupPawns = setupPawnList,
        };
        SendFakeRequestToServer(MessageGenre.GAMESETUP, setupRequest);
    }

    public void SendMove(SQueuedMove move)
    {
        MoveRequest moveRequest = new()
        {
            move = move,
        };
        SendFakeRequestToServer(MessageGenre.MOVE, moveRequest);
    }


    void SendFakeRequestToServer(MessageGenre messageGenre, RequestBase requestData)
    {
        Debug.Log($"OFFLINE: Sending fake Request of type {messageGenre}");
        if (!isConnected)
        {
            return;
        }
        requestData.clientId = clientId;
        requestData.requestId = new Guid();
        ResponseBase response;
        switch (requestData)
        {
            case RegisterClientRequest:
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
                fakeServerLobby = new SLobby
                {
                    lobbyId = Guid.NewGuid(),
                    hostId = clientId,
                    guestId = Guid.Empty,
                    lobbyParameters = gameLobbyRequest.lobbyParameters,
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
                    requestId = requestData.requestId,
                    success = true,
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
                    requestId = requestData.requestId,
                    success = true,
                    data = fakeServerLobby,
                };
                break;
            case StartGameRequest startGameRequest:
                fakeServerLobby.isGameStarted = true;
                response = new Response<SLobbyParameters>
                {
                    requestId = requestData.requestId,
                    success = true,
                    data = currentLobby.lobbyParameters,
                };
                break;
            case SetupRequest setupRequest:
                if (fakeServerLobby.IsHost(setupRequest.clientId))
                {
                    hostSetupPawns = setupRequest.setupPawns;
                }
                else
                {
                    guestSetupPawns = setupRequest.setupPawns;
                }
                // we assume the fake server already has the other players setupRequest
                response = new Response<bool>
                {
                    requestId = requestData.requestId,
                    success = true,
                    data = true,
                };
                break;
            case MoveRequest moveRequest:
                if (fakeServerLobby.IsHost(moveRequest.clientId))
                {
                    hostQueuedMove = moveRequest.move;
                }
                else
                {
                    guestQueuedMove = moveRequest.move;
                }
                response = new Response<bool>
                {
                    requestId = requestData.requestId,
                    success = true,
                    data = true,
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(requestData));
        }
        //Task.Delay(200);
        ProcessFakeResponse(response, messageGenre);
    }
    
    void ProcessFakeResponse(ResponseBase response, MessageGenre messageGenre)
    {
        Debug.Log($"OFFLINE: Processing fake response of type {messageGenre}");
        switch (messageGenre)
        {
            case MessageGenre.SERVERERROR:
                // TODO: not yet implemented
                break;
            case MessageGenre.REGISTERCLIENT:
                Response<string> registerClientResponse = (Response<string>)response;
                HandleRegisterClientResponse(registerClientResponse);
                break;
            case MessageGenre.REGISTERNICKNAME:
                Response<string> registerNicknameResponse = (Response<string>)response;
                HandleRegisterNicknameResponse(registerNicknameResponse);
                break;
            case MessageGenre.GAMELOBBY:
                Response<SLobby> startLobbyResponse = (Response<SLobby>)response;
                HandleGameLobbyResponse(startLobbyResponse);
                break;
            case MessageGenre.LEAVEGAMELOBBY:
                Response<string> leaveGameLobbyResponse = (Response<string>)response;
                HandleLeaveGameLobbyResponse(leaveGameLobbyResponse);
                break;
            case MessageGenre.JOINGAMELOBBY:
                // TODO: not yet implemented
                break;
            case MessageGenre.READYLOBBY:
                Response<SLobby> readyLobbyResponse = (Response<SLobby>)response;
                HandleReadyLobbyResponse(readyLobbyResponse);
                break;
            case MessageGenre.GAMESTART:
                Response<SLobbyParameters> gameStartResponse = (Response<SLobbyParameters>)response;
                HandleGameStartResponse(gameStartResponse);
                break;
            case MessageGenre.GAMESETUP:
                Response<bool> gameSetupResponse = (Response<bool>)response;
                HandleGameSetupResponse(gameSetupResponse);
                break;
            case MessageGenre.SETUPFINISHED:
                Response<SGameState> setupFinishedResponse = (Response<SGameState>)response;
                HandleSetupFinished(setupFinishedResponse);
                break;
            case MessageGenre.MOVE:
                Response<bool> moveResponse = (Response<bool>)response;
                HandleMoveResponse(moveResponse);
                break;
            case MessageGenre.RESOLVE:
                Response<SResolveReceipt> resolveResponse = (Response<SResolveReceipt>)response;
                HandleResolveResponse(resolveResponse);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(messageGenre), messageGenre, null);
        }
    }
    
    // Helper methods to simulate server responses
    
    void HandleRegisterClientResponse(Response<string> response)
    {
        Debug.Log("Invoking OnRegisterClientResponse");
        OnRegisterClientResponse?.Invoke(response);
    }
    
    void HandleRegisterNicknameResponse(Response<string> response)
    {
        PlayerPrefs.SetString("nickname", response.data);
        isNicknameRegistered = true;
        Debug.Log("Invoking OnRegisterNicknameResponse");
        OnRegisterNicknameResponse?.Invoke(response);
    }
    
    void HandleGameLobbyResponse(Response<SLobby> response)
    {
        currentLobby = response.data;
        Debug.Log("Invoking OnGameLobbyResponse");
        OnGameLobbyResponse?.Invoke(response);
    }
    
    void HandleLeaveGameLobbyResponse(Response<string> response)
    {
        Debug.Log("Invoking OnLeaveGameLobbyResponse");
        OnLeaveGameLobbyResponse?.Invoke(response);
    }

    void HandleReadyLobbyResponse(Response<SLobby> response)
    {
        Debug.Log("Invoking OnReadyLobbyResponse");
        OnReadyLobbyResponse?.Invoke(response);
    }
    
    void HandleGameStartResponse(Response<SLobbyParameters> gameStartResponse)
    {
        Debug.Log($"Invoking OnDemoStarted to {OnDemoStartedResponse?.GetInvocationList().Length} listeners");
        OnDemoStartedResponse?.Invoke(gameStartResponse);
    }
    
    
    void HandleGameSetupResponse(Response<bool> gameSetupResponse)
    {
        Debug.Log($"Invoking HandleGameSetupResponse to {OnDemoStartedResponse?.GetInvocationList().Length} listeners");
        OnSetupSubmittedResponse?.Invoke(gameSetupResponse);
        // fill blue setup pawns like as if blue already sent a valid request
        guestSetupPawns = SGameState.GenerateValidSetup(currentLobby.lobbyParameters.guestTeam, currentLobby.lobbyParameters);
        OnBothClientsSetupSubmitted();
    }
    
    void OnBothClientsSetupSubmitted()
    {
        SSetupPawn[] combinedSetupPawns = new SSetupPawn[hostSetupPawns.Length + guestSetupPawns.Length];
        for (int i = 0; i < hostSetupPawns.Length; i++)
        {
            combinedSetupPawns[i] = hostSetupPawns[i];
        }
        int guestSetupPawnsIndex = 0;
        for (int i = hostSetupPawns.Length; i < combinedSetupPawns.Length; i++)
        {
            combinedSetupPawns[i] = guestSetupPawns[guestSetupPawnsIndex];
            guestSetupPawnsIndex++;
        }
        SPawn[] allPawns = new SPawn[combinedSetupPawns.Length];
        for (int i = 0; i < allPawns.Length; i++)
        {
            allPawns[i] = new SPawn(combinedSetupPawns[i]);
        }
        masterGameState = new SGameState((int)Team.NONE, currentLobby.lobbyParameters.board, allPawns);
        SGameState hostInitialGameState = SGameState.Censor(masterGameState, currentLobby.lobbyParameters.hostTeam);
        // unused in fake client
        SGameState guestInitialGameState = SGameState.Censor(masterGameState, currentLobby.lobbyParameters.guestTeam);
        Response<SGameState> hostInitialGameStateResponse = new()
        {
            requestId = Guid.Empty,
            success = true,
            data = hostInitialGameState,
        };
        Response<SGameState> guestInitialGameStateResponse = new()
        {
            requestId = Guid.Empty,
            success = true,
            data = guestInitialGameState,
        };
        //Task.Delay(200);
        ProcessFakeResponse(hostInitialGameStateResponse, MessageGenre.SETUPFINISHED);
        //ProcessFakeResponse(guestInitialGameStateResponse, MessageGenre.SETUPFINISHED);
    }
    
    void HandleSetupFinished(Response<SGameState> initialGameStateResponse)
    {
        OnSetupFinishedResponse?.Invoke(initialGameStateResponse);
    }
    
    void HandleMoveResponse(Response<bool> moveResponse)
    {
        OnMoveResponse?.Invoke(moveResponse);
        OnBothClientsMoveSubmitted();
    }

    void OnBothClientsMoveSubmitted()
    {
        // NOTE: most of this is fake for AI
        
        guestQueuedMove = SGameState.GenerateValidMove(masterGameState, currentLobby.lobbyParameters.guestTeam);
        SQueuedMove redMove;
        SQueuedMove blueMove;
        if (currentLobby.lobbyParameters.hostTeam == (int)Team.RED)
        {
            redMove = hostQueuedMove;
            blueMove = guestQueuedMove;
        }
        else
        {
            redMove = guestQueuedMove;
            blueMove = hostQueuedMove;
        }
        SResolveReceipt receipt = SGameState.Resolve(masterGameState, redMove, blueMove);
        masterGameState = receipt.gameState;

        SResolveReceipt hostReceipt = receipt;
        hostReceipt.gameState = SGameState.Censor(masterGameState, currentLobby.lobbyParameters.hostTeam);
        SResolveReceipt guestReceipt = receipt;
        guestReceipt.gameState = SGameState.Censor(masterGameState, currentLobby.lobbyParameters.guestTeam);
        Response<SResolveReceipt> hostGameStateResponse = new()
        {
            requestId = Guid.Empty,
            success = true,
            data = hostReceipt,
        };
        Response<SResolveReceipt> guestGameStateResponse = new()
        {
            requestId = Guid.Empty,
            success = true,
            data = guestReceipt,
        };
        //Task.Delay(200);
        ProcessFakeResponse(hostGameStateResponse, MessageGenre.RESOLVE);

    }
    
    void HandleResolveResponse(Response<SResolveReceipt> resolveResponse)
    {
        OnResolveResponse?.Invoke(resolveResponse);
    }
    
}
