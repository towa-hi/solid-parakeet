using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;

// BoardManager is responsible for managing the board state, including tiles and pawns.
// It handles the setup of the board and pawns for a given player.

public class BoardManager : MonoBehaviour
{
    public Transform purgatory;
    public BoardClickInputManager boardClickInputManager;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public Grid grid;
    public GamePhase phase;
    
    // setup stuff
    
    public ClickInputManager clickInputManager;
    public Player player;
    public Player opponentPlayer;
    public SetupParameters setupParameters;
    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    public List<PawnView> pawnViews = new();
    public Vector2Int hoveredPos;
    public event Action<PawnChanges> OnPawnModified; // subscribed to by all pawnviews
    public event Action<GamePhase, GamePhase> OnPhaseChanged;
    
    public PawnView currentHoveredPawnView;
    public TileView currentHoveredTileView;

    bool setupIsSubmitted;
    
    // game stuff
    public SGameState serverGameState;
    public PawnView selectedPawnView;
    public QueuedMove queuedMove;
    List<TileView> highlightedTileViews = new();
    List<PawnView> highlightedPawnViews = new();

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
            case GamePhase.WAITING:
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
            case GamePhase.WAITING:
                clickInputManager.Reset();
                SelectPawnView(null);
                break;
            case GamePhase.MOVE:
                clickInputManager.Reset();
                SelectPawnView(null);
                ClearQueueMove();
                break;
            case GamePhase.RESOLVE:
                SelectPawnView(null);
                clickInputManager.Reset();
                if (queuedMove == null)
                {
                    throw new Exception("Queuedmove cant be null in resolve phase");
                }
                TileView queuedTileMove = GetTileView(queuedMove.pos);
                queuedTileMove.OnArrow(false);
                
                foreach (var tileView in highlightedTileViews)
                {
                    tileView.OnHighlight(false);
                }
                foreach (var pawnView in highlightedPawnViews)
                {
                    pawnView.OnHighlight(false);
                }
                queuedMove = null;
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        OnPhaseChanged?.Invoke(oldPhase, phase);
    }
    
    public void OnDemoStartedResponse(Response<SSetupParameters> response)
    {
        setupParameters = response.data.ToUnity();
        Debug.Log("setup parameters set");
        player = (Player)setupParameters.player;
        opponentPlayer = player == Player.RED ? Player.BLUE : Player.RED;
        Debug.Log("player set");
        Debug.Log("setupSelectedPawnDef set");
        LoadBoardData(setupParameters.board);
        LoadPawnViews(player, setupParameters.maxPawnsDict);
        Debug.Log("board set");
        Debug.Log("phase set");
        clickInputManager.OnPositionHovered += OnPositionHovered;
        clickInputManager.OnClick += OnClick;
        clickInputManager.Initialize();
        SetPhase(GamePhase.SETUP);
    }
    
    public void OnSetupSubmittedResponse(Response<bool> response)
    {
        if (response.data)
        {
            setupIsSubmitted = true;
            SetPhase(GamePhase.WAITING);
        }
        else
        {
            SetPhase(GamePhase.SETUP);
        }
    }

    public void OnSetupFinishedResponse(Response<SGameState> response)
    {
        SGameState initialGameState = response.data;
        UpdateState(initialGameState);
        SetPhase(GamePhase.MOVE);
    }

    void UpdateState(SGameState inServerGameState)
    {
        serverGameState = inServerGameState;
        foreach (SPawn sPawn in inServerGameState.pawns)
        {
            Pawn pawn = GetPawnById(sPawn.pawnId);
            if (pawn != null)
            {
                PawnChanges pawnChanges = new()
                {
                    pawn = pawn,
                };
                if (pawn.pos != sPawn.pos.ToUnity())
                {
                    pawnChanges.posChanged = true;
                    pawn.pos = sPawn.pos.ToUnity();
                }
                if (pawn.isSetup != sPawn.isSetup)
                {
                    pawnChanges.isSetupChanged = true;
                    pawn.isSetup = sPawn.isSetup;
                }
                if (pawn.isAlive != sPawn.isAlive)
                {
                    pawnChanges.isAliveChanged = true;
                    pawn.isAlive = sPawn.isAlive;
                }
                if (pawn.hasMoved != sPawn.hasMoved)
                {
                    pawnChanges.hasMovedChanged = true;
                    pawn.hasMoved = sPawn.hasMoved;
                }
                if (pawn.isVisibleToOpponent != sPawn.isVisibleToOpponent)
                {
                    pawnChanges.isVisibleToOpponentChanged = true;
                    pawn.isVisibleToOpponent = sPawn.isVisibleToOpponent;
                }
                OnPawnModified?.Invoke(pawnChanges);
            }
            else
            {
                TileView tileView = GetTileView(sPawn.pos.ToUnity());
                Pawn newPawn = sPawn.ToUnity();
                GameObject pawnObject = Instantiate(pawnPrefab, transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnViews.Add(pawnView);
                pawnView.Initialize(newPawn, tileView);
            }
        }
    }
    
    void OnPositionHovered(Vector2Int oldPos, Vector2Int newPos)
    {
        // Store references to previous hovered pawn and tile
        PawnView previousHoveredPawnView = currentHoveredPawnView;
        TileView previousHoveredTileView = currentHoveredTileView;

        // Update current hovered pawn and tile based on new position
        if (IsPosValid(newPos))
        {
            currentHoveredPawnView = GetPawnViewFromPos(newPos);
            currentHoveredTileView = GetTileView(newPos);
        }
        else
        {
            currentHoveredPawnView = null;
            currentHoveredTileView = null;
        }

        // Check if the hovered pawn has changed
        if (previousHoveredPawnView != currentHoveredPawnView)
        {
            // Unhover the previous pawn
            if (previousHoveredPawnView != null)
            {
                previousHoveredPawnView.OnHovered(false);
            }
            // Hover the new pawn
            if (currentHoveredPawnView != null)
            {
                currentHoveredPawnView.OnHovered(true);
            }
        }

        // Check if the hovered tile has changed
        if (previousHoveredTileView != currentHoveredTileView)
        {
            // Unhover the previous tile
            if (previousHoveredTileView != null)
            {
                previousHoveredTileView.OnHovered(false);
            }
            // Hover the new tile
            if (currentHoveredTileView != null)
            {
                currentHoveredTileView.OnHovered(true);
            }
        }

        // Update the hovered position
        hoveredPos = newPos;
    }

    public void OnMoveResponse(Response<bool> response)
    {
        if (response.data)
        {
            SetPhase(GamePhase.WAITING);
        }
        else
        {
            SetPhase(GamePhase.MOVE);
        }
    }
    
    
    public void OnResolveResponse(Response<SGameState> response)
    {
        if (response.success)
        {
            // apply diff here
            
            SetPhase(GamePhase.MOVE);
        }
    }
    
    void OnClick(Vector2 screenPointerPosition, Vector2Int hoveredPosition)
    {
        switch (phase)
        {
            case GamePhase.UNINITIALIZED:
                // Do nothing
                break;

            case GamePhase.SETUP:
                if (!IsPosValid(hoveredPosition))
                {
                    // Invalid position; do nothing
                    break;
                }

                if (currentHoveredPawnView != null)
                {
                    // Remove the pawn at the hovered position
                    SendPawnToPurgatory(currentHoveredPawnView.pawn);
                }
                else if (setupSelectedPawnDef != null && GetPawnViewFromPos(hoveredPosition) == null)
                {
                    // Place a new pawn if a pawnDef is selected and no pawn is at this position
                    GetPawnFromPurgatoryByPawnDef(player, setupSelectedPawnDef, hoveredPosition);
                }
                // If no pawnDef is selected or a pawn is already at this position, do nothing
                break;
            case GamePhase.WAITING:
                // do nothing
                break;
            case GamePhase.MOVE:
                if (!IsPosValid(hoveredPosition))
                {
                    if (selectedPawnView != null)
                    {
                        SelectPawnView(null);
                    }
                    break;
                }

                if (selectedPawnView != null)
                {
                    if (currentHoveredPawnView != null && currentHoveredPawnView.pawn.player == player)
                    {
                        if (currentHoveredPawnView == selectedPawnView)
                        {
                            SelectPawnView(null);
                            Debug.Log("OnClick: deselected because clicked the selected pawn");
                        }
                        else
                        {
                            SelectPawnView(currentHoveredPawnView);
                            Debug.Log("OnClick: selected a different pawn");
                        }
                    }
                    else
                    {
                        bool success = TryQueueMove(selectedPawnView, hoveredPosition);
                        if (success)
                        {
                            Debug.Log("OnClick: tried to go to a tile");
                            SelectPawnView(null);
                        }
                        else
                        {
                            SelectPawnView(null);
                            Debug.Log("OnClick: deselected because couldn't go to tile");
                        }
                    }
                }
                else
                {
                    if (currentHoveredPawnView != null && currentHoveredPawnView.pawn.player == player)
                    {
                        SelectPawnView(currentHoveredPawnView);
                        Debug.Log("OnClick: selecting pawn");
                    }
                    else
                    {
                        Debug.Log("OnClick: doing nothing, clicked an empty tile with nothing selected");
                    }
                }
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    bool TryQueueMove(PawnView pawnView, Vector2Int pos)
    {
        Debug.Log($"TryQueueMove at {pos}");
        SPawn pawnOriginalState = GetPawnStateById(pawnView.pawn.pawnId);
        STile[] movableTilesList = serverGameState.GetMovableTiles(pawnOriginalState);
        // check if valid move
        // queue a new PawnAction to go to that position
        bool moveIsValid = movableTilesList.Any(tile => tile.pos.ToUnity() == pos);
        if (!moveIsValid)
        {
            return false;
        }
        if (queuedMove != null)
        {
            TileView oldTileView = GetTileView(queuedMove.pos);
            oldTileView.OnArrow(false);
        }
        queuedMove = new QueuedMove(player, pawnView.pawn, pos);
        TileView tileView = GetTileView(queuedMove.pos);
        tileView.OnArrow(true);
        return true;
    }

    void ClearQueueMove()
    {
        if (queuedMove != null)
        {
            TileView tileView = GetTileView(queuedMove.pos);
            tileView.OnArrow(false);
        }
        queuedMove = null;
    }
    
    Pawn GetPawnFromPurgatoryByPawnDef(Player targetPlayer, PawnDef pawnDef, Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        foreach (var pawnView in pawnViews)
        {
            Pawn pawn = pawnView.pawn;
            if (pawn.player == targetPlayer)
            {
                if (pawn.def != null)
                {
                    if (pawn.def == pawnDef)
                    {
                        if (pawn.isSetup)
                        {
                            if (!pawn.isAlive)
                            {
                                pawn.SetAlive(true, pos);
                                PawnChanges pawnChanges = new()
                                {
                                    pawn = pawn,
                                    hasMovedChanged = false,
                                    isAliveChanged = true,
                                    isSetupChanged = false,
                                    isVisibleToOpponentChanged = false,
                                    posChanged = true,
                                };
                                OnPawnModified?.Invoke(pawnChanges);
                                return pawn;
                            }
                        }
                    }
                }
            }
            
            
        }
        Debug.Log($"can't find any pawns of pawnDef {pawnDef.name}");
        return null;
    }

    void SendPawnToPurgatory(Pawn pawn)
    {
        pawn.SetAlive(false, null);
        PawnChanges pawnChanges = new()
        {
            pawn = pawn,
            hasMovedChanged = false,
            isAliveChanged = true,
            isSetupChanged = false,
            isVisibleToOpponentChanged = false,
            posChanged = true,
        };
        OnPawnModified?.Invoke(pawnChanges);
    }

    void SelectPawnView(PawnView pawnView)
    {
        foreach (TileView highlightedTileView in highlightedTileViews)
        {
            highlightedTileView.OnHighlight(false);
        }
        foreach (PawnView highlightedPawnView in highlightedPawnViews)
        {
            highlightedPawnView.OnHighlight(false);
        }
        if (selectedPawnView)
        {
            selectedPawnView.SetSelect(false);
            if (pawnView == null)
            {
                selectedPawnView.SetSelect(false);
                selectedPawnView = null;
            }
        }
        selectedPawnView = pawnView;
        if (selectedPawnView)
        {
            highlightedTileViews.Clear();
            pawnView.SetSelect(true);
            SPawn selectedPawnState = GetPawnStateById(pawnView.pawn.pawnId);
            STile[] tiles = serverGameState.GetMovableTiles(selectedPawnState);
            foreach (STile tile in tiles)
            {
                TileView tileView = GetTileView(tile.pos.ToUnity());
                tileView.OnHighlight(true);
                highlightedTileViews.Add(tileView);
                PawnView pawnViewOnTile = GetPawnViewFromPos(tile.pos.ToUnity());
                if (pawnViewOnTile)
                {
                    highlightedPawnViews.Add(pawnViewOnTile);
                    pawnViewOnTile.OnHighlight(true);
                }
            }
        }
    }
    
    public PawnDef setupSelectedPawnDef;

    public void OnSetupPawnEntrySelected(PawnDef pawnDef)
    {
        setupSelectedPawnDef = pawnDef;
    }

    public bool IsSetupValid(Player targetPlayer)
{
    // Create a copy of the max pawns dict to track counts
    Dictionary<PawnDef, int> pawnCounts = new Dictionary<PawnDef, int>(setupParameters.maxPawnsDict);

    // Iterate over alive pawns on the board for the target player
    foreach (PawnView pawnView in pawnViews)
    {
        Pawn pawn = pawnView.pawn;
        if (pawn.isAlive && pawn.player == targetPlayer)
        {
            if (pawn.def == null)
            {
                return false;
            }
            // Check if pawnDef is in the max pawns dict
            if (!pawnCounts.ContainsKey(pawn.def))
            {
                Debug.LogError($"PawnDef '{pawn.def.pawnName}' not found in max pawns dict");
                return false;
            }

            // Decrement the count for this pawnDef
            pawnCounts[pawn.def] -= 1;

            // If count goes negative, there are too many pawns of this type
            if (pawnCounts[pawn.def] < 0)
            {
                Debug.LogError($"Too many pawns of type '{pawn.def.pawnName}'");
                return false;
            }

            // Check if the pawn is on a valid tile
            if (!IsPosValid(pawn.pos))
            {
                Debug.LogError($"Pawn '{pawn.def.pawnName}' is on an invalid position {pawn.pos}");
                return false;
            }

            TileView tileView = GetTileView(pawn.pos);
            if (tileView == null)
            {
                Debug.LogError($"Tile at position {pawn.pos} not found for pawn '{pawn.def.pawnName}'");
                return false;
            }
            if (!tileView.tile.IsTileEligibleForPlayer(targetPlayer))
            {
                Debug.LogError($"Tile at position {pawn.pos} is not eligible for player '{targetPlayer}'");
                return false;
            }
        }
    }

    // Check if there are any remaining pawns that haven't been placed
    foreach (var kvp in pawnCounts)
    {
        if (kvp.Value > 0)
        {
            Debug.LogError($"Not all pawns of type '{kvp.Key.pawnName}' have been placed. {kvp.Value} remaining.");
            return false;
        }
    }

    Debug.Log("Setup is valid");
    return true;
}
    
    void LoadPawnViews(Player targetPlayer, Dictionary<PawnDef, int> maxPawnsDict)
    {
        foreach ((PawnDef pawnDef, int max) in maxPawnsDict)
        {
            for (int i = 0; i < max; i++)
            {
                Pawn pawn = new(pawnDef, targetPlayer, true);
                GameObject pawnObject = Instantiate(pawnPrefab, transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnViews.Add(pawnView);
                pawnView.Initialize(pawn, null);
            }
        }
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
                Vector2Int pos = new(x, y);
                tileViews.Add(pos, tileView);
            }
        }
    }

    public void AutoSetup(Player targetPlayer)
    {
        if (targetPlayer == Player.NONE)
        {
            throw new Exception("Player can't be none");
        }
        // TODO: make this not call the same event 1600 times
        foreach (var pawnView in pawnViews)
        {
            if (pawnView.pawn.player == targetPlayer)
            {
                SendPawnToPurgatory(pawnView.pawn);
            }
        }
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
                GetPawnFromPurgatoryByPawnDef(targetPlayer, pawnDef, pos);
            }
        }
    }
    
    void ClearTiles()
    {
        ClearPawns();
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }
        tileViews.Clear();
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
    
    public PawnView GetPawnViewFromPos(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == pos);
    }
    
    public PawnView GetPawnViewFromPawn(Pawn pawn)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn == pawn);
    }

    public TileView GetTileView(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return tileViews.TryGetValue(pos, out TileView tileView) ? tileView : null;
    }

    int FindPawnCount(Player targetPlayer, PawnDef targetPawnDef)
    {
        return pawnViews.Where(pawnView => pawnView.pawn.def == targetPawnDef).Count(pawnView => pawnView.pawn.player == targetPlayer);
    }

    bool IsPosValid(Vector2Int pos)
    {
        return tileViews.Keys.Contains(pos);
    }

    public List<SPawn> GetSPawnListForSetup()
    {
        List<SPawn> sPawnList = new();
        foreach (var pawnView in pawnViews)
        {
            SPawn sPawn = new(pawnView.pawn);
            if (sPawn.isAlive && sPawn.player == (int)player)
            {
                sPawnList.Add(sPawn);
            }
        }
        return sPawnList;
    }

    Pawn GetPawnById(Guid id)
    {
        return pawnViews.Where(pawnView => pawnView.pawn.pawnId == id).Select(pawnView => pawnView.pawn).FirstOrDefault();
    }

    SPawn GetPawnStateById(Guid id)
    {
        return serverGameState.pawns.FirstOrDefault(pawn => pawn.pawnId == id);
    }

}
