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
    public AppState appState = AppState.MAIN;
    public MainMenu mainMenu;
    public PawnSelector pawnSelector;
    public Camera mainCamera;

    public NetworkManager networkManager;
    public GameClient gameClient;
    
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
        gameClient = new GameClient(networkManager);
        ChangeAppState(appState);
        Globals.inputActions.Enable();
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
        SetIsLoading(true);
        try
        {
            // StartAsync: Connect to the server and send the alias
            //_ = gameClient.StartAsync("127.0.0.1", 12345, "bob");
            
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during matchmaking: {e.Message}");
            // Optionally, display an error message to the user via UI
        }
        
        //ChangeAppState(AppState.GAME);
    }

    void Update()
    {
        if (networkManager.isConnected)
        {
            networkManager.ProcessIncomingMessages();
        }
    }

    public async void OnPasswordEntered(string password)
    {
        PasswordModal.instance.Show(false);
        Debug.Log(password);
        int[] passwordInts = ParsePassword(password);
        SetIsLoading(true);
        try
        {
            // CreateNewGameAsync: Send NEWGAME message with the password
            //await gameClient.CreateNewGameAsync(passwordInts);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during matchmaking: {e.Message}");
            // Optionally, display an error message to the user via UI
        }
        finally
        {
            // Hide the loading screen
            SetIsLoading(false);
        }
    }
    
    private int[] ParsePassword(string password)
    {
        // Remove any non-digit and non-separator characters
        string cleanedPassword = Regex.Replace(password, "[^0-9, ]", "");

        // Split the string by commas or spaces
        string[] parts = cleanedPassword.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 5)
        {
            int[] passwordInts = new int[5];
            for (int i = 0; i < 5; i++)
            {
                if (!int.TryParse(parts[i], out passwordInts[i]))
                {
                    Debug.LogError($"Failed to parse part {i + 1}: '{parts[i]}'");
                    return null; // Parsing failed
                }
            }
            Debug.Log($"Parsed password with separators: [{string.Join(", ", passwordInts)}]");
            return passwordInts;
        }
        else if (cleanedPassword.Length == 5)
        {
            int[] passwordInts = new int[5];
            for (int i = 0; i < 5; i++)
            {
                char c = cleanedPassword[i];
                if (!char.IsDigit(c))
                {
                    Debug.LogError($"Non-digit character found at position {i + 1}: '{c}'");
                    return null; // Invalid character
                }
                passwordInts[i] = c - '0';
            }
            Debug.Log($"Parsed password without separators: [{string.Join(", ", passwordInts)}]");
            return passwordInts;
        }
        else
        {
            Debug.LogError($"Invalid password format. Expected 5 integers separated by commas/spaces or a continuous 5-digit number. Received: '{password}'");
            return null; // Invalid format
        }
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
    }

    void ResGameStarted(GameState state)
    {
        boardManager.StartBoard(Player.RED, state);
        boardManager2.StartBoard(Player.BLUE, state);
        SetIsLoading(false);
    }

    public void SetIsLoading(bool inIsLoading)
    {
        isLoading = inIsLoading;
        LoadingScreen.instance.ShowLoadingScreen(isLoading);
    }
}