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
    public Camera mainCamera;

    public AppState appState = AppState.MAIN;
    public BoardManager boardManager;
    
    
    // temp param, should be chosen by a UI widget later
    public BoardDef tempBoardDef;

    public Action<string> onNicknameChanged;
    public NetworkManager networkManager;
    public GameClient client;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            networkManager = new NetworkManager();
            client = new GameClient(networkManager);
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
        
    }

    void Start()
    {
        
        
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
    
    public void OnTileClicked(TileView tileView, Vector2 mousePos)
    {

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
        boardManager.ClearBoard();
        //mainMenu.ShowMainMenu(true);
    }

    void OnGame()
    {
        PlayerProfile hostProfile = new PlayerProfile(Guid.NewGuid());
        PlayerProfile guestProfile = new PlayerProfile(Guid.NewGuid());
        // params should be passed in from a game settings widget later
        //gameState = new GameState(tempBoardDef);
        //mainMenu.ShowMainMenu(false);
        //SetIsLoading(true);
    }

    void ResGameStarted(GameState state)
    {
        boardManager.StartBoard(Player.RED, state);
    }
    
}