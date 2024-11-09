using System;
using System.Threading.Tasks;
using UnityEngine;

public class FakeClient : IGameClient
{
    // Events
    public event Action<Response<string>> OnRegisterClientResponse;
    public event Action<Response<string>> OnDisconnect;
    public event Action<ResponseBase> OnErrorResponse;
    public event Action<Response<string>> OnRegisterNicknameResponse;
    public event Action<Response<SLobby>> OnGameLobbyResponse;
    public event Action<Response<string>> OnLeaveGameLobbyResponse;
    public event Action<Response<string>> OnJoinGameLobbyResponse;
    public event Action<Response<SLobby>> OnReadyLobbyResponse;
    public event Action OnDemoStarted;
    public event Action OnLobbyResponse;

    // Internal state
    private Guid clientId;
    private bool isConnected = false;
    private bool isNicknameRegistered = false;
    private SLobby currentLobby;

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
        Debug.Log("FakeClient: Simulating connection to server...");
        // Simulate connection delay
        await Task.Delay(100);
        isConnected = true;
        Debug.Log("FakeClient: Simulated connection established.");

        // Simulate server sending a client registration response
        SimulateRegisterClientResponse();
    }

    public async Task SendRegisterNickname(string nicknameInput)
    {
        Debug.Log($"FakeClient: Sending register nickname request with nickname '{nicknameInput}'");

        if (!Globals.IsNicknameValid(nicknameInput))
        {
            Debug.LogError($"FakeClient: Invalid nickname '{nicknameInput}'.");
            SimulateErrorResponse("Invalid nickname.", MessageType.REGISTERNICKNAME);
            return;
        }

        // Simulate server processing delay
        await Task.Delay(100);

        // Simulate successful nickname registration
        isNicknameRegistered = true;
        PlayerPrefs.SetString("nickname", nicknameInput);
        Debug.Log($"FakeClient: Nickname '{nicknameInput}' registered successfully.");

        var response = new Response<string>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = nicknameInput
        };

        OnRegisterNicknameResponse?.Invoke(response);
    }

    public async Task SendGameLobby()
    {
        Debug.Log("FakeClient: Sending game lobby creation request...");

        // Simulate server processing delay
        await Task.Delay(100);

        // Create a simulated lobby
        currentLobby = new SLobby
        {
            lobbyId = Guid.NewGuid(),
            hostId = clientId,
            guestId = Guid.Empty, // No guest in single-player
            sBoardDef = new SBoardDef(GameManager.instance.tempBoardDef),
            gameMode = 0,
            isGameStarted = false,
            password = "offline",
            hostReady = false,
            guestReady = false
        };

        Debug.Log($"FakeClient: Game lobby created with lobbyId {currentLobby.lobbyId}");

        var response = new Response<SLobby>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = currentLobby
        };

        OnGameLobbyResponse?.Invoke(response);
    }

    public async Task SendGameLobbyLeaveRequest()
    {
        Debug.Log("FakeClient: Sending game lobby leave request...");

        // Simulate server processing delay
        await Task.Delay(100);

        currentLobby = null;

        Debug.Log("FakeClient: Left the lobby successfully.");

        var response = new Response<string>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = "Left the lobby successfully."
        };

        OnLeaveGameLobbyResponse?.Invoke(response);
    }

    public async Task SendGameLobbyJoinRequest(string password)
    {
        Debug.Log($"FakeClient: Sending game lobby join request with password '{password}'");

        // Simulate server processing delay
        await Task.Delay(100);

        // Simulate successful lobby join
        Debug.Log("FakeClient: Joined the lobby successfully.");

        var response = new Response<string>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = "Joined the lobby successfully."
        };

        OnJoinGameLobbyResponse?.Invoke(response);
    }

    public async Task SendGameLobbyReadyRequest(bool ready)
    {
        Debug.Log($"FakeClient: Sending game lobby ready request. Ready status: {ready}");

        // Simulate server processing delay
        await Task.Delay(100);

        if (currentLobby == null)
        {
            Debug.LogError("FakeClient: No lobby found to set ready status.");
            SimulateErrorResponse("No lobby found.", MessageType.READYLOBBY);
            return;
        }

        currentLobby.hostReady = ready;

        Debug.Log("FakeClient: Lobby ready status updated.");

        var response = new Response<SLobby>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = currentLobby
        };

        OnReadyLobbyResponse?.Invoke(response);

        // If host is ready, start the game
        if (currentLobby.hostReady)
        {
            SimulateGameStart();
        }
    }

    public async Task StartGameDemoRequest()
    {
        Debug.Log("FakeClient: Starting game demo...");

        // Simulate starting a demo game
        await Task.Delay(100);
        Debug.Log("FakeClient: Game demo started.");
        OnDemoStarted?.Invoke();
    }

    // Helper methods to simulate server responses

    private void SimulateRegisterClientResponse()
    {
        Debug.Log("FakeClient: Simulating register client response...");

        var response = new Response<string>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = "Client registered successfully."
        };

        OnRegisterClientResponse?.Invoke(response);
        Debug.Log("FakeClient: Register client response sent.");

        // Automatically send nickname registration after client registration
        SendRegisterNickname(Globals.GetNickname());
    }

    private void SimulateErrorResponse(string message, MessageType messageType)
    {
        Debug.LogError($"FakeClient: Simulating error response. MessageType: {messageType}, Message: {message}");

        var response = new ResponseBase
        {
            requestId = Guid.NewGuid(),
            success = false,
            responseCode = 1,
            message = message
        };

        OnErrorResponse?.Invoke(response);
    }

    private void SimulateGameStart()
    {
        Debug.Log("FakeClient: Simulating game start...");

        currentLobby.isGameStarted = true;

        var response = new Response<SLobby>
        {
            requestId = Guid.NewGuid(),
            success = true,
            responseCode = 0,
            data = currentLobby,
            message = "Game started."
        };

        // You might have an event for game start
        OnReadyLobbyResponse?.Invoke(response);

        Debug.Log("FakeClient: Game started.");
    }
}
