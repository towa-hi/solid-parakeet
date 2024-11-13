using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// BoardManager is responsible for managing the board state, including tiles and pawns.
// It handles the setup of the board and pawns for a given player.

public class BoardManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public Grid grid;

    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly List<PawnView> pawnViews = new();
    
    public Player player;
    
    // setup stuff
    public SetupParameters setupParameters;
    public Dictionary<PawnDef, int> pawnsLeft;
    public PawnDef setupSelectedPawnDef = null;

    public event Action<Dictionary<PawnDef, int>, Pawn> OnPawnAdded; 
    public event Action<Dictionary<PawnDef, int>, Pawn> OnPawnRemoved;
    
    public void StartBoardSetup(Player inPlayer, SetupParameters inSetupParameters)
    {
        setupParameters = inSetupParameters;
        pawnsLeft = new();
        setupSelectedPawnDef = null;
        if (inPlayer == Player.NONE)
        {
            throw new Exception("BoardManager cannot have player == Player.NONE");
        }
        player = inPlayer;
        pawnsLeft = new Dictionary<PawnDef, int>(setupParameters.maxPawnsDict);
        LoadBoardData(setupParameters.board);
    }
    
    void LoadBoardData(BoardDef boardDef)
    {
        Debug.Log("BoardManager reading BoardDef from setupParameters");
        ClearTiles();
        for (int y = 0; y < boardDef.boardSize.y; y++)
        {
            for (int x = 0; x < boardDef.boardSize.x; x++)
            {
                // Get the position of the tile in the grid
                Vector3Int gridPosition = new(x, y, 0);
                Vector3 worldPosition = grid.CellToWorld(gridPosition);  // Convert grid position to world position
                // Spawn a tile at the grid position
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
                TileView tileView = tileObject.GetComponent<TileView>();
                Tile tile = boardDef.tiles[x + y * boardDef.boardSize.x];
                tileView.Initialize(tile, this);
                tileViews.Add(new(x, y), tileView);
            }
        }
    }

    public void AutoSetup()
    {
        ClearPawns();
        BoardDef boardDef = setupParameters.board;
        // Keep track of positions that have already been used
        HashSet<Vector2Int> usedPositions = new();
        // Get all eligible positions for the player
        List<Vector2Int> allEligiblePositions = tileViews.Values
            .Where(tileView => tileView.tile.IsTileEligibleForPlayer(player))
            .Select(tileView => tileView.tile.pos)
            .ToList();
        foreach ((PawnDef pawnDef, int pawnCount) in setupParameters.maxPawnsDict)
        {
            List<Vector2Int> eligiblePositions = boardDef.GetEligiblePositionsForPawn(player, pawnDef, usedPositions);
            // Check if there are enough eligible positions
            if (eligiblePositions.Count < pawnCount)
            {
                Debug.LogWarning($"Not enough eligible positions for pawn '{pawnDef.name}'. Ignoring placement rules for this pawn type.");
                eligiblePositions = allEligiblePositions.Except(usedPositions).ToList();
            }
            // Place the pawns randomly on the eligible positions
            for (int i = 0; i < pawnCount; i++)
            {
                if (eligiblePositions.Count == 0)
                {
                    Debug.LogWarning($"No more eligible positions available for pawn '{pawnDef.name}'.");
                    break;
                }
                int index = UnityEngine.Random.Range(0, eligiblePositions.Count);
                Vector2Int pos = eligiblePositions[index];
                eligiblePositions.RemoveAt(index);
                usedPositions.Add(pos);
                Pawn pawn = new(pawnDef, player, pos);
                AddPawn(pawn);
            }
        }
    }
    
    void AddPawn(Pawn pawn)
    {
        int pawnCount = FindPawnCount(pawn.def);
        if (pawnCount <= 0)
        {
            throw new Exception();
        }
        pawnsLeft[pawn.def] -= 1;
        GameObject pawnObject = Instantiate(pawnPrefab, transform);
        PawnView pawnView = pawnObject.GetComponent<PawnView>();
        pawnView.Initialize(pawn, GetTileView(pawn.pos));
        pawnViews.Add(pawnView);
        OnPawnAdded?.Invoke(pawnsLeft, pawnView.pawn);
    }

    void DeletePawn(Pawn pawn)
    {
        // TODO: make this not retarded
        pawnsLeft[pawn.def] += 1;
        PawnView pawnView = GetPawnViewFromPawn(pawn);
        if (!pawnView) return;
        pawnViews.Remove(pawnView);
        // Deactivate the GameObject immediately
        pawnView.gameObject.SetActive(false);
        pawnView.pawnClickableHandler.billboardClickable.enabled = false;
        pawnView.pawnClickableHandler.planeClickable.enabled = false;
        // Schedule destruction at the end of the frame
        StartCoroutine(DestroyPawnViewAtEndOfFrame(pawnView.gameObject));
        OnPawnRemoved?.Invoke(pawnsLeft, pawnView.pawn);
    }
    
    IEnumerator DestroyPawnViewAtEndOfFrame(GameObject obj)
    {
        yield return new WaitForEndOfFrame();
        Destroy(obj);
    }
    
    void ClearTiles()
    {
        // this also clears all pawns
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
            PawnView pawnView = GetPawnViewAtPos(tileView.tile.pos);
            if (pawnView)
            {
                DeletePawn(pawnView.pawn);
            }
        }
        tileViews.Clear();
    }

    void ClearPawns()
    {
        List<PawnView> tempPawnViews = new(pawnViews);
        foreach (PawnView pawnView in tempPawnViews)
        {
            DeletePawn(pawnView.pawn);
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
        if (tileViews.TryGetValue(pos, out TileView tileView))
        {
            return tileView;
        }
        else
        {
            Debug.LogError($"TileView not found at position {pos}");
            return null;
        }
    }

    public void OnTileClicked(TileView tileView)
    {
        Debug.Log("tile clicked");
        PawnView pawnViewOnTile = GetPawnViewAtPos(tileView.tile.pos);
        if (pawnViewOnTile != null)
        {
            if (pawnViewOnTile.pawn.def != setupSelectedPawnDef)
            {
                Debug.Log($"tileView {tileView.tile.pos} removing pawn to replace with another type or null");
                DeletePawn(pawnViewOnTile.pawn);
            }
            else
            {
                Debug.LogWarning($"tileView {tileView.tile.pos} OK clicked but pawn of this def already exists");
                return;
            }
        }

        if (setupSelectedPawnDef == null)
        {
            Debug.Log($"tileView {tileView.tile.pos} cleared OK");
            return;
        }

        if (!tileView.tile.IsTileEligibleForPlayer(player))
        {
            Debug.LogWarning($"tileView {tileView.tile.pos} cleared but no new pawn added because not eligible");
            return;
        }

        if (pawnsLeft[setupSelectedPawnDef] <= 0)
        {
            Debug.LogWarning($"tileView {tileView.tile.pos} cleared but no new pawn added because no pawns remaining of this def");;
            return;
        }
        Pawn newPawn = new(setupSelectedPawnDef, player, tileView.tile.pos);
        AddPawn(newPawn);
        Debug.Log($"tileView {tileView.tile.pos} added new pawn OK");
    }

    int FindPawnCount(PawnDef targetPawnDef)
    {
        foreach (var pair in pawnsLeft)
        {
            if (pair.Key == targetPawnDef)
            {
                return pair.Value; // Return the associated int if the PawnDef matches
            }
        }
        // Return a default value if the PawnDef is not found
        return -1;
    }
}
