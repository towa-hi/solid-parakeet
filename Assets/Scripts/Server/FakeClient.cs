using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PimDeWitte.UnityMainThreadDispatcher;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

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
    public event Action<Response<SGameState>> OnResolveResponse;

    // Internal state
    Guid clientId;
    bool isConnected;
    bool isNicknameRegistered;
    SLobby currentLobby;

    // simulated state of server lobby
    SLobby fakeServerLobby;
    SSetupParameters lobbySetupParameters;
    SBoardDef lobbyBoardDef;
    SPawn[] blueSetupPawns;
    SPawn[] redSetupPawns;
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

    public async Task ConnectToServer()
    {
        RegisterClientRequest registerClientRequest = new();
        isConnected = true;
        await SendFakeRequestToServer(MessageType.REGISTERCLIENT, registerClientRequest);
    }

    public async Task SendRegisterNickname(string nicknameInput)
    {
        RegisterNicknameRequest registerNicknameRequest = new()
        {
            nickname = nicknameInput,
        };
        await SendFakeRequestToServer(MessageType.REGISTERNICKNAME, registerNicknameRequest);
    }

    public async Task SendGameLobby()
    {
        GameLobbyRequest gameLobbyRequest = new()
        {
            gameMode = 0,
            sBoardDef = new SBoardDef(GameManager.instance.tempBoardDef),
        };
        await SendFakeRequestToServer(MessageType.GAMELOBBY, gameLobbyRequest);
    }

    public async Task SendGameLobbyLeaveRequest()
    {
        LeaveGameLobbyRequest leaveGameLobbyRequest = new();
        await SendFakeRequestToServer(MessageType.LEAVEGAMELOBBY, leaveGameLobbyRequest);
    }

    public async Task SendGameLobbyJoinRequest(string password)
    {
        // NOTE: this should never be happening in offline mode
        JoinGameLobbyRequest joinGameLobbyRequest = new();
        await SendFakeRequestToServer(MessageType.JOINGAMELOBBY, joinGameLobbyRequest);
    }

    public async Task SendGameLobbyReadyRequest(bool ready)
    {
        ReadyGameLobbyRequest readyGameLobbyRequest = new()
        {
            ready = true,
        };
        await SendFakeRequestToServer(MessageType.READYLOBBY, readyGameLobbyRequest);
    }

    public async Task SendStartGameDemoRequest()
    {
        SetupParameters fakeSetupParameters = new()
        {
            player = Player.RED,
            board = GameManager.instance.tempBoardDef,
            maxPawnsDict = Globals.GetUnorderedPawnDefDict(),
        };
        SSetupParameters setupParameters = new(fakeSetupParameters);
        StartGameRequest startGameRequest = new()
        {
            setupParameters = setupParameters,
        };
        await SendFakeRequestToServer(MessageType.GAMESTART, startGameRequest);
    }

    public async Task SendSetupSubmissionRequest(List<SPawn> setupPawnList)
    {
        SetupRequest setupRequest = new()
        {
            player = (int)Player.RED,
            pawns = setupPawnList,
        };
        await SendFakeRequestToServer(MessageType.GAMESETUP, setupRequest);
    }

    public async Task SendMove(SQueuedMove move)
    {
        MoveRequest moveRequest = new()
        {
            move = move,
        };
        await SendFakeRequestToServer(MessageType.MOVE, moveRequest);
    }


    async Task SendFakeRequestToServer(MessageType messageType, RequestBase requestData)
    {
        Debug.Log($"OFFLINE: Sending fake Request of type {messageType}");
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
                    SPawn[] redPawns = setupRequest.pawns.ToArray();
                    for (int i = 0; i < redPawns.Length; i++)
                    {
                        SPawn notSetupPawn = redPawns[i];
                        notSetupPawn.isSetup = false;
                        redPawns[i] = notSetupPawn;
                    }
                    redSetupPawns = redPawns;
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
            case MessageType.GAMESETUP:
                Response<bool> gameSetupResponse = (Response<bool>)response;
                HandleGameSetupResponse(gameSetupResponse);
                break;
            case MessageType.SETUPFINISHED:
                Response<SGameState> setupFinishedResponse = (Response<SGameState>)response;
                HandleSetupFinished(setupFinishedResponse);
                break;
            case MessageType.MOVE:
                Response<bool> moveResponse = (Response<bool>)response;
                HandleMoveResponse(moveResponse);
                break;
            case MessageType.RESOLVE:
                Response<SGameState> resolveResponse = (Response<SGameState>)response;
                HandleResolveResponse(resolveResponse);
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
            Debug.Log($"Invoking OnDemoStarted to {OnDemoStartedResponse?.GetInvocationList().Length} listeners");
            OnDemoStartedResponse?.Invoke(gameStartResponse);
        });
    }
    
    
    void HandleGameSetupResponse(Response<bool> gameSetupResponse)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.Log($"Invoking HandleGameSetupResponse to {OnDemoStartedResponse?.GetInvocationList().Length} listeners");
            OnSetupSubmittedResponse?.Invoke(gameSetupResponse);
        });
        // fill blue setup pawns like as if blue already sent a valid request
        blueSetupPawns = GenerateValidSetup(lobbyBoardDef, (int)Player.BLUE, lobbySetupParameters);
        OnBothPlayersSetupSubmitted();
    }

    static SPawn[] GenerateValidSetup(SBoardDef boardDef, int player, SSetupParameters setupParameters)
    {
        List<SPawn> sPawns = new();
        HashSet<SVector2Int> usedPositions = new();
        List<SVector2Int> allEligiblePositions = new();
        foreach (STile sTile in boardDef.tiles)
        {
            if (sTile.IsTileEligibleForPlayer(player))
            {
                allEligiblePositions.Add(sTile.pos);
            }
        }
        foreach (SSetupPawnData setupPawnData in setupParameters.maxPawnsDict)
        {
            List<SVector2Int> eligiblePositions = boardDef.GetEligiblePositionsForPawn(player, setupPawnData.pawnDef, usedPositions);
            if (eligiblePositions.Count < setupPawnData.maxPawns)
            {
                eligiblePositions = allEligiblePositions.Except(usedPositions).ToList();
            }
            for (int i = 0; i < setupPawnData.maxPawns; i++)
            {
                if (eligiblePositions.Count == 0)
                {
                    break;
                }
                int index = UnityEngine.Random.Range(0, eligiblePositions.Count);
                SVector2Int pos = eligiblePositions[index];
                eligiblePositions.RemoveAt(index);
                usedPositions.Add(pos);
                SPawn newPawn = new()
                {
                    pawnId = Guid.NewGuid(),
                    def = setupPawnData.pawnDef,
                    player = player,
                    pos = pos,
                    isSetup = false,
                    isAlive = true,
                    hasMoved = false,
                    isVisibleToOpponent = false,
                };
                sPawns.Add(newPawn);
            }
        }
        return sPawns.ToArray();
    }


    async void OnBothPlayersSetupSubmitted()
    {
        SPawn[] allPawns = new SPawn[redSetupPawns.Length + blueSetupPawns.Length];
        for (int i = 0; i < redSetupPawns.Length; i++)
        {
            allPawns[i] = redSetupPawns[i];
        }
        int blueSetupPawnsIndex = 0;
        for (int i = redSetupPawns.Length; i < allPawns.Length; i++)
        {
            allPawns[i] = blueSetupPawns[blueSetupPawnsIndex];
            blueSetupPawnsIndex++;
        }
        masterGameState = new SGameState((int)Player.NONE, lobbyBoardDef, allPawns);
        SGameState redInitialGameState = masterGameState.Censor((int)Player.RED);
        // blue state is unused in fake client
        SGameState blueInitialGameState = masterGameState.Censor((int)Player.BLUE);
        Response<SGameState> initialGameStateResponseRed = new()
        {
            requestId = Guid.Empty,
            success = true,
            data = redInitialGameState,
        };
        //await Task.Delay(1000);
        ProcessFakeResponse(initialGameStateResponseRed, MessageType.SETUPFINISHED);
    }
    
    void HandleSetupFinished(Response<SGameState> initialGameStateResponse)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            OnSetupFinishedResponse?.Invoke(initialGameStateResponse);
        });
    }
    
    void HandleMoveResponse(Response<bool> moveResponse)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            OnMoveResponse?.Invoke(moveResponse);
        });
        OnBothPlayersMoveSubmitted();
    }

    async void OnBothPlayersMoveSubmitted()
    {
        SQueuedMove? maybeBlueQueuedMove = masterGameState.GenerateValidMove((int)Player.BLUE);
        if (maybeBlueQueuedMove.HasValue)
        {
            blueQueuedMove = maybeBlueQueuedMove.Value;
        }
        else
        {
            // TODO: tell the players that the game is over
            return;
        }
        SGameState nextGameState = Resolve(masterGameState, redQueuedMove, blueQueuedMove);
        masterGameState = nextGameState;
        SGameState redGameState = masterGameState.Censor((int)Player.RED);
        
        Response<SGameState> redGameStateResponse = new Response<SGameState>
        {
            requestId = Guid.Empty,
            success = true,
            data = redGameState,
        };
        await Task.Delay(1000);
        ProcessFakeResponse(redGameStateResponse, MessageType.RESOLVE);

    }
    
    void HandleResolveResponse(Response<SGameState> resolveResponse)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            OnResolveResponse?.Invoke(resolveResponse);
        });
    }
    
    static SGameState Resolve(SGameState gameState, SQueuedMove redMove, SQueuedMove blueMove)
    {
        SGameState nextGameState = new SGameState()
        {
            player = (int)Player.NONE,
            boardDef = gameState.boardDef,
            pawns = (SPawn[])gameState.pawns.Clone(),
        };
        // check where red wants to go
        SPawn? maybePawnOnPos = gameState.GetPawnFromPos(redMove.pos);
        // if a pawn exists on this position
        if (maybePawnOnPos.HasValue)
        {
            SPawn pawnOnPos = maybePawnOnPos.Value;
            if (pawnOnPos.player == (int)Player.RED)
            {
                throw new Exception("NOT A VALID MOVE");
            }
            // pawn on pos is an enemy
            if (pawnOnPos.player == (int)Player.BLUE)
            {
                // if pawn on pos is also moving
                if (pawnOnPos.pawnId == blueMove.pawn.pawnId)
                {
                    // if enemy pawn is moving away
                }
            }
        }

        return nextGameState;
    }
    
    
}
