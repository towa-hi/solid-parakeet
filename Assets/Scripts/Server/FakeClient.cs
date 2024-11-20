using System;
using System.Collections.Generic;
using System.Linq;
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
    public event Action<Response<SSetupParameters>> OnDemoStartedResponse;
    public event Action<Response<bool>> OnSetupSubmittedResponse;
    public event Action<Response<SInitialGameState>> OnSetupFinishedResponse;
    
    // Internal state
    Guid clientId;
    bool isConnected;
    bool isNicknameRegistered;
    SLobby currentLobby;

    // simulated state of server lobby
    SLobby fakeServerLobby;
    SSetupParameters lobbySetupParameters;
    SBoardDef lobbyBoardDef;
    List<SPawn> redPlayerSetupPawns;
    
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
        SSetupParameters setupParameters = new(Player.RED, currentLobby.sBoardDef);
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
            pawns = setupPawnList,
        };
        await SendFakeRequestToServer(MessageType.GAMESETUP, setupRequest);
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
                lobbySetupParameters = startGameRequest.setupParameters;
                response = new Response<SSetupParameters>
                {
                    data = startGameRequest.setupParameters,
                };
                break;
            case SetupRequest setupRequest:
                redPlayerSetupPawns = setupRequest.pawns;
                // we assume the fake server already has the other players setupRequest
                response = new Response<bool>
                {
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
                Response<SInitialGameState> setupFinishedResponse = (Response<SInitialGameState>)response;
                HandleSetupFinished(setupFinishedResponse);
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
        SetNextResponseAfterDelay();
    }

    
    async void SetNextResponseAfterDelay()
    {
        await Task.Delay(1000);
        List<SPawn> sPawns = new List<SPawn>(redPlayerSetupPawns);
        Debug.Log($"sPawns count: {sPawns.Count}");
        int opponentPlayer = (int)Player.BLUE;
        // auto setup pawns
        SBoardDef sBoardDef = lobbySetupParameters.board;
        HashSet<SVector2Int> usedPositions = new();
        List<SVector2Int> allEligiblePositions = new();
        foreach (STile sTile in sBoardDef.tiles)
        {
            if (sTile.IsTileEligibleForPlayer(opponentPlayer))
            {
                allEligiblePositions.Add(sTile.pos);
            }
        }
        foreach (SSetupPawnData setupPawnData in lobbySetupParameters.setupPawnDatas)
        {
            List<SVector2Int> eligiblePositions = sBoardDef.GetEligiblePositionsForPawn(opponentPlayer, setupPawnData.pawnDef, usedPositions);
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
                SPawn newPawn = new SPawn()
                {
                    pawnId = Guid.NewGuid(),
                    def = setupPawnData.pawnDef,
                    player = opponentPlayer,
                    pos = pos,
                    isSetup = false,
                    isAlive = true,
                    hasMoved = false,
                    isVisibleToPlayer = true,
                };
                sPawns.Add(newPawn);
            }
        }

        List<SPawn> redPlayerPawnList = new List<SPawn>();
        List<SPawn> bluePlayerPawnList = new List<SPawn>();
        //now process all pawns
        foreach (SPawn sPawn in sPawns)
        {
            sPawn.isAlive = true;
            sPawn.isSetup = false;
            SPawn redVersionPawn = new SPawn(sPawn);
            if (sPawn.player != (int)Player.RED)
            {
                redVersionPawn.def = null;
                redVersionPawn.isVisibleToPlayer = false;
            }
            redPlayerPawnList.Add(redVersionPawn);
            SPawn blueVersionPawn = new SPawn(sPawn);
            if (sPawn.player != (int)Player.BLUE)
            {
                blueVersionPawn.def = null;
                blueVersionPawn.isVisibleToPlayer = false;
            }
            bluePlayerPawnList.Add(blueVersionPawn);
        }
        // blue state is unused in fake client
        SInitialGameState blueInitialGameState = new((int)Player.BLUE, bluePlayerPawnList, lobbyBoardDef);
        SInitialGameState redInitialGameState = new ((int)Player.RED, redPlayerPawnList, lobbyBoardDef);
        Response<SInitialGameState> initialGameStateResponseRed = new Response<SInitialGameState>
        {
            data = redInitialGameState,
        };
        ProcessFakeResponse(initialGameStateResponseRed, MessageType.SETUPFINISHED);
    }
    
    
    void HandleSetupFinished(Response<SInitialGameState> initialGameStateResponse)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            OnSetupFinishedResponse?.Invoke(initialGameStateResponse);
        });
    }
}
