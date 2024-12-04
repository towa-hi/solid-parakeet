using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public BoardManager boardManager;
    public GuiManager guiManager;
    public CameraManager cameraManager;
    public IGameClient client;
    public bool offlineMode;
    
    public BoardDef tempBoardDef;
    public SSetupPawnData[] tempMaxPawnsArray;
    public List<KeyValuePair<PawnDef, int>> orderedPawnDefList;
    

    void Awake()
    {
        Debug.developerConsoleVisible = true;
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
        tempMaxPawnsArray = Globals.GetMaxPawnsArray();
        orderedPawnDefList = Globals.GetOrderedPawnList();
    }

    void Start()
    {
        guiManager.Initialize();
        cameraManager.Initialize();
        Debug.Log("Enable input action");
        Globals.inputActions.Game.Enable();
    }
    
    public void SetOfflineMode(bool inOfflineMode)
    {
        offlineMode = inOfflineMode;
        if (inOfflineMode)
        {
            client = new FakeClient();
            Debug.Log("GameManager: Initialized FakeClient for offline mode.");
        }
        else
        {
            //client = new GameClient();
            Debug.Log("GameManager: Initialized GameClient for online mode.");
        }
        client.OnRegisterClientResponse += OnRegisterClientResponse;
        client.OnDisconnect += OnDisconnect;
        client.OnErrorResponse += OnErrorResponse;
        client.OnRegisterNicknameResponse += OnRegisterNicknameResponse;
        client.OnGameLobbyResponse += OnGameLobbyResponse;
        client.OnLeaveGameLobbyResponse += OnLeaveGameLobbyResponse;
        client.OnReadyLobbyResponse += OnReadyLobbyResponse;
        client.OnDemoStartedResponse += OnDemoStartedResponse;
        client.OnSetupSubmittedResponse += OnSetupSubmittedResponse;
        client.OnSetupFinishedResponse += OnSetupFinishedResponse;
        client.OnMoveResponse += OnMoveResponse;
        client.OnResolveResponse += OnResolveResponse;
        
        client.ConnectToServer();
    }
    
    void OnRegisterClientResponse(Response<string> response)
    {
        guiManager.OnRegisterClientResponse(response);
        client.SendRegisterNickname(Globals.GetNickname());
    }
    
    void OnDisconnect(Response<string> response)
    {
        guiManager.OnDisconnect(response);
    }
    
    void OnErrorResponse(ResponseBase response)
    {
        Debug.LogError(response.message);
        guiManager.OnErrorResponse(response);
    }
    
    void OnRegisterNicknameResponse(Response<string> response)
    {
        guiManager.OnRegisterNicknameResponse(response);
    }
    
    void OnGameLobbyResponse(Response<SLobby> response)
    {
        guiManager.OnGameLobbyResponse(response);
    }
    
    void OnLeaveGameLobbyResponse(Response<string> response)
    {
        guiManager.OnLeaveGameLobbyResponse(response);
    }
    void OnReadyLobbyResponse(Response<SLobby> response)
    {
        guiManager.OnReadyLobbyResponse(response);
    }

    void OnDemoStartedResponse(Response<SSetupParameters> response)
    {
        boardManager.OnDemoStartedResponse(response);
        guiManager.OnDemoStartedResponse(response);
    }
    
    void OnSetupSubmittedResponse(Response<bool> response)
    {
        boardManager.OnSetupSubmittedResponse(response);
    }
    
    void OnSetupFinishedResponse(Response<SGameState> response)
    {
        boardManager.OnSetupFinishedResponse(response);
    }

    void OnMoveResponse(Response<bool> response)
    {
        boardManager.OnMoveResponse(response);
    }
    
    void OnResolveResponse(Response<SResolveReceipt> response)
    {
        boardManager.OnResolveResponse(response);
    }
    
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
