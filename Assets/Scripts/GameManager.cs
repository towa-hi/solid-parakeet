using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public AppState appState = AppState.MAIN;
    public MainMenu mainMenu;
    public PawnSelector pawnSelector;
    public Camera mainCamera;
    // temp param, should be chosen by a UI widget later
    public BoardDef tempBoardDef;

    // clear these on new game
    
    public GameState gameState = null;
    
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
        if (tileView.tile.tileSetup == TileSetup.NONE)
        {
            return;
        }
        
        pawnSelector.OpenAndInitialize(tileView);
    }
    
    public void OnSetupPawnSelectorSelected(TileView tileView, PawnDef pawnDef)
    {
        gameState.OnSetupPawnSelectorSelected(pawnDef, tileView.tile.pos);
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
        BoardManager.instance.ClearBoard();
        mainMenu.ShowMainMenu(true);
    }

    void OnGame()
    {
        // params should be passed in from a game settings widget later
        gameState = new GameState(tempBoardDef);
        mainMenu.ShowMainMenu(false);
        gameState.ChangePhase(GamePhase.SETUP);
        BoardManager.instance.StartBoard(true, gameState);
    }

    public void StartGame(bool isHost, BoardDef boardDef)
    {
        gameState = new GameState(boardDef);
        BoardManager.instance.StartBoard(isHost, gameState);
        
    }
    
}
