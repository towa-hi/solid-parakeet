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

        // Invoke the event to notify listeners that the client has changed
        OnClientChanged?.Invoke(oldGameClient, client);
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
}
