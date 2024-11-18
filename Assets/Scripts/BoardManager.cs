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
    public GameObject setupPawnPrefab;
    public Grid grid;
    public GamePhase phase;
    
    // setup stuff
    
    public Player player;
    public SetupParameters setupParameters;
    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly List<SetupPawnView> setupPawnViews = new();
    public event Action<List<SetupPawnView>> OnSetupPawnViewsChanged;
    
    // game stuff
    readonly List<PawnView> pawnViews = new();
    
    void SetPhase(GamePhase inPhase)
    {
        GamePhase oldPhase = phase;
        switch (oldPhase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        phase = inPhase;
        switch (phase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        //OnPhaseChanged?.Invoke(oldPhase, phase);
    }
    
    public void OnDemoStartedResponse(Response<SSetupParameters> response)
    {
        setupParameters = new(response.data);
        Debug.Log("setup parameters set");
        player = response.data.player;
        Debug.Log("player set");
        Debug.Log("setupSelectedPawnDef set");
        LoadBoardData(setupParameters.board);
        Debug.Log("board set");
        SetPhase(GamePhase.SETUP);
        Debug.Log("phase set");
    }

    public void StartDemoGame()
    {
        bool setupValid = IsSetupValid(player);
    }

    bool IsSetupValid(Player targetPlayer)
    {
        Dictionary<PawnDef, int> pawnDefsOnBoard = new(setupParameters.maxPawnsDict);
        foreach (SetupPawnView setupPawnView in setupPawnViews.Where(setupPawnView => setupPawnView.pawn.player == targetPlayer))
        {
            if (!setupPawnView.pawn.isSetup)
            {
                Debug.LogError("isSetup was false");
                return false;
            }
            TileView tileView = GetTileView(setupPawnView.pawn.pos);
            if (tileView == null)
            {
                Debug.LogError("tile was null");
                return false;
            }
            if (!tileView.tile.IsTileEligibleForPlayer(targetPlayer))
            {
                Debug.LogError("tile was not eligible for player");
                return false;
            }
            if (pawnDefsOnBoard[setupPawnView.pawn.def] <= 0)
            {
                Debug.LogError("too many pawns of this type");
                return false;
            }
            pawnDefsOnBoard[setupPawnView.pawn.def] -= 1;
        }
        if (pawnDefsOnBoard.Values.Any(count => count != 0))
        {
            Debug.LogError("pawns remaining");
            return false;
        }
        Debug.Log("setup valid");
        return true;
    }
    
    SetupPawnView AddSetupPawnView(Player targetPlayer, PawnDef pawnDef, Vector2Int position)
    {
        // if  tile doesnt exist
        TileView tileView = GetTileView(position);
        if (!tileView)
        {
            return null;
        }
        // if tile is not eligible
        if (!tileView.tile.IsTileEligibleForPlayer(targetPlayer))
        {
            return null;
        }
        // if trying to place a pawn over max pawn limit
        int pawnsAlreadyPlaced = FindPawnPreviewCount(targetPlayer, pawnDef);
        if (pawnsAlreadyPlaced >= setupParameters.maxPawnsDict[pawnDef])
        {
            return null;
        }
        // if trying to place on a occupied position
        SetupPawnView pawnOnLocation = GetSetupPawnViewAtPos(position);
        if (pawnOnLocation)
        {
            return null;
        }
        // make preview
        GameObject setupPawnObject = Instantiate(setupPawnPrefab, transform);
        SetupPawnView setupPawnView = setupPawnObject.GetComponent<SetupPawnView>();
        Pawn setupPawn = new(pawnDef, targetPlayer, position, true);
        setupPawnView.Initialize(setupPawn, tileView);
        setupPawnViews.Add(setupPawnView);
        OnSetupPawnViewsChanged?.Invoke(setupPawnViews);
        return setupPawnView;
    }

    void DeleteSetupPawnView(SetupPawnView setupPawnView)
    {
        if (setupPawnView == null)
        {
            return;
        }
        setupPawnViews.Remove(setupPawnView);
        Destroy(setupPawnView.gameObject);
        OnSetupPawnViewsChanged?.Invoke(setupPawnViews);
    }
    
    SetupPawnView GetSetupPawnViewAtPos(Vector2Int position)
    {
        return setupPawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == position);
    }
    
    int FindPawnPreviewCount(Player targetPlayer, PawnDef targetPawnDef)
    {
        return setupPawnViews.Where(pawnView => pawnView.pawn.def == targetPawnDef).Count(pawnView => pawnView.pawn.player == targetPlayer);

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

    public void AutoSetup(Player targetPlayer)
    {
        if (targetPlayer == Player.NONE)
        {
            throw new Exception("Player can't be none");
        }
        ClearSetupPawns(targetPlayer);
        BoardDef boardDef = setupParameters.board;
        // Keep track of positions that have already been used
        HashSet<Vector2Int> usedPositions = new();
        // Get all eligible positions for the player
        List<Vector2Int> allEligiblePositions = tileViews.Values
            .Where(tileView => tileView.tile.IsTileEligibleForPlayer(targetPlayer))
            .Select(tileView => tileView.tile.pos)
            .ToList();
        foreach ((PawnDef pawnDef, int pawnCount) in setupParameters.maxPawnsDict)
        {
            List<Vector2Int> eligiblePositions = boardDef.GetEligiblePositionsForPawn(targetPlayer, pawnDef, usedPositions);
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
                SetupPawnView setupPawnView = AddSetupPawnView(targetPlayer, pawnDef, pos);
                if (setupPawnView == null)
                {
                    throw new Exception("setupPawnView was null");
                }
            }
        }
    }

    

    
    
    // void AddPawn(Pawn pawn)
    // {
    //     int pawnCount = FindPawnCount(player, pawn.def);
    //     if (pawnCount <= 0)
    //     {
    //         throw new Exception();
    //     }
    //     //pawnsLeft[pawn.def] -= 1;
    //     GameObject pawnObject = Instantiate(pawnPrefab, transform);
    //     PawnView pawnView = pawnObject.GetComponent<PawnView>();
    //     pawnView.Initialize(pawn, GetTileView(pawn.pos));
    //     //pawnViews.Add(pawnView);
    //     //OnPawnAdded?.Invoke(pawnsLeft, pawnView.pawn);
    // }

    // void DeletePawn(Pawn pawn)
    // {
    //     // TODO: make this not retarded
    //     //pawnsLeft[pawn.def] += 1;
    //     PawnView pawnView = GetPawnViewFromPawn(pawn);
    //     if (!pawnView) return;
    //     //pawnViews.Remove(pawnView);
    //     // Deactivate the GameObject immediately
    //     pawnView.gameObject.SetActive(false);
    //     pawnView.pawnClickableHandler.billboardClickable.enabled = false;
    //     pawnView.pawnClickableHandler.planeClickable.enabled = false;
    //     // Schedule destruction at the end of the frame
    //     StartCoroutine(DestroyPawnViewAtEndOfFrame(pawnView.gameObject));
    //     //OnPawnRemoved?.Invoke(pawnsLeft, pawnView.pawn);
    // }
    
    // IEnumerator DestroyPawnViewAtEndOfFrame(GameObject obj)
    // {
    //     yield return new WaitForEndOfFrame();
    //     Destroy(obj);
    // }
    
    void ClearTiles()
    {
        // this also clears all pawns
        ClearSetupPawns(Player.RED);
        ClearSetupPawns(Player.BLUE);
        ClearPawns();
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }
        tileViews.Clear();
    }
    
    void ClearSetupPawns(Player targetPlayer)
    {
        var pawnsToRemove = setupPawnViews.Where(spv => spv.pawn.player == targetPlayer).ToList();
        foreach (var spv in pawnsToRemove)
        {
            DeleteSetupPawnView(spv);
        }
    }
    
    void ClearPawns()
    {
        List<PawnView> tempPawnViews = new(pawnViews);
        foreach (PawnView pawnView in tempPawnViews)
        {
            DeletePawnView(pawnView);
        }
        pawnViews.Clear();
    }
    
    void DeletePawnView(PawnView pawnView)
    {
        if (pawnView == null)
        {
            return;
        }
        pawnViews.Remove(pawnView);
        Destroy(pawnView.gameObject);
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

    public void OnSetupPawnClicked(SetupPawnView setupPawnView)
    {
        
    }
    
    public void OnTileClicked(TileView tileView)
    {
        Debug.Log("tile clicked");
        // PawnView pawnViewOnTile = GetPawnViewAtPos(tileView.tile.pos);
        // if (pawnViewOnTile != null)
        // {
        //     if (pawnViewOnTile.pawn.def != setupSelectedPawnDef)
        //     {
        //         Debug.Log($"tileView {tileView.tile.pos} removing pawn to replace with another type or null");
        //         //DeletePawn(pawnViewOnTile.pawn);
        //     }
        //     else
        //     {
        //         Debug.LogWarning($"tileView {tileView.tile.pos} OK clicked but pawn of this def already exists");
        //         return;
        //     }
        // }
        //
        // if (setupSelectedPawnDef == null)
        // {
        //     Debug.Log($"tileView {tileView.tile.pos} cleared OK");
        //     return;
        // }
        //
        // if (!tileView.tile.IsTileEligibleForPlayer(player))
        // {
        //     Debug.LogWarning($"tileView {tileView.tile.pos} cleared but no new pawn added because not eligible");
        //     return;
        // }

        // if (pawnsLeft[setupSelectedPawnDef] <= 0)
        // {
        //     Debug.LogWarning($"tileView {tileView.tile.pos} cleared but no new pawn added because no pawns remaining of this def");;
        //     return;
        // }
        //AddPawn(newPawn);
        //AddSetupPawnView(player, setupSelectedPawnDef, tileView.tile.pos);
        Debug.Log($"tileView {tileView.tile.pos} added new pawn OK");
    }

    int FindPawnCount(Player targetPlayer, PawnDef targetPawnDef)
    {
        return pawnViews.Where(pawnView => pawnView.pawn.def == targetPawnDef).Count(pawnView => pawnView.pawn.player == targetPlayer);
    }

}
