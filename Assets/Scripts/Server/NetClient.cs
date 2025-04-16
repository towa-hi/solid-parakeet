using System;
using UnityEngine;

public class NetClient
{
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

    public NetClient()
    {
        
    }
    
    public void ConnectToServer()
    {
        var response = new Response<string>
        {
            requestId = new Guid(),
            success = true,
            responseCode = 0,
            data = "Client registered successfully.",
        };
        OnRegisterClientResponse?.Invoke(response);
    }

    public void SendRegisterNickname(string nicknameInput)
    {
        var response = new Response<string>
        {
            requestId = new Guid(),
            success = true,
            responseCode = 0,
            data = nicknameInput,
        };
        OnRegisterNicknameResponse?.Invoke(response);
    }

    public void SendGameLobby(SLobbyParameters lobbyParameters)
    {
        throw new NotImplementedException();
    }

    public void SendGameLobbyLeaveRequest()
    {
        throw new NotImplementedException();
    }

    public void SendGameLobbyReadyRequest(bool ready)
    {
        throw new NotImplementedException();
    }

    public void SendStartGameDemoRequest()
    {
        throw new NotImplementedException();
    }

    public void SendSetupSubmissionRequest(SSetupPawn[] setupPawnList)
    {
        throw new NotImplementedException();
    }

    public void SendMove(SQueuedMove move)
    {
        throw new NotImplementedException();
    }
}
