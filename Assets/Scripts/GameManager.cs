using System;
using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

// NOTE: GameManager is a singleton and there can only be one per client. GameManager
// is responsible for taking in input from BoardManager and other UI or views and
// updating the singular gameState

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public AppState appState = AppState.MAIN;
    public MainMenu mainMenu;
    public PawnSelector pawnSelector;
    public Camera mainCamera;

    public NetworkManager networkManager;
    
    // clear these on new game
    //public GameState gameState = null;
    public BoardManager boardManager;
    public BoardManager boardManager2;
    
    // temp param, should be chosen by a UI widget later
    public BoardDef tempBoardDef;

    public bool isLoading;
    
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
        networkManager = new NetworkManager();
        networkManager.OnConnected += OnConnectedToServer;
        networkManager.OnDisconnected += OnDisconnectedFromServer;
        networkManager.OnMessageReceived += OnMessageReceivedFromServer;
        ChangeAppState(appState);
        Globals.inputActions.Enable();
        if (GameServer.instance == null)
        {
            GameServer.instance = new GameServer();
        }
        
    }

    void Update()
    {
        networkManager.ProcessIncomingMessages(HandleServerMessage);
    }

    void OnConnectedToServer()
    {
        Debug.Log("Connected to server");
    }

    void OnDisconnectedFromServer()
    {
        Debug.Log("Disconnected from server");
    }
    
    void OnMessageReceivedFromServer(string obj)
    {
        Debug.Log("obj");
    }
    
    void HandleServerMessage(string message)
    {
        Debug.Log("Received from server: " + message);
        GameMessage gameMessage = JsonConvert.DeserializeObject<GameMessage>(message);
        if (gameMessage != null)
        {
            switch (gameMessage.type)
            {
                case "welcome":
                    HandleWelcomeMessage(gameMessage.data);
                    break;
                case "echo":
                    HandleEchoMessage(gameMessage.data);
                    break;
                // Add more cases as needed
                default:
                    Debug.LogWarning("Unknown message type: " + gameMessage.type);
                    break;
            }
        }
        
    }
    
    void SendAlias()
    {
        string alias = "bob";
        if (!string.IsNullOrEmpty(alias))
        {
            // Create a GameMessage object
            GameMessage message = new GameMessage
            {
                type = "alias",
                data = new { alias = alias }
            };

            //Serialize to JSON
            string jsonMessage = JsonConvert.SerializeObject(message);
            
            // Send to server
            networkManager.SendMessageToServer(jsonMessage);
            Debug.Log("Sent alias to server: " + jsonMessage);

        }
        else
        {
            Debug.LogWarning("Alias cannot be empty.");
        }
    }

    void HandleWelcomeMessage(object data)
    {
        // Extract and display the welcome message
        string welcomeText = data.ToString();
    }

    void HandleEchoMessage(object data)
    {
        // Extract and display the echoed message
        string echoText = data.ToString();
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (networkManager != null)
        {
            networkManager.OnConnected -= OnConnectedToServer;
            networkManager.OnDisconnected -= OnDisconnectedFromServer;
            networkManager.OnMessageReceived -= OnMessageReceivedFromServer;
        }

        // Optionally, disconnect from the server when the GameManager is destroyed
        networkManager.Disconnect();
    }
    
    void ChangeAppState(AppState inAppState)
    {
        switch (inAppState)
        {
            case AppState.MAIN:
                OnMain();
                break;
            case AppState.GAME:
                OnGame();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(inAppState), inAppState, null);
        }
    }

    public void StartGame()
    {
        networkManager.ConnectToServer("127.0.0.1", 12345);
        //ChangeAppState(AppState.GAME);
    }

    public void OnTileClicked(TileView tileView, Vector2 mousePos)
    {
        // switch (GameServer.instance.gameState.phase)
        // {
        //     case GamePhase.UNINITIALIZED:
        //         break;
        //     case GamePhase.SETUP:
        //     {
        //         if (tileView.IsTileInteractableDuringSetup())
        //         {
        //             pawnSelector.OpenAndInitialize(tileView);
        //         }
        //         break;
        //     }
        //     case GamePhase.MOVE:
        //         break;
        //     case GamePhase.RESOLVE:
        //         break;
        //     case GamePhase.END:
        //         break;
        //     default:
        //         throw new ArgumentOutOfRangeException();
        // }
    }
    
    public void OnSetupPawnSelectorSelected(TileView tileView, PawnDef pawnDef)
    {
        //gameState.OnSetupPawnSelectorSelected(pawnDef,  tileView.tile);
    }
    
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    
    void OnMain()
    {
        // clear all game related stuff
        //gameState = null;
        boardManager.ClearBoard();
        mainMenu.ShowMainMenu(true);
    }

    async void OnGame()
    {
        PlayerProfile hostProfile = new PlayerProfile(Guid.NewGuid());
        PlayerProfile guestProfile = new PlayerProfile(Guid.NewGuid());
        // params should be passed in from a game settings widget later
        //gameState = new GameState(tempBoardDef);
        mainMenu.ShowMainMenu(false);
        SetIsLoading(true);
        GameServer.instance.GameStarted += ResGameStarted;
        await GameServer.instance.ReqStartGameAsync(tempBoardDef, hostProfile, guestProfile);
    }

    void ResGameStarted(GameState state)
    {
        boardManager.StartBoard(Player.RED, state);
        boardManager2.StartBoard(Player.BLUE, state);
        SetIsLoading(false);
    }

    void SetIsLoading(bool inIsLoading)
    {
        isLoading = inIsLoading;
        LoadingScreen.instance.ShowLoadingScreen(isLoading);
    }
}

