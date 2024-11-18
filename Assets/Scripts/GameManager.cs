using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

// NOTE: GameManager is a singleton and there can only be one per client. GameManager
// is responsible for taking in input from BoardManager and other UI or views and
// updating the singular gameState

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public BoardManager boardManager;
    public GuiManager guiManager;
    public BoardClickInputManager boardClickInputManager;
    public Action<string> onNicknameChanged;
    public IGameClient client;
    public bool offlineMode;
    public Camera mainCamera;
    
    public BoardDef tempBoardDef;
    
    public event Action<IGameClient, IGameClient> OnClientChanged;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
    }

    void Start()
    {
        guiManager.Initialize();
        Debug.Log("Enable input action");
        Globals.inputActions.Game.Enable();
    }
    
    public void SetOfflineMode(bool inOfflineMode)
    {
        offlineMode = inOfflineMode;
        IGameClient oldGameClient = client;
        if (inOfflineMode)
        {
            client = new FakeClient();
            Debug.Log("GameManager: Initialized FakeClient for offline mode.");
        }
        else
        {
            client = new GameClient();
            Debug.Log("GameManager: Initialized GameClient for online mode.");
        }
        client.OnRegisterClientResponse += OnRegisterClientResponse;
        client.OnDisconnect += OnDisconnect;
        client.OnErrorResponse += OnErrorResponse;
        client.OnRegisterNicknameResponse += OnRegisterNicknameResponse;
        client.OnGameLobbyResponse += OnGameLobbyResponse;
        client.OnLeaveGameLobbyResponse += OnLeaveGameLobbyResponse;
        client.OnReadyLobbyResponse += OnReadyLobbyResponse;
        client.OnDemoStarted += OnDemoStartedResponse;
        
        OnClientChanged?.Invoke(oldGameClient, client);
        _ = client.ConnectToServer();
    }
    
    void OnRegisterClientResponse(Response<string> response)
    {
        guiManager.OnRegisterClientResponse(response);
        _ = client.SendRegisterNickname(Globals.GetNickname());
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
        Debug.Log("GameManager: OnDemoStartedResponse");
        Debug.Log("GameManager: sent to boardmanager");
        boardManager.OnDemoStartedResponse(response);
        Debug.Log("GameManager: sent to guimanager");
        guiManager.OnDemoStartedResponse(response);
        Debug.Log("GameManager: sent to boardclickinputmanager");
        boardClickInputManager.Initialize(boardManager);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
