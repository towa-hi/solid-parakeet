using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public AppState appState = AppState.MAIN;
    public GameState gameState = null;
    public MainMenu mainmenu;
    // temp param, should be chosen by a UI widget later
    public BoardDef tempBoardDef;
    public Camera mainCamera;
    
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
        mainCamera = Camera.main;
        ChangeAppState(appState);
    }

    public void ChangeAppState(AppState inAppState)
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
        mainmenu.ShowMenu(true);
    }

    void OnGame()
    {
        // params should be passed in from a game settings widget later
        gameState = new GameState(tempBoardDef);
        mainmenu.ShowMenu(false);
        gameState.ChangePhase(GamePhase.SETUP);
        BoardManager.instance.StartBoard(gameState);
    }
    
    
}
