using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public AppState appState = AppState.MAIN;
    public GameState gameState = null;
    public MainMenu mainMenu;
    public PawnSelector pawnSelector;
    public Camera mainCamera;
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
        pawnSelector.OpenAndInitialize(tileView);
    }
    public void OnPawnSelectorSelected(TileView tileView, PawnDef inPawnDef)
    {
        if (inPawnDef != null)
        {
            Debug.Log(inPawnDef.pawnName + " selected");
        }
        
        Pawn pawn = gameState.AddPawn(inPawnDef, tileView.tile.pos);
        BoardManager.instance.SpawnPawn(pawn, tileView.tile.pos);
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
        BoardManager.instance.StartBoard(gameState);
    }
    
    
}
