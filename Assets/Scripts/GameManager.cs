using System;
using System.Diagnostics;
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

    // clear these on new game
    public GameState gameState = null;
    public BoardManager boardManager;
    public BoardManager boardManager2;
    
    // temp param, should be chosen by a UI widget later
    public BoardDef tempBoardDef;
    
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
        ChangeAppState(AppState.GAME);
    }

    public void OnTileClicked(TileView tileView, Vector2 mousePos)
    {
        switch (gameState.phase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
            {
                if (tileView.IsTileInteractableDuringSetup())
                {
                    pawnSelector.OpenAndInitialize(tileView);
                }
                break;
            }
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    public void OnSetupPawnSelectorSelected(TileView tileView, PawnDef pawnDef)
    {
        gameState.OnSetupPawnSelectorSelected(pawnDef,  tileView.tile);
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
        gameState = null;
        boardManager.ClearBoard();
        mainMenu.ShowMainMenu(true);
    }

    void OnGame()
    {
        // params should be passed in from a game settings widget later
        gameState = new GameState(tempBoardDef);
        mainMenu.ShowMainMenu(false);
        gameState.ChangePhase(GamePhase.SETUP);
        boardManager.StartBoard(Player.RED, gameState);
        
        // for testing
        boardManager2.StartBoard(Player.BLUE, gameState);
    }
}
