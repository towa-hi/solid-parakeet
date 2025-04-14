using System;
using System.Collections.Generic;
using Contract;
using UnityEngine;

public class TestBoardManager : MonoBehaviour
{
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public ClickInputManager clickInputManager;
    public Vortex vortex;

    public UserState userState;

    public Dictionary<Vector2Int, TileView> tileViews = new();
    public List<PawnView> pawnViews = new();

    public Vector2Int hoveredPos;
    public PawnView previousHoveredPawnView;
    public PawnView currentHoveredPawnView;
    public TileView previousHoveredTileView;
    public TileView currentHoveredTileView;

    public Transform waveStartPositionOne;
    public Transform waveStartPositionTwo;
    public float waveSpeed;
    
    public IPhase currentPhase;
    
    public event Action<IPhase> OnPhaseChanged;
    public event Action<PawnDef> OnSetupStateChanged;

    Lobby GetLobby()
    {
        Lobby? maybeLobby = StellarManagerTest.currentLobby;
        if (maybeLobby.HasValue)
        {
            return maybeLobby.Value;
        }
        else
        {
            throw new NullReferenceException();
        }
    }

    void Start()
    {
        //clickInputManager.Initialize(this);
    }

    BoardDef boardDef;
    public void StartGame()
    {
        Lobby lobby = GetLobby();
        BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
        foreach (var board in boardDefs)
        {
            if (board.name != lobby.parameters.board_def_name) continue;
            boardDef = board;
            break;
        }
        grid.SetBoard(new SBoardDef(boardDef));
        foreach (Tile tile in boardDef.tiles)
        {
            STile sTile = new STile(tile);
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(null, sTile,boardDef.isHex);
            tileViews.Add(tile.pos, tileView);
        }
    }
}
