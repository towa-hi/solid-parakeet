using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// NOTE: BoardManager is the root of all the stuff the player can
// interact with. Multiple boardManagers can subscribe to the same gameState 
// and use the same GameManager mainly for testing purposes. When networking is
// added there will only be one BoardManger per client but we need two now for testing.
// BoardManager only exists when a game is being played and should be cleanly destroyed
// by GameManager later

public class BoardManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    
    public Grid grid;

    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly List<PawnView> pawnViews = new();

    public Player player;
    
    void OnDestroy()
    {
        // if (GameManager.instance?.gameState != null)
        // {
        //     GameManager.instance.gameState.PawnAdded -= OnPawnAdded;
        //     GameManager.instance.gameState.PawnDeleted -= OnPawnDeleted;
        // }
    }

    public void StartBoard(Player inPlayer, GameState state)
    {
        if (inPlayer == Player.NONE)
        {
            throw new Exception("BoardManager cannot have player == Player.NONE");
        }
        player = inPlayer;
        state.PawnAdded += OnPawnAdded;
        state.PawnDeleted += OnPawnDeleted;
        LoadBoardData(state);
    }
    
    public void ClearBoard()
    {
        ClearPawns();
        ClearTiles();
    }
    
    void LoadBoardData(GameState state)
    {
        Debug.Log("BoardManager reading BoardDef from gameState");
        SpawnTiles(state);
    }
    
    void SpawnTiles(GameState state)
    {
        Debug.Log("BoardManager SpawnTiles()");
        BoardDef board = state.board;
        ClearTiles(); 
        
        for (int y = 0; y < board.boardSize.y; y++)
        {
            for (int x = 0; x < board.boardSize.x; x++)
            {
                // Get the position of the tile in the grid
                Vector3Int gridPosition = new(x, y, 0);
                Vector3 worldPosition = grid.CellToWorld(gridPosition);  // Convert grid position to world position
                // Spawn a tile at the grid position
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
                TileView tileView = tileObject.GetComponent<TileView>();
                Tile tile = board.tiles[x + y * board.boardSize.x];
                tileView.Initialize(tile, this);
                tileViews.Add(new Vector2Int(x, y), tileView);
            }
        }
    }
    void OnPawnAdded(Pawn pawn)
    {
        GameObject pawnObject = Instantiate(pawnPrefab, transform);
        PawnView pawnView = pawnObject.GetComponent<PawnView>();
        pawnView.Initialize(pawn, GetTileView(pawn.pos));
        pawnViews.Add(pawnView);
    }

    void OnPawnDeleted(Pawn pawn)
    {
        PawnView pawnView = GetPawnViewFromPawn(pawn);
        if (!pawnView) return;
        Destroy(pawnView.gameObject);
        pawnViews.Remove(pawnView);
    }
    
    void ClearTiles()
    {
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }
        tileViews.Clear();
    }

    void ClearPawns()
    {
        // clear pawns
        foreach (PawnView pawnView in pawnViews)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews.Clear();
    }

    public PawnView GetPawnViewAtPos(Vector2Int pos)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == pos);
    }

    public PawnView GetPawnViewFromPawn(Pawn pawn)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn == pawn);
    }
    
    public TileView GetTileView(Vector2Int pos)
    {
        return tileViews[pos];
    }

}
