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
    public event Action<Response<SSetupParameters>> OnDemoStartedResponse;
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
    SSetupParameters lobbySetupParameters;
    SBoardDef lobbyBoardDef;
    SSetupPawn[] blueSetupPawns;
    SSetupPawn[] redSetupPawns;
    SQueuedMove redQueuedMove;
    SQueuedMove blueQueuedMove;
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

    public void SendGameLobby()
    {
        GameLobbyRequest gameLobbyRequest = new()
        {
            gameMode = 0,
            sBoardDef = new SBoardDef(GameManager.instance.tempBoardDef),
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
        List<KeyValuePair<PawnDef, int>> orderedPawnList = new List<KeyValuePair<PawnDef, int>>(GameManager.instance.orderedPawnDefList);
        orderedPawnList.RemoveAll(kvp => kvp.Key.pawnName == "Unknown");
        SSetupPawnData[] arr = new SSetupPawnData[orderedPawnList.Count];
        for (int i = 0; i < arr.Length; i++)
        {
            KeyValuePair<PawnDef, int> kvp = orderedPawnList[i];
            SSetupPawnData setupPawnData = new SSetupPawnData()
            {
                pawnDef = new SPawnDef(kvp.Key),
                maxPawns = kvp.Value,
            };
            arr[i] = setupPawnData;
        }
        SSetupParameters setupParameters = new()
        {
            player = (int)Player.RED,
            board = new SBoardDef(GameManager.instance.tempBoardDef),
            maxPawnsDict = arr,
        };
        StartGameRequest startGameRequest = new()
        {
            setupParameters = setupParameters,
        };
        SendFakeRequestToServer(MessageGenre.GAMESTART, startGameRequest);
    }
    
    public void SendSetupSubmissionRequest(SSetupPawn[] setupPawnList)
    {
        SetupRequest setupRequest = new()
        {
            player = (int)Player.RED,
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
                lobbyBoardDef = gameLobbyRequest.sBoardDef;
                fakeServerLobby = new SLobby
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
                lobbySetupParameters = startGameRequest.setupParameters;
                response = new Response<SSetupParameters>
                {
                    requestId = requestData.requestId,
                    success = true,
                    data = startGameRequest.setupParameters,
                };
                break;
            case SetupRequest setupRequest:
                if (setupRequest.player == (int)Player.RED)
                {
                    redSetupPawns = setupRequest.setupPawns;
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
                if (moveRequest.move.player == (int)Player.RED)
                {
                    redQueuedMove = moveRequest.move;
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
                Response<SSetupParameters> gameStartResponse = (Response<SSetupParameters>)response;
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
    
    void HandleGameStartResponse(Response<SSetupParameters> gameStartResponse)
    {
        Debug.Log($"Invoking OnDemoStarted to {OnDemoStartedResponse?.GetInvocationList().Length} listeners");
        OnDemoStartedResponse?.Invoke(gameStartResponse);
    }
    
    
    void HandleGameSetupResponse(Response<bool> gameSetupResponse)
    {
        Debug.Log($"Invoking HandleGameSetupResponse to {OnDemoStartedResponse?.GetInvocationList().Length} listeners");
        OnSetupSubmittedResponse?.Invoke(gameSetupResponse);
        // fill blue setup pawns like as if blue already sent a valid request
        blueSetupPawns = SGameState.GenerateValidSetup((int)Player.BLUE, lobbySetupParameters);
        OnBothPlayersSetupSubmitted();
    }
    
    void OnBothPlayersSetupSubmitted()
    {
        SSetupPawn[] combinedSetupPawns = new SSetupPawn[redSetupPawns.Length + blueSetupPawns.Length];
        for (int i = 0; i < redSetupPawns.Length; i++)
        {
            combinedSetupPawns[i] = redSetupPawns[i];
        }
        int blueSetupPawnsIndex = 0;
        for (int i = redSetupPawns.Length; i < combinedSetupPawns.Length; i++)
        {
            combinedSetupPawns[i] = blueSetupPawns[blueSetupPawnsIndex];
            blueSetupPawnsIndex++;
        }
        SPawn[] allPawns = new SPawn[combinedSetupPawns.Length];
        for (int i = 0; i < allPawns.Length; i++)
        {
            allPawns[i] = new SPawn(combinedSetupPawns[i]);
        }
        masterGameState = new SGameState((int)Player.NONE, lobbyBoardDef, allPawns);
        SGameState redInitialGameState = SGameState.Censor(masterGameState, (int)Player.RED);
        // blue state is unused in fake client
        SGameState blueInitialGameState = SGameState.Censor(masterGameState, (int)Player.BLUE);
        Response<SGameState> initialGameStateResponseRed = new()
        {
            requestId = Guid.Empty,
            success = true,
            data = redInitialGameState,
        };
        //Task.Delay(200);
        ProcessFakeResponse(initialGameStateResponseRed, MessageGenre.SETUPFINISHED);
    }
    
    void HandleSetupFinished(Response<SGameState> initialGameStateResponse)
    {
        OnSetupFinishedResponse?.Invoke(initialGameStateResponse);
    }
    
    void HandleMoveResponse(Response<bool> moveResponse)
    {
        OnMoveResponse?.Invoke(moveResponse);
        OnBothPlayersMoveSubmitted();
    }

    void OnBothPlayersMoveSubmitted()
    {
        SQueuedMove? maybeBlueQueuedMove = SGameState.GenerateValidMove(masterGameState, (int)Player.BLUE);
        if (maybeBlueQueuedMove.HasValue)
        {
            blueQueuedMove = maybeBlueQueuedMove.Value;
        }
        else
        {
            // TODO: tell the players that the game is over
            return;
        }
        SResolveReceipt receipt = SGameState.Resolve(masterGameState, redQueuedMove, blueQueuedMove);
        masterGameState = receipt.gameState;

        SResolveReceipt redReceipt = receipt;
        redReceipt.player = (int)Player.RED;
        redReceipt.gameState = SGameState.Censor(masterGameState, (int)Player.RED);
        Response<SResolveReceipt> redGameStateResponse = new Response<SResolveReceipt>
        {
            requestId = Guid.Empty,
            success = true,
            data = redReceipt,
        };
        //Task.Delay(200);
        ProcessFakeResponse(redGameStateResponse, MessageGenre.RESOLVE);

    }
    
    void HandleResolveResponse(Response<SResolveReceipt> resolveResponse)
    {
        OnResolveResponse?.Invoke(resolveResponse);
    }
}
