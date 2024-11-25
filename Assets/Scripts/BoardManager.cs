using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
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
    public SSetupParameters serverSetupParameters;
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
    public SQueuedMove? maybeQueuedMove;
    HashSet<TileView> highlightedTileViews = new();
    HashSet<PawnView> highlightedPawnViews = new();

    
    // resolve stuff
    bool isBattleHappening;

    void SetPhase(GamePhase inPhase)
    {
        GamePhase oldPhase = phase;
        switch (oldPhase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                foreach (var tileView in tileViews.Values)
                {
                    if (tileView.tile.isPassable)
                    {
                        tileView.SetToGreen();
                    }
                }
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
                if (!maybeQueuedMove.HasValue)
                {
                    throw new Exception("Queuedmove cant be null in resolve phase");
                }
                else
                {
                    TileView queuedTileMove = GetTileView(maybeQueuedMove.Value.pos.ToUnity());
                    queuedTileMove.OnArrow(false);
                
                    foreach (var tileView in highlightedTileViews)
                    {
                        tileView.OnHighlight(false);
                    }
                    foreach (var pawnView in highlightedPawnViews)
                    {
                        pawnView.OnHighlight(false);
                    }
                    maybeQueuedMove = null;
                }
                
                break;
            case GamePhase.END:
                SelectPawnView(null);
                clickInputManager.Reset();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        OnPhaseChanged?.Invoke(oldPhase, phase);
    }
    
    public void OnDemoStartedResponse(Response<SSetupParameters> response)
    {
        serverSetupParameters = response.data;
        //setupParameters = response.data.ToUnity();
        Debug.Log("setup parameters set");
        player = (Player)serverSetupParameters.player;
        opponentPlayer = player == Player.RED ? Player.BLUE : Player.RED;
        Debug.Log("player set");
        Debug.Log("setupSelectedPawnDef set");
        LoadFromSetupParameters(serverSetupParameters);
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
        InitialUpdateState(initialGameState);
        SetPhase(GamePhase.MOVE);
    }

    public void SendQueuedMove()
    {
        if (maybeQueuedMove.HasValue)
        {
            GameManager.instance.client.SendMove(maybeQueuedMove.Value);
        }
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
    
    
    public void OnResolveResponse(Response<SResolveReceipt> response)
    {
        if (response.success)
        {
            // apply diff here
            SetPhase(GamePhase.RESOLVE);
            UpdateState(response.data);
        }
    }

    void InitialUpdateState(SGameState gameState)
    {
        SQueuedMove fakeRedMove = new()
        {
            player = (int)Player.RED,
            pawnId = Guid.Empty,
            initialPos = new SVector2Int(0, 0),
            pos = new SVector2Int(0, 0),
        };
        SQueuedMove fakeBlueMove = new()
        {
            player = (int)Player.BLUE,
            pawnId = Guid.Empty,
            initialPos = new SVector2Int(0, 0),
            pos = new SVector2Int(0, 0),
        };
        SConflictReceipt[] receipts = new SConflictReceipt[0];
        SResolveReceipt fakeReceipt = new()
        {
            player = gameState.player,
            redQueuedMove = fakeRedMove,
            blueQueuedMove = fakeBlueMove,
            gameState = gameState,
            receipts = receipts,
        };
        UpdateState(fakeReceipt);
    }
    void UpdateState(SResolveReceipt receipt)
    {
        
        HashSet<PawnChanges> pawnChangesSet = new();
        foreach (SPawn sPawn in receipt.gameState.pawns)
        {
            Pawn pawn = GetPawnById(sPawn.pawnId);
            if (pawn == null)
            {
                TileView tileView = GetTileView(sPawn.pos.ToUnity());
                Pawn newPawn = sPawn.ToUnity();
                GameObject pawnObject = Instantiate(pawnPrefab, transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnViews.Add(pawnView);
                pawnView.Initialize(newPawn, tileView);
            }
        }

        serverGameState = receipt.gameState;
        if (receipt.redQueuedMove.pawnId == Guid.Empty)
        {
            return;
        }

        StartCoroutine(ApplyResolve(receipt));

    }
    IEnumerator ApplyResolve(SResolveReceipt receipt)
    {
        Dictionary<PawnView, SQueuedMove> movingScouts = new();
        Dictionary<PawnView, SQueuedMove> movingPawns = new();
        PawnView redMovingPawn = GetPawnViewById(receipt.redQueuedMove.pawnId);
        PawnView blueMovingPawn = GetPawnViewById(receipt.blueQueuedMove.pawnId);
        if (redMovingPawn.pawn.def.pawnName == "Scout")
        {
            movingScouts.Add(redMovingPawn, receipt.redQueuedMove);
        }
        else
        {
            movingPawns.Add(redMovingPawn, receipt.redQueuedMove);
        }
        if (blueMovingPawn.pawn.def.pawnName == "Scout")
        {
            movingScouts.Add(blueMovingPawn, receipt.blueQueuedMove);
        }
        else
        {
            movingPawns.Add(blueMovingPawn, receipt.blueQueuedMove);
        }
        // TODO: differentiate between conflicts caused by scouts and conflicts where the scout is the defender
        HashSet<SConflictReceipt> scoutInvolvedConflicts = new();
        HashSet<SConflictReceipt> otherConflicts = new();
        foreach (var conflict in receipt.receipts)
        {
            var redPawn = receipt.gameState.GetPawnFromId(conflict.redPawnId);
            var bluePawn = receipt.gameState.GetPawnFromId(conflict.bluePawnId);
            if (redPawn.def.pawnName == "Scout" || bluePawn.def.pawnName == "Scout")
            {
                scoutInvolvedConflicts.Add(conflict);
            }
            else if (redPawn.def.pawnName != "Scout" && bluePawn.def.pawnName != "Scout")
            {
                otherConflicts.Add(conflict);
            }
            else
            {
                Debug.LogError($"Weird edge case detected red {redPawn.def.pawnName} at {redPawn.pos} blue {bluePawn.def.pawnName} at {bluePawn.pos}");
            }
        }
        Debug.Log("Start by moving scouts if they exist");
        yield return StartCoroutine(MovePawnGroup(movingScouts));
        foreach (var conflict in scoutInvolvedConflicts)
        {
            Debug.Log("Start pawn battle");
            SPawn redPawnState = receipt.gameState.GetPawnFromId(conflict.redPawnId);
            SPawn bluePawnState = receipt.gameState.GetPawnFromId(conflict.bluePawnId);
            PawnView redPawn = GetPawnViewById(conflict.redPawnId);
            PawnView bluePawn = GetPawnViewById(conflict.bluePawnId);
            redPawn.RevealPawn(redPawnState);
            bluePawn.RevealPawn(bluePawnState);
            yield return StartBattle(GetPawnViewById(conflict.redPawnId), GetPawnViewById(conflict.bluePawnId), conflict.redDies, conflict.blueDies);
            if (conflict.redDies)
            {
                if (!redPawnState.isAlive)
                {
                    redPawn.SendToGraveyard();
                }
                else
                {
                    throw new Exception("killed a pawn that isAlive is true");
                }
            }
            
            if (conflict.blueDies)
            {
                if (!bluePawnState.isAlive)
                {
                    bluePawn.SendToGraveyard();
                }
                else
                {
                    throw new Exception("killed a pawn that isAlive is true");
                }
            }
            Debug.Log("Pawn battle done");
        }
        Debug.Log("move pawns if they exist");
        yield return StartCoroutine(MovePawnGroup(movingPawns));
        foreach (var conflict in otherConflicts)
        {
            Debug.Log("Start battle");
            SPawn redPawnState = receipt.gameState.GetPawnFromId(conflict.redPawnId);
            SPawn bluePawnState = receipt.gameState.GetPawnFromId(conflict.bluePawnId);
            PawnView redPawn = GetPawnViewById(conflict.redPawnId);
            PawnView bluePawn = GetPawnViewById(conflict.bluePawnId);
            redPawn.RevealPawn(redPawnState);
            bluePawn.RevealPawn(bluePawnState);
            yield return StartBattle(GetPawnViewById(conflict.redPawnId), GetPawnViewById(conflict.bluePawnId), conflict.redDies, conflict.blueDies);
            if (conflict.redDies)
            {
                if (!redPawnState.isAlive)
                {
                    redPawn.SendToGraveyard();
                }
                else
                {
                    throw new Exception("killed a pawn that isAlive is true");
                }
            }
            
            if (conflict.blueDies)
            {
                if (!bluePawnState.isAlive)
                {
                    bluePawn.SendToGraveyard();
                }
                else
                {
                    throw new Exception("killed a pawn that isAlive is true");
                }
            }
            Debug.Log("Battle done");
        }
        foreach (var pawnView in pawnViews)
        {
            pawnView.UpdatePawn(receipt.gameState.GetPawnFromId(pawnView.pawn.pawnId));
        }
        
        SetPhase(GamePhase.MOVE);
        
    }

    IEnumerator StartBattle(PawnView redPawn, PawnView bluePawn, bool redDies, bool blueDies)
    {
        isBattleHappening = true;
        GameManager.instance.guiManager.gameOverlay.resolveScreen.Initialize(redPawn, bluePawn, redDies, blueDies, OnBattleFinished);
        yield return new WaitUntil(() => isBattleHappening == false);
    }

    void OnBattleFinished()
    {
        isBattleHappening = false;
        GameManager.instance.guiManager.gameOverlay.resolveScreen.Hide();
    }
    
    
    private IEnumerator MovePawnGroup(Dictionary<PawnView, SQueuedMove> group)
    {
        if (group.Count == 0)
        {
            Debug.Log("MovePawnGroup: nothing of this group exists");
            yield return null;
        }
        foreach (var kvp in group)
        {
            if (kvp.Key.pawn.isAlive)
            {
                kvp.Key.MoveView(kvp.Value.pos.ToUnity());
            }
            else
            {
                Debug.Log($"MovePawnGroup: not moving {kvp.Key.pawn.def.name} because it's dead");
            }
        }

        while (group.Any(kvp => kvp.Key.isMoving))
        {
            yield return null;
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
        if (maybeQueuedMove.HasValue)
        {
            TileView oldTileView = GetTileView(maybeQueuedMove.Value.pos.ToUnity());
            oldTileView.OnArrow(false);
        }
        maybeQueuedMove = new SQueuedMove((int)player, pawnView.pawn.pawnId, new SVector2Int(pawnView.pawn.pos),new SVector2Int(pos));
        TileView tileView = GetTileView(maybeQueuedMove.Value.pos.ToUnity());
        tileView.OnArrow(true);
        return true;
    }

    void ClearQueueMove()
    {
        if (maybeQueuedMove.HasValue)
        {
            TileView tileView = GetTileView(maybeQueuedMove.Value.pos.ToUnity());
            tileView.OnArrow(false);
        }
        maybeQueuedMove = null;
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

    void SendPawnToPurgatory(Pawn pawn, bool invoke = true)
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
        if (invoke)
        {
            OnPawnModified?.Invoke(pawnChanges);
        }
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

    public void SubmitSetup()
    {
        SPawn[] sPawns = new SPawn[pawnViews.Count];
        for (int i = 0; i < sPawns.Length; i++)
        {
            SPawn sPawn = new(pawnViews[i].pawn);
            sPawns[i] = sPawn;
        }

        if (SSetupParameters.IsSetupValid((int)player, serverSetupParameters, sPawns))
        {
            GameManager.instance.client.SendSetupSubmissionRequest(sPawns);
        }
    }

    void LoadFromSetupParameters(SSetupParameters setupParameters)
    {
        List<PawnView> tempPawnViews = new(pawnViews);
        foreach (PawnView pawnView in tempPawnViews)
        {
            pawnViews.Remove(pawnView);
            Destroy(pawnView.gameObject);
        }
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView.gameObject);
        }

        foreach (STile sTile in setupParameters.board.tiles)
        {
            Vector3Int gridPosition = new(sTile.pos.x, sTile.pos.y, 0);
            Vector3 worldPosition = grid.CellToWorld(gridPosition);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            Tile tile = sTile.ToUnity();
            tileView.Initialize(tile, this);
            tileViews.Add(tile.pos, tileView);
        }
        foreach (SSetupPawnData setupPawnData in serverSetupParameters.maxPawnsDict)
        {
            for (int i = 0; i < setupPawnData.maxPawns; i++)
            {
                Pawn pawn = new(setupPawnData.pawnDef.ToUnity(), (Player)setupParameters.player, true);
                GameObject pawnObject = Instantiate(pawnPrefab, transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnViews.Add(pawnView);
                pawnView.Initialize(pawn, null);
            }
        }
    }
    
    public void AutoSetup(Player targetPlayer)
    {
        foreach (var pawnView in pawnViews)
        {
            if (pawnView.pawn.player == targetPlayer)
            {
                SendPawnToPurgatory(pawnView.pawn, false);
            }
        }
        SPawn[] validSetup = SGameState.GenerateValidSetup((int)targetPlayer, serverSetupParameters);
        foreach (SPawn sPawn in validSetup)
        {
            PawnDef pawnDef = sPawn.def.ToUnity();
            GetPawnFromPurgatoryByPawnDef(targetPlayer, pawnDef, sPawn.pos.ToUnity());
        }
    }
    
    PawnView GetPawnViewFromPos(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == pos);
    }

    public TileView GetTileView(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return tileViews.TryGetValue(pos, out TileView tileView) ? tileView : null;
    }
    
    bool IsPosValid(Vector2Int pos)
    {
        return tileViews.Keys.Contains(pos);
    }

    Pawn GetPawnById(Guid id)
    {
        return pawnViews.Where(pawnView => pawnView.pawn.pawnId == id).Select(pawnView => pawnView.pawn).FirstOrDefault();
    }

    PawnView GetPawnViewById(Guid id)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pawnId == id);
    }
    SPawn GetPawnStateById(Guid id)
    {
        return serverGameState.pawns.FirstOrDefault(pawn => pawn.pawnId == id);
    }

}
