using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public ClickInputManager clickInputManager;
    // setup stuff
    
    public Player player;
    public Player opponentPlayer;
    public SSetupParameters serverSetupParameters;
    public Dictionary<Vector2Int, TileView> tileViews = new();
    public List<PawnView> pawnViews = new();
    public Vector2Int hoveredPos;
    //public event Action<PawnChanges> OnPawnModified; // subscribed to by all pawnviews
    //public event Action<GamePhase, GamePhase> OnPhaseChanged;
    
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

    Dictionary<GamePhase, IPhase> phases;
    public IPhase currentPhase;

    public event Action<IPhase> OnPhaseChanged;
    public event Action<PawnDef> OnSetupStateChanged;
    public void InvokeOnSetupStateChanged(PawnDef selectedPawnDef) {OnSetupStateChanged?.Invoke(selectedPawnDef);}
    
    
    void Awake()
    {
        phases = new()
        {
            { GamePhase.UNINITIALIZED, new UninitializedPhase(this) },
            { GamePhase.SETUP, null },
            { GamePhase.MOVE, null },
            { GamePhase.WAITING, null },
            { GamePhase.RESOLVE, null },
            { GamePhase.END, null },
        };
    }

    void SetPhaseNew(IPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
        OnPhaseChanged?.Invoke(currentPhase);
    }
    
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
                    TileView queuedTileMove = GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
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
        //OnPhaseChanged?.Invoke(oldPhase, phase);
    }
    
    #region Responses
    
    public void OnDemoStartedResponse(Response<SSetupParameters> response)
    {
        SetPhaseNew(new SetupPhase(this, response.data));
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
            SetPhaseNew(new WaitingPhase(this));
        }
        else
        {
            // TODO: handle this later
        }
    }

    public void OnSetupFinishedResponse(Response<SGameState> response)
    {
        
        SGameState initialGameState = response.data;
        SetPhaseNew(new MovePhase(this, initialGameState));
        // foreach (SPawn sPawn in initialGameState.pawns)
        // {
        //     Pawn pawn = GetPawnById(sPawn.pawnId);
        //     if (pawn == null)
        //     {
        //         TileView tileView = GetTileViewByPos(sPawn.pos.ToUnity());
        //         Pawn newPawn = sPawn.ToUnity();
        //         GameObject pawnObject = Instantiate(pawnPrefab, transform);
        //         PawnView pawnView = pawnObject.GetComponent<PawnView>();
        //         pawnViews.Add(pawnView);
        //         pawnView.Initialize(newPawn, tileView);
        //     }
        // }
        // foreach (PawnView pawnView in pawnViews)
        // {
        //     pawnView.SyncState(initialGameState.GetPawnById(pawnView.pawn.pawnId));
        // }
        // serverGameState = initialGameState;
        // SetPhase(GamePhase.MOVE);
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
        SetPhaseNew(new ResolvePhase(this, response.data));
        StartCoroutine(ApplyResolve(response.data));
        // if (response.success)
        // {
        //     
        //     // apply diff here
        //     
        //     
        //     SetPhase(GamePhase.RESOLVE);
        //     SResolveReceipt receipt = response.data;
        //     foreach (SPawn sPawn in receipt.gameState.pawns)
        //     {
        //         Pawn pawn = GetPawnById(sPawn.pawnId);
        //         if (pawn == null)
        //         {
        //             TileView tileView = GetTileViewByPos(sPawn.pos.ToUnity());
        //             Pawn newPawn = sPawn.ToUnity();
        //             GameObject pawnObject = Instantiate(pawnPrefab, transform);
        //             PawnView pawnView = pawnObject.GetComponent<PawnView>();
        //             pawnViews.Add(pawnView);
        //             pawnView.Initialize(newPawn, tileView);
        //         }
        //     }
        //     serverGameState = receipt.gameState;
        //     StartCoroutine(ApplyResolve(receipt));
        // }

    }
    // coroutines
        
    IEnumerator ApplyResolve(SResolveReceipt receipt)
    {
        foreach (SEventState resolveEvent in receipt.events)
        {
            yield return RunEvent(resolveEvent, receipt);
        }
        foreach (PawnView pawnView in pawnViews)
        {
            pawnView.SyncState(receipt.gameState.GetPawnById(pawnView.pawn.pawnId));
        }
        SetPhaseNew(new MovePhase(this)); 
        yield return null;
    }
    
    IEnumerator RunEvent(SEventState eventState, SResolveReceipt receipt)
    {
    ResolveEvent eventType = (ResolveEvent)eventState.eventType;
    string debugString = $"RunEvent: {eventState}";
    Debug.Log($"RunEvent {eventState}");
    PawnView pawnView = GetPawnViewById(eventState.pawnId);
    SPawn pawnState = receipt.gameState.GetPawnById(eventState.pawnId);
    switch (eventType)
    {
        case ResolveEvent.MOVE:
            Vector3 target = GetTileViewByPos(eventState.targetPos.ToUnity()).pawnOrigin.position;
            if (pawnView.transform.position != target)
            {
                yield return StartCoroutine(pawnView.ArcToPosition(target, Globals.PAWNMOVEDURATION, 0.5f));
            }
            break;
        case ResolveEvent.CONFLICT:
            PawnView defenderPawnView = GetPawnViewById(eventState.defenderPawnId);
            SPawn defenderPawnState = receipt.gameState.GetPawnById(eventState.defenderPawnId);
            pawnView.RevealPawn(pawnState);
            defenderPawnView.RevealPawn(defenderPawnState);
            Vector3 conflictTarget = GetTileViewByPos(eventState.targetPos.ToUnity()).pawnOrigin.position;
            yield return StartCoroutine(pawnView.ArcToPosition(conflictTarget, Globals.PAWNMOVEDURATION, 0.5f));
            SPawn redPawnState;
            SPawn bluePawnState;
            if (pawnState.player == (int)Player.RED)
            {
                redPawnState = pawnState;
                bluePawnState = defenderPawnState;
            }
            else
            {
                redPawnState = defenderPawnState;
                bluePawnState = pawnState;
            }
            debugString += $" {redPawnState} vs {bluePawnState}";
            Debug.Log(debugString);
            yield return StartBattle(redPawnState, bluePawnState);
            break;
        case ResolveEvent.SWAPCONFLICT:
            PawnView defenderSwapPawnView = GetPawnViewById(eventState.defenderPawnId);
            SPawn defenderSwapPawnState = receipt.gameState.GetPawnById(eventState.defenderPawnId);
            pawnView.RevealPawn(pawnState);
            defenderSwapPawnView.RevealPawn(defenderSwapPawnState);
            SPawn redPawnStateSwap;
            SPawn bluePawnStateSwap;
            if (pawnState.player == (int)Player.RED)
            {
                redPawnStateSwap = pawnState;
                bluePawnStateSwap = defenderSwapPawnState;
            }
            else
            {
                redPawnStateSwap = defenderSwapPawnState;
                bluePawnStateSwap = pawnState;
            }
            yield return StartBattle(redPawnStateSwap, bluePawnStateSwap);
            break;
        case ResolveEvent.DEATH:
            Vector3 purgatoryTarget = GameManager.instance.boardManager.purgatory.position;
            yield return StartCoroutine(pawnView.ArcToPosition(purgatoryTarget, Globals.PAWNMOVEDURATION, 2f));
            break;
        default:
            throw new ArgumentOutOfRangeException();
    }
    yield return null;
    }
    
    IEnumerator StartBattle(SPawn redPawn, SPawn bluePawn)
    {
        isBattleHappening = true;
        GameManager.instance.guiManager.gameOverlay.resolveScreen.Initialize(redPawn, bluePawn, !redPawn.isAlive, !bluePawn.isAlive, OnBattleFinished);
        yield return new WaitUntil(() => isBattleHappening == false);
    }
    
    void OnBattleFinished()
    {
        isBattleHappening = false;
        GameManager.instance.guiManager.gameOverlay.resolveScreen.Hide();
    }
    #endregion 
    #region Input
    
    void OnPositionHovered(Vector2Int oldPos, Vector2Int newPos)
    {
        // Store references to previous hovered pawn and tile
        PawnView previousHoveredPawnView = currentHoveredPawnView;
        TileView previousHoveredTileView = currentHoveredTileView;
        // Update current hovered pawn and tile based on new position
        if (IsPosValid(newPos))
        {
            currentHoveredPawnView = GetPawnViewByPos(newPos);
            currentHoveredTileView = GetTileViewByPos(newPos);
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
        
        currentPhase.OnHover(oldPos, newPos);
    }

    
    void OnClick(Vector2 screenPointerPosition, Vector2Int hoveredPosition)
    {
        currentPhase.OnClick(hoveredPosition);
        // switch (phase)
        // {
        //     case GamePhase.UNINITIALIZED:
        //         // Do nothing
        //         break;
        //
        //     case GamePhase.SETUP:
        //         if (!IsPosValid(hoveredPosition))
        //         {
        //             // Invalid position; do nothing
        //             break;
        //         }
        //
        //         if (currentHoveredPawnView != null)
        //         {
        //             // Remove the pawn at the hovered position
        //             SetupSendPawnToPurgatory(currentHoveredPawnView.pawn);
        //         }
        //         else if (setupSelectedPawnDef != null && GetPawnViewByPos(hoveredPosition) == null)
        //         {
        //             // Place a new pawn if a pawnDef is selected and no pawn is at this position
        //             SetupGetPawnFromPurgatoryByPawnDef(player, setupSelectedPawnDef, hoveredPosition);
        //         }
        //         // If no pawnDef is selected or a pawn is already at this position, do nothing
        //         break;
        //     case GamePhase.WAITING:
        //         // do nothing
        //         break;
        //     case GamePhase.MOVE:
        //         if (!IsPosValid(hoveredPosition))
        //         {
        //             if (selectedPawnView != null)
        //             {
        //                 SelectPawnView(null);
        //             }
        //             break;
        //         }
        //
        //         if (selectedPawnView != null)
        //         {
        //             if (currentHoveredPawnView != null && currentHoveredPawnView.pawn.player == player)
        //             {
        //                 if (currentHoveredPawnView == selectedPawnView)
        //                 {
        //                     SelectPawnView(null);
        //                     Debug.Log("OnClick: deselected because clicked the selected pawn");
        //                 }
        //                 else
        //                 {
        //                     SelectPawnView(currentHoveredPawnView);
        //                     Debug.Log("OnClick: selected a different pawn");
        //                 }
        //             }
        //             else
        //             {
        //                 bool success = TryQueueMove(selectedPawnView, hoveredPosition);
        //                 if (success)
        //                 {
        //                     Debug.Log("OnClick: tried to go to a tile");
        //                     SelectPawnView(null);
        //                 }
        //                 else
        //                 {
        //                     SelectPawnView(null);
        //                     Debug.Log("OnClick: deselected because couldn't go to tile");
        //                 }
        //             }
        //         }
        //         else
        //         {
        //             if (currentHoveredPawnView != null && currentHoveredPawnView.pawn.player == player)
        //             {
        //                 SelectPawnView(currentHoveredPawnView);
        //                 Debug.Log("OnClick: selecting pawn");
        //             }
        //             else
        //             {
        //                 Debug.Log("OnClick: doing nothing, clicked an empty tile with nothing selected");
        //             }
        //         }
        //         break;
        //     case GamePhase.RESOLVE:
        //         break;
        //     case GamePhase.END:
        //         break;
        //     default:
        //         throw new ArgumentOutOfRangeException();
        // }
    }
    
    
    
    void SelectPawnView(PawnView pawnView)
    {
        // foreach (TileView highlightedTileView in highlightedTileViews)
        // {
        //     highlightedTileView.OnHighlight(false);
        // }
        // foreach (PawnView highlightedPawnView in highlightedPawnViews)
        // {
        //     highlightedPawnView.OnHighlight(false);
        // }
        // if (selectedPawnView)
        // {
        //     selectedPawnView.SetSelect(false);
        //     if (pawnView == null)
        //     {
        //         selectedPawnView.SetSelect(false);
        //         selectedPawnView = null;
        //     }
        // }
        // selectedPawnView = pawnView;
        // if (selectedPawnView)
        // {
        //     highlightedTileViews.Clear();
        //     pawnView.SetSelect(true);
        //     SPawn selectedPawnState = GetPawnStateById(pawnView.pawn.pawnId);
        //     STile[] tiles = serverGameState.GetMovableTiles(selectedPawnState);
        //     foreach (STile tile in tiles)
        //     {
        //         TileView tileView = GetTileViewByPos(tile.pos.ToUnity());
        //         tileView.OnHighlight(true);
        //         highlightedTileViews.Add(tileView);
        //         PawnView pawnViewOnTile = GetPawnViewByPos(tile.pos.ToUnity());
        //         if (pawnViewOnTile)
        //         {
        //             highlightedPawnViews.Add(pawnViewOnTile);
        //             pawnViewOnTile.OnHighlight(true);
        //         }
        //     }
        // }
    }
    
    bool TryQueueMove(PawnView pawnView, Vector2Int pos)
    {
        // Debug.Log($"TryQueueMove at {pos}");
        // SPawn pawnOriginalState = GetPawnStateById(pawnView.pawn.pawnId);
        // STile[] movableTilesList = serverGameState.GetMovableTiles(pawnOriginalState);
        // // check if valid move
        // // queue a new PawnAction to go to that position
        // bool moveIsValid = movableTilesList.Any(tile => tile.pos.ToUnity() == pos);
        // if (!moveIsValid)
        // {
        //     return false;
        // }
        // if (maybeQueuedMove.HasValue)
        // {
        //     TileView oldTileView = GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
        //     oldTileView.OnArrow(false);
        // }
        // maybeQueuedMove = new SQueuedMove((int)player, pawnView.pawn.pawnId, new SVector2Int(pawnView.pawn.pos),new SVector2Int(pos));
        // TileView tileView = GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
        // tileView.OnArrow(true);
        return true;
    }
    
    void ClearQueueMove()
    {
        if (maybeQueuedMove.HasValue)
        {
            TileView tileView = GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
            tileView.OnArrow(false);
        }
        maybeQueuedMove = null;
    }
    
    #endregion
    #region Setup
    
    Pawn SetupGetPawnFromPurgatoryByPawnDef(Player targetPlayer, PawnDef pawnDef, Vector2Int pos)
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

    void SetupSendPawnToPurgatory(Pawn pawn, bool invoke = true)
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
    }
    
    void AutoSetup(Player targetPlayer)
    {
        foreach (var pawnView in pawnViews)
        {
            if (pawnView.pawn.player == targetPlayer)
            {
                SetupSendPawnToPurgatory(pawnView.pawn, false);
            }
        }
        SSetupPawn[] validSetup = SGameState.GenerateValidSetup((int)targetPlayer, serverSetupParameters);
        foreach (SSetupPawn setupPawn in validSetup)
        {
            PawnDef pawnDef = setupPawn.def.ToUnity();
            SetupGetPawnFromPurgatoryByPawnDef(targetPlayer, pawnDef, setupPawn.pos.ToUnity());
        }
    }

    
    public void OnSubmitSetup()
    {
        SSetupPawn[] setupPawns = new SSetupPawn[pawnViews.Count];
        for (int i = 0; i < setupPawns.Length; i++)
        {
            setupPawns[i] = new SSetupPawn(pawnViews[i].pawn);
        }

        if (SSetupParameters.IsSetupValid((int)player, serverSetupParameters, setupPawns))
        {
            GameManager.instance.client.SendSetupSubmissionRequest(setupPawns);
        }
    }
    
    #endregion
    
    
    public PawnView GetPawnViewByPos(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pos == pos);
    }

    public TileView GetTileViewByPos(Vector2Int pos)
    {
        if (!IsPosValid(pos))
        {
            throw new ArgumentOutOfRangeException($"Pos {pos} is invalid");
        }
        return tileViews.TryGetValue(pos, out TileView tileView) ? tileView : null;
    }
    
    public bool IsPosValid(Vector2Int pos)
    {
        return tileViews.Keys.Contains(pos);
    }

    Pawn GetPawnById(Guid id)
    {
        return pawnViews.Where(pawnView => pawnView.pawn.pawnId == id).Select(pawnView => pawnView.pawn).FirstOrDefault();
    }

    public PawnView GetPawnViewById(Guid id)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pawnId == id);
    }
    
    public void SendQueuedMove()
    {
        if (maybeQueuedMove.HasValue)
        {
            GameManager.instance.client.SendMove(maybeQueuedMove.Value);
        }
    }

    public void ClearPawnViews()
    {
        List<PawnView> tempPawnViews = new(pawnViews);
        foreach (PawnView pawnView in tempPawnViews)
        {
            Destroy(pawnView.gameObject);
        }
        pawnViews = new();
    }

    public void ClearTileViews()
    {
        foreach (TileView tileView in tileViews.Values)
        {
            Destroy(tileView);
        }
        tileViews = new();
    }
}

public enum GamePhase
{
    UNINITIALIZED,
    SETUP,
    WAITING,
    MOVE,
    RESOLVE,
    END
}

public interface IPhase
{
    public void EnterState();
    public void ExitState();

    public void OnHover(Vector2Int oldPos, Vector2Int newPos);

    public void OnClick(Vector2Int hoveredPosition);
}

public class UninitializedPhase : IPhase
{
    BoardManager boardManager;
    public UninitializedPhase(BoardManager inBoardManager)
    {
        boardManager = inBoardManager;
    }
    
    public void EnterState()
    {

    }

    public void ExitState()
    {

    }

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {

    }

    public void OnClick(Vector2Int hoveredPosition)
    {

    }
}

public class SetupPhase : IPhase
{
    BoardManager bm;
    public SSetupParameters setupParameters;
    public PawnDef selectedPawnDef;
    
    public SetupPhase(BoardManager inBoardManager, SSetupParameters inSetupParameters)
    {
        bm = inBoardManager;
        setupParameters = inSetupParameters;
    }
    
    public void EnterState()
    {
        Debug.Assert(setupParameters.player != (int)Player.NONE);
        Debug.Assert(bm.purgatory != null);
        List<PawnView> pawnViews = new();
        foreach (TileView tileView in bm.tileViews.Values)
        {
            UnityEngine.Object.Destroy(tileView);
        }
        
        Dictionary<Vector2Int, TileView> tileViews = new();
        foreach (STile sTile in setupParameters.board.tiles)
        {
            Vector3 worldPosition = bm.grid.CellToWorld(new Vector3Int(sTile.pos.x, sTile.pos.y, 0));
            GameObject tileObject = UnityEngine.Object.Instantiate(bm.tilePrefab, worldPosition, Quaternion.identity, bm.transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            Tile tile = sTile.ToUnity();
            tileView.Initialize(tile, bm);
            tileViews.Add(tile.pos, tileView);
        }
        foreach (SSetupPawnData setupPawnData in setupParameters.maxPawnsDict)
        {
            for (int i = 0; i < setupPawnData.maxPawns; i++)
            {
                Pawn pawn = new(setupPawnData.pawnDef.ToUnity(), (Player)setupParameters.player, true);
                GameObject pawnObject = UnityEngine.Object.Instantiate(bm.pawnPrefab, bm.purgatory.position, Quaternion.identity, bm.transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnView.Initialize(pawn, null);
                pawnViews.Add(pawnView);
            }
        }
        
        bm.ClearPawnViews();
        bm.ClearTileViews();
        bm.player = (Player)setupParameters.player;
        bm.opponentPlayer = bm.player == Player.RED ? Player.BLUE : Player.RED;
        bm.serverSetupParameters = setupParameters;
        bm.tileViews = tileViews;
        bm.pawnViews = pawnViews;
    }
    
    public void ExitState()
    {
        bm.ClearPawnViews();
    }

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {
        if (!bm.tileViews.Keys.Contains(bm.hoveredPos))
        {
            return;
        }
        else if (bm.currentHoveredPawnView != null)
        {
            PawnView deadPawnView = bm.currentHoveredPawnView;
            SPawn newState = new(deadPawnView.pawn)
            {
                isAlive = false,
                pos = new SVector2Int(Globals.PURGATORY),
            };
            deadPawnView.SyncState(newState);
            deadPawnView.UpdateViewPosition();
            bm.InvokeOnSetupStateChanged(selectedPawnDef);
        }
        else if (selectedPawnDef)
        {
            PawnView alivePawnView = GetPawnViewFromPurgatoryByPawnDef(bm.player, selectedPawnDef);
            if (!alivePawnView)
            {
                return;
            }
            SPawn newState = new(alivePawnView.pawn)
            {
                isAlive = true,
                pos = new SVector2Int(bm.hoveredPos),
            };
            alivePawnView.SyncState(newState);
            alivePawnView.UpdateViewPosition();
            bm.InvokeOnSetupStateChanged(selectedPawnDef);
        }
    }

    public void OnPawnDefSelected(PawnDef pawnDef)
    {
        selectedPawnDef = selectedPawnDef == pawnDef ? null : pawnDef;
        bm.InvokeOnSetupStateChanged(selectedPawnDef);
    }

    public void OnAutoSetup()
    {
        foreach (var pawnView in bm.pawnViews)
        {
            if (pawnView.pawn.player == (Player)setupParameters.player)
            {
                SPawn newState = new(pawnView.pawn)
                {
                    isAlive = false,
                    pos = new SVector2Int(Globals.PURGATORY),
                };
                pawnView.SyncState(newState);
            }
        }
        SSetupPawn[] validSetup = SGameState.GenerateValidSetup(setupParameters.player, setupParameters);
        foreach (SSetupPawn setupPawn in validSetup)
        {
            PawnView pawnView = GetPawnViewFromPurgatoryByPawnDef((Player)setupParameters.player, setupPawn.def.ToUnity());
            SPawn newState = new(pawnView.pawn)
            {
                isAlive = true,
                pos = setupPawn.pos,
            };
            pawnView.SyncState(newState);
            pawnView.UpdateViewPosition();
        }
        bm.InvokeOnSetupStateChanged(selectedPawnDef);
    }

    public void OnSubmitSetup()
    {
        SSetupPawn[] setupPawns = new SSetupPawn[bm.pawnViews.Count];
        for (int i = 0; i < setupPawns.Length; i++)
        {
            setupPawns[i] = new SSetupPawn(bm.pawnViews[i].pawn);
        }

        if (SSetupParameters.IsSetupValid((int)bm.player, setupParameters, setupPawns))
        {
            GameManager.instance.client.SendSetupSubmissionRequest(setupPawns);
        }
    }
    
    PawnView GetPawnViewFromPurgatoryByPawnDef(Player targetPlayer, PawnDef pawnDef)
    {
        foreach (PawnView pawnView in 
                 from pawnView in bm.pawnViews 
                 let pawn = pawnView.pawn 
                 where pawn.player == targetPlayer 
                 where pawn.def != null 
                 where pawn.def == pawnDef 
                 where pawn.isSetup 
                 where !pawn.isAlive 
                 select pawnView)
        {
            return pawnView;
        }
        Debug.Log($"can't find any pawns of pawnDef {pawnDef.name}");
        return null;
    }
}

public class WaitingPhase : IPhase
{
    BoardManager bm;
    public WaitingPhase(BoardManager inBoardManager)
    {
        bm = inBoardManager;
    }
    
    public void EnterState() {}

    public void ExitState() {}

    public void OnHover(Vector2Int oldPos, Vector2Int newPos) {}

    public void OnClick(Vector2Int hoveredPosition) {}
}

public class MovePhase : IPhase
{
    BoardManager bm;
    SGameState gameStateForHoldingOnly;
    public PawnView selectedPawnView;
    public SQueuedMove? maybeQueuedMove;
    HashSet<TileView> highlightedTileViews = new();
    HashSet<PawnView> highlightedPawnViews = new();
    bool moveSubmitted = false;
    bool isInitialMove = false;
    public MovePhase(BoardManager inBoardManager, SGameState inInitialGameState)
    {
        bm = inBoardManager;
        gameStateForHoldingOnly = inInitialGameState;
        isInitialMove = true;
    }

    public MovePhase(BoardManager inBoardManager)
    {
        bm = inBoardManager;
        isInitialMove = false;
    }
    
    public void EnterState()
    {
        if (isInitialMove)
        {
            Debug.Assert(bm.pawnViews.Count == 0);
            foreach (TileView tileView in bm.tileViews.Values)
            {
                tileView.SetToGreen();
            }
            List<PawnView> pawnViews = new();
            foreach (SPawn pawnState in gameStateForHoldingOnly.pawns)
            {
                TileView tileView = bm.GetTileViewByPos(pawnState.pos.ToUnity());
                Pawn newPawn = pawnState.ToUnity();
                GameObject pawnObject = UnityEngine.Object.Instantiate(bm.pawnPrefab, bm.transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnView.Initialize(newPawn, tileView);
                pawnViews.Add(pawnView);
            }
            bm.pawnViews = pawnViews;
            bm.serverGameState = gameStateForHoldingOnly;
        }
    }

    public void ExitState()
    {
        SelectPawnView(null);
        if (!maybeQueuedMove.HasValue)
        {
            throw new Exception("maybeQueuedMove cant be null when exiting resolve phase");
        }
        TileView queuedTileMove = bm.GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
        queuedTileMove.OnArrow(false);
        foreach (TileView tileView in highlightedTileViews)
        {
            tileView.OnHighlight(false);
        }
        foreach (PawnView pawnView in highlightedPawnViews)
        {
            pawnView.OnHighlight(false);
        }
    }

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {
        if (!bm.IsPosValid(hoveredPosition))
        {
            if (selectedPawnView != null)
            {
                SelectPawnView(null);
            }
        }
        else if (selectedPawnView != null)
        {
            if (bm.currentHoveredPawnView != null && bm.currentHoveredPawnView.pawn.player == bm.player)
            {
                if (bm.currentHoveredPawnView == selectedPawnView)
                {
                    SelectPawnView(null);
                    Debug.Log("OnClick: deselected because clicked the selected pawn");
                }
                else
                {
                    SelectPawnView(bm.currentHoveredPawnView);
                    Debug.Log("OnClick: selected a different pawn");
                }
            }
            else
            {
                bool success = TryQueueMove(selectedPawnView, hoveredPosition);
                SelectPawnView(null);
                if (success)
                {
                    Debug.Log("OnClick: queued a move");
                }
                else
                {
                    Debug.Log("OnClick: failed to queue a move");
                }
            }
        }
        else
        {
            if (bm.currentHoveredPawnView != null && bm.currentHoveredPawnView.pawn.player == bm.player)
            {
                SelectPawnView(bm.currentHoveredPawnView);
                Debug.Log("OnClick: selecting pawn");
            }
            else
            {
                Debug.Log("OnClick: doing nothing, clicked an empty tile with nothing selected");
            }
        }
    }

    public void OnSubmitMove()
    {
        if (maybeQueuedMove.HasValue)
        {
            moveSubmitted = true;
            GameManager.instance.client.SendMove(maybeQueuedMove.Value);
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
        }
        selectedPawnView = pawnView;
        if (selectedPawnView)
        {
            highlightedTileViews.Clear();
            pawnView.SetSelect(true);
            SPawn selectedPawnState = bm.serverGameState.GetPawnById(pawnView.pawn.pawnId);
            STile[] tiles = bm.serverGameState.GetMovableTiles(selectedPawnState);
            foreach (STile tile in tiles)
            {
                TileView tileView = bm.GetTileViewByPos(tile.pos.ToUnity());
                tileView.OnHighlight(true);
                highlightedTileViews.Add(tileView);
                PawnView pawnViewOnTile = bm.GetPawnViewByPos(tile.pos.ToUnity());
                if (pawnViewOnTile)
                {
                    highlightedPawnViews.Add(pawnViewOnTile);
                    pawnViewOnTile.OnHighlight(true);
                }
            }
        }
    }
    
    bool TryQueueMove(PawnView pawnView, Vector2Int pos)
    {
        Debug.Log($"TryQueueMove at {pos}");
        SPawn pawnOriginalState = bm.serverGameState.GetPawnById(pawnView.pawn.pawnId);
        STile[] movableTilesList = bm.serverGameState.GetMovableTiles(pawnOriginalState);
        // check if valid move
        // queue a new PawnAction to go to that position
        bool moveIsValid = movableTilesList.Any(tile => tile.pos.ToUnity() == pos);
        if (!moveIsValid)
        {
            return false;
        }
        if (maybeQueuedMove.HasValue)
        {
            TileView oldTileView = bm.GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
            oldTileView.OnArrow(false);
        }
        maybeQueuedMove = new SQueuedMove((int)bm.player, pawnView.pawn.pawnId, new SVector2Int(pawnView.pawn.pos),new SVector2Int(pos));
        TileView tileView = bm.GetTileViewByPos(maybeQueuedMove.Value.pos.ToUnity());
        tileView.OnArrow(true);
        return true;
    }
    
}

public class ResolvePhase : IPhase
{
    BoardManager bm;
    SResolveReceipt receipt;
    public ResolvePhase(BoardManager inBoardManager, SResolveReceipt inReceipt)
    {
        bm = inBoardManager;
        receipt = inReceipt;
    }
    
    public void EnterState()
    {
        bm.serverGameState = receipt.gameState;
        
    }

    public void ExitState()
    {

    }

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {

    }
}

public class EndPhase : IPhase
{
    BoardManager boardManager;
    
    public EndPhase(BoardManager inBoardManager)
    {
        boardManager = inBoardManager;
    }
    
    public void EnterState()
    {

    }

    public void ExitState()
    {

    }

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {

    }
}