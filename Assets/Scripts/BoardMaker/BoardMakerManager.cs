using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

public class BoardMakerManager : MonoBehaviour
{
    public BoardDef loadThisBoard;
    
    public BoardMakerTool currentTool;
    public ClickInputManager clickInputManager;
    public BoardGrid grid;
    public BoardDef currentlyLoadedBoardDef;
    public GuiBoardMaker gui;
    public GameObject boardMakerTilePrefab;
    public GameObject boardObject;
    public List<BoardMakerTile> tiles;
    public Vector2Int maxBoardSize = new Vector2Int(10, 10);
    public int currentSetupZone;
    public TextMeshProUGUI counter;
    
    void Start()
    {
        currentTool = BoardMakerTool.NONE;
        tiles = new List<BoardMakerTile>();
        Globals.InputActions.Game.Enable();
        clickInputManager.OnClick += OnClick;
    }

    void SetCounter()
    {
        if (!currentlyLoadedBoardDef)
        {
            counter.text = "no board loaded";
            return;
        }

        int red = 0;
        int blue = 0;
        foreach (var tile in tiles)
        {
            if (tile.setupTeam == Team.RED)
            {
                red++;
            }
            else if (tile.setupTeam == Team.BLUE)
            {
                blue++;
            }
        }
        string message = $"red: {red} blue: {blue}";
        counter.text = message;
    }
    
    void OnClick(Vector2 mousePos, Vector2Int pos)
    {
        if (!currentlyLoadedBoardDef)
        {
            return;
        }

        if (pos != Globals.Purgatory)
        {
            BoardMakerTile tile = GetTile(pos);
            switch (currentTool)
            {
                case BoardMakerTool.NONE:
                    break;
                case BoardMakerTool.PASSABLE:
                    tile.SetIsPassable(!tile.isPassable);
                    break;
                case BoardMakerTool.REDTEAM:
                    tile.SetSetupTeam(tile.setupTeam == Team.RED ? Team.NONE : Team.RED);
                    break;
                case BoardMakerTool.BLUETEAM:
                    tile.SetSetupTeam(tile.setupTeam == Team.BLUE ? Team.NONE : Team.BLUE);
                    break;
                case BoardMakerTool.SETUPZONE:
                    tile.SetSetupZone(currentSetupZone);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            SetCounter();
        }
        Debug.Log(pos);
    }

    public void SaveBoardButton()
    {
        int index = 0;
        BoardDef boardDefCopy = Object.Instantiate(currentlyLoadedBoardDef);
        boardDefCopy.tiles = new Tile[100];
        foreach (BoardMakerTile boardMakerTile in tiles)
        {
            Tile tile = new Tile();
            tile.pos = boardMakerTile.pos;
            tile.isPassable = boardMakerTile.isPassable;
            tile.setupTeam = boardMakerTile.setupTeam;
            tile.autoSetupZone = boardMakerTile.autoSetupZone;
            boardDefCopy.tiles[index] = tile;
            index++;
        }
        string directoryPath = "Assets/Resources/Boards";
        string randomName = $"BoardDef_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
        string assetPath = $"{directoryPath}/{randomName}.asset";
        AssetDatabase.CreateAsset(boardDefCopy, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"BoardDef asset saved at {assetPath}");
        LoadBoard(boardDefCopy);
    }
    
    public void LoadBoardButton()
    {
        if (!loadThisBoard)
        {
            Debug.LogError("LoadThisBoard must exist");
        }
        else
        {
            LoadBoard(loadThisBoard);
        }
    }
    
    public BoardDef CreateBoardDef(string path)
    {
        BoardDef boardDef = ScriptableObject.CreateInstance<BoardDef>();
        #if UNITY_EDITOR
            // Save the ScriptableObject as an asset in the specified path
            AssetDatabase.CreateAsset(boardDef, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created BoardDef at {path}");
        #endif

        return boardDef;
    }

    public void LoadBoard(BoardDef board)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            Destroy(tiles[i].gameObject);
        }
        tiles = new List<BoardMakerTile>();
        currentlyLoadedBoardDef = board;
        SBoardDef serializedBoardDef = new SBoardDef(currentlyLoadedBoardDef);
        grid.SetBoard(serializedBoardDef);
        if (!currentlyLoadedBoardDef)
        {
            Debug.LogError("LoadBoard could not load board");
            return;
        }
        grid.SetBoard(new SBoardDef(currentlyLoadedBoardDef));
        for (int y = 0; y < maxBoardSize.y; y++)
        {
            for (int x = 0; x < maxBoardSize.x; x++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                Vector3 worldPosition = grid.CellToWorld(currentPos);
                GameObject tileObject = Instantiate(boardMakerTilePrefab, worldPosition, Quaternion.identity, boardObject.transform);
                BoardMakerTile boardMakerTile = tileObject.GetComponent<BoardMakerTile>();
                boardMakerTile.Initialize(currentPos, currentlyLoadedBoardDef.isHex);
                tiles.Add(boardMakerTile);
            }
            
        }
        foreach (Tile tile in currentlyLoadedBoardDef.tiles)
        {
            BoardMakerTile boardMakerTile = GetTile(tile.pos);
            if (boardMakerTile)
            {
                boardMakerTile.LoadState(tile);
            }
        }
        SetCounter();
    }

    public BoardMakerTile GetTile(Vector2Int pos)
    {
        return tiles.FirstOrDefault(tile => tile.pos == pos);

    }

    public TextMeshProUGUI toolText;
    public void OnTogglePassable()
    {
        toolText.text = "Toggle Passable";
        currentTool = BoardMakerTool.PASSABLE;
    }

    public void OnSetRed()
    {
        toolText.text = "Set red";
        currentTool = BoardMakerTool.REDTEAM;
    }

    public void OnSetBlue()
    {
        toolText.text = "Set blue";
        currentTool = BoardMakerTool.BLUETEAM;
    }

    public void OnSetZone(int zone)
    {
        toolText.text = "Set zone " + zone;
        currentTool = BoardMakerTool.SETUPZONE;
        currentSetupZone = zone;
    }
}

public enum BoardMakerTool
{
    NONE,
    PASSABLE,
    REDTEAM,
    BLUETEAM,
    SETUPZONE,
}