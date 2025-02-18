using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using UnityEngine;
using UnityEngine.Serialization;

// BoardManager is responsible for managing the board state, including tiles and pawns.
// It handles the setup of the board and pawns for a given player.

public class BoardManager : MonoBehaviour
{
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public ClickInputManager clickInputManager;
    public Vortex vortex;
    
    public Team team;
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
    
    // game stuff
    public SGameState serverGameState;
    
    // resolve stuff
    bool isBattleHappening; // TODO: Move this into resolve phase later

    public IPhase currentPhase;

    public event Action<IPhase> OnPhaseChanged;
    public event Action<PawnDef> OnSetupStateChanged;
    
    public void InvokeOnSetupStateChanged(PawnDef selectedPawnDef) {OnSetupStateChanged?.Invoke(selectedPawnDef);}
    public SpotLight spotLightHandler;
    
    void Start()
    {
        clickInputManager.Initialize(this);
        SetPhase(new UninitializedPhase(this));
    }

    void Update()
    {
        currentPhase?.Update();
    }
    
    void SetPhase(IPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
        clickInputManager.ForceInvokeOnPositionHovered();
        OnPhaseChanged?.Invoke(currentPhase);
    }
    
    #region Responses

    public void OnGameStartTransitionFinished()
    {
        SetPhase(new SetupPhase(this, cachedResponse.data));
    }

    Response<SLobbyParameters> cachedResponse;
    public void OnDemoStartedResponse(Response<SLobbyParameters> response)
    {
        cachedResponse = response;
        clickInputManager.OnPositionHovered += OnPositionHovered;
        clickInputManager.OnClick += OnClick;
    }
    
    public void OnSetupSubmittedResponse(Response<bool> response)
    {
        SetPhase(new WaitingPhase(this));
    }

    public void OnSetupFinishedResponse(Response<SGameState> response)
    {
        SetPhase(new MovePhase(this, response.data));
    }
    
    public void OnMoveResponse(Response<bool> response)
    {
        SetPhase(new WaitingPhase(this));
    }
    
    public void OnResolveResponse(Response<SResolveReceipt> response)
    {
        SetPhase(new ResolvePhase(this, response.data));
        StartCoroutine(ApplyResolve(response.data));
    }
    
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
        if (receipt.gameState.winnerTeam != 0)
        {
            SetPhase(new EndPhase(this, receipt.gameState.winnerTeam));
        }
        else
        {
            SetPhase(new MovePhase(this)); 
        }
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
                TileView targetTileView = GetTileViewByPos(eventState.targetPos);
                spotLightHandler.LookAt(targetTileView.pawnOrigin);
                if (pawnView.transform.position != targetTileView.pawnOrigin.position)
                {
                    yield return StartCoroutine(pawnView.ArcToPosition(targetTileView.pawnOrigin, Globals.PawnMoveDuration, 0.25f));
                    BounceBoard();
                }
                break;
            case ResolveEvent.CONFLICT:
                PawnView defenderPawnView = GetPawnViewById(eventState.defenderPawnId);
                spotLightHandler.LookAt(defenderPawnView.transform);
                SPawn defenderPawnState = receipt.gameState.GetPawnById(eventState.defenderPawnId);
                pawnView.RevealPawn(pawnState);
                defenderPawnView.RevealPawn(defenderPawnState);
                TileView conflictTileView = GetTileViewByPos(eventState.targetPos);
                yield return StartCoroutine(pawnView.ArcToPosition(conflictTileView.pawnOrigin, Globals.PawnMoveDuration, 0.25f));
                SPawn redPawnState;
                SPawn bluePawnState;
                if (pawnState.team == (int)Team.RED)
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
                if (PlayerPrefs.GetInt("FASTMODE") == 0)
                {
                    yield return StartBattle(redPawnState, bluePawnState);
                }
                break;
            case ResolveEvent.SWAPCONFLICT:
                PawnView defenderSwapPawnView = GetPawnViewById(eventState.defenderPawnId);
                spotLightHandler.LookAt(defenderSwapPawnView.transform);
                SPawn defenderSwapPawnState = receipt.gameState.GetPawnById(eventState.defenderPawnId);
                pawnView.RevealPawn(pawnState);
                defenderSwapPawnView.RevealPawn(defenderSwapPawnState);
                SPawn redPawnStateSwap;
                SPawn bluePawnStateSwap;
                if (pawnState.team == (int)Team.RED)
                {
                    redPawnStateSwap = pawnState;
                    bluePawnStateSwap = defenderSwapPawnState;
                }
                else
                {
                    redPawnStateSwap = defenderSwapPawnState;
                    bluePawnStateSwap = pawnState;
                }
                if (PlayerPrefs.GetInt("FASTMODE") == 0)
                {
                    yield return StartBattle(redPawnStateSwap, bluePawnStateSwap);
                }
                break;
            case ResolveEvent.DEATH:
                spotLightHandler.LookAt(null);
                pawnView.shatterEffect.ShatterEffect();
                // TODO: see if this is needful
                pawnView.LockMovementToTransform(purgatory);
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
    }
    
    #endregion 
    #region Input

    public void ClearOutlineEffects()
    {
        if (currentHoveredPawnView)
        {
            currentHoveredPawnView.OnHovered(false);
            currentHoveredPawnView = null;
        }
        if (currentHoveredTileView)
        {
            currentHoveredTileView.OnHovered(false);
            currentHoveredTileView = null;
        }
    }
    
    void OnPositionHovered(Vector2Int oldPos, Vector2Int newPos)
    {
        // Store references to previous hovered pawn and tile
        previousHoveredPawnView = currentHoveredPawnView;
        previousHoveredTileView = currentHoveredTileView;
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
        currentPhase.OnHover(
            newPos, 
            oldPos, 
            currentHoveredTileView, 
            previousHoveredTileView, 
            currentHoveredPawnView, 
            previousHoveredPawnView);
    }
    
    void OnClick(Vector2 screenPointerPosition, Vector2Int hoveredPosition)
    {
        currentPhase.OnClick(hoveredPosition, currentHoveredTileView, currentHoveredPawnView);
    }

    #endregion

    Sequence bounceSequence;
    Vector3 basePosition = new Vector3(-4.5f, 0, -4.5f);
    public TweenSettings<float> bounceDownSettings;
    public TweenSettings<float> bounceUpSettings;
    public ShakeSettings punchSettings;
    
    public void BounceBoard()
    {
        bounceSequence = Sequence.Create()
            .Chain(Tween.PunchLocalPosition(transform, punchSettings));
            // .Chain(Tween.PositionY(transform, bounceDownSettings))
            // .Chain(Tween.PositionY(transform, bounceUpSettings));
    }
    
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
        return tileViews.TryGetValue(pos, out TileView tileView) ? tileView : null;
    }
    
    public bool IsPosValid(Vector2Int pos)
    {
        return tileViews.Keys.Contains(pos);
    }
    
    public PawnView GetPawnViewById(Guid id)
    {
        return pawnViews.FirstOrDefault(pawnView => pawnView.pawn.pawnId == id);
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
    public void Update();
    public void OnHover(
        Vector2Int newPos, 
        Vector2Int oldPos, 
        TileView currentHoveredTileView, 
        TileView previousHoveredTileView, 
        PawnView currentHoveredPawnView, 
        PawnView previousHoveredPawnView);

    public void OnClick(
        Vector2Int hoveredPosition, 
        TileView currentHoveredTileView, 
        PawnView currentHoveredPawnView);

}

public class UninitializedPhase : IPhase
{
    BoardManager bm;
    public UninitializedPhase(BoardManager inBoardManager)
    {
        bm = inBoardManager;
    }
    
    public void EnterState() {}

    public void ExitState() {}

    public void Update() {}
    

    public void OnHover(Vector2Int newPos, Vector2Int oldPos, TileView currentHoveredTileView, TileView previousHoveredTileView,
        PawnView currentHoveredPawnView, PawnView previousHoveredPawnView) {}

    public void OnClick(Vector2Int hoveredPosition, TileView currentHoveredTileView, PawnView currentHoveredPawnView) {}
}

public class SetupPhase : IPhase
{
    BoardManager bm;
    public SLobbyParameters lobbyParameters;
    public PawnDef selectedPawnDef;
    
    public SetupPhase(BoardManager inBoardManager, SLobbyParameters inLobbyParameters)
    {
        bm = inBoardManager;
        lobbyParameters = inLobbyParameters;
    }
    
    public void EnterState()
    {
        Debug.Assert(lobbyParameters.hostTeam != (int)Team.NONE);
        Debug.Assert(bm.purgatory != null);
        List<PawnView> pawnViews = new();
        bm.grid.SetBoard(lobbyParameters.board);
        foreach (TileView tileView in bm.tileViews.Values)
        {
            UnityEngine.Object.Destroy(tileView);
        }
        
        Dictionary<Vector2Int, TileView> tileViews = new();
        foreach (STile sTile in lobbyParameters.board.tiles)
        {
            Vector3 worldPosition = bm.grid.CellToWorld(sTile.pos);
            GameObject tileObject = UnityEngine.Object.Instantiate(bm.tilePrefab, worldPosition, Quaternion.identity, bm.transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(bm, sTile, lobbyParameters.board.isHex);
            tileViews.Add(sTile.pos, tileView);
        }

        foreach (SMaxPawnsPerRank maxPawns in lobbyParameters.maxPawns)
        {
            for (int i = 0; i < maxPawns.max; i++)
            {
                PawnDef pawnDef = GameManager.instance.orderedPawnDefList.FirstOrDefault(def => def.rank == maxPawns.rank);
                Pawn pawn = new(pawnDef, (Team)lobbyParameters.hostTeam, true);
                GameObject pawnObject = UnityEngine.Object.Instantiate(bm.pawnPrefab, bm.purgatory.position, Quaternion.identity, bm.transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                pawnView.Initialize(pawn, null);
                pawnViews.Add(pawnView);
            }
        }
        
        bm.ClearPawnViews();
        bm.ClearTileViews();
        bm.team = (Team)lobbyParameters.hostTeam;
        bm.tileViews = tileViews;
        bm.pawnViews = pawnViews;

        List<Vector3> waveOrigins = new List<Vector3>()
        {
            bm.waveStartPositionOne.position,
            bm.waveStartPositionTwo.position,
        };
        TileIntroAnimation(waveOrigins, bm.waveSpeed);
    }
    
    public void ExitState()
    {
        bm.ClearOutlineEffects();
        bm.ClearPawnViews();
    }

    public void Update() {}
    
    public void OnHover(Vector2Int newPos, Vector2Int oldPos, TileView currentHoveredTileView, TileView previousHoveredTileView,
        PawnView currentHoveredPawnView, PawnView previousHoveredPawnView)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition, TileView currentHoveredTileView, PawnView currentHoveredPawnView)
    {
        if (bm.clickInputManager.isOverUI)
        {
            // do nothing
        }
        else if (!bm.tileViews.Keys.Contains(bm.hoveredPos))
        {
            // do nothing
        }
        else if (!lobbyParameters.board.GetTileByPos(hoveredPosition).IsTileSetupAllowed((int)bm.team))
        {
            // do nothing
        }
        else if (bm.currentHoveredPawnView != null)
        {
            PawnView deadPawnView = bm.currentHoveredPawnView;
            SPawn newState = new(deadPawnView.pawn)
            {
                isAlive = false,
                pos = Globals.Purgatory,
            };
            deadPawnView.SyncState(newState);
            deadPawnView.LockMovementToTransform(bm.purgatory);
            bm.InvokeOnSetupStateChanged(selectedPawnDef);
        }
        else if (selectedPawnDef)
        {
            PawnView alivePawnView = GetPawnViewFromPurgatoryByPawnDef(bm.team, selectedPawnDef);
            if (!alivePawnView)
            {
                return;
            }
            SPawn newState = new(alivePawnView.pawn)
            {
                isAlive = true,
                pos = bm.hoveredPos,
            };
            alivePawnView.SyncState(newState);
            alivePawnView.LockMovementToTransform(currentHoveredTileView.pawnOrigin);
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
            if (pawnView.pawn.team == (Team)lobbyParameters.hostTeam)
            {
                SPawn newState = new(pawnView.pawn)
                {
                    isAlive = false,
                    pos = Globals.Purgatory,
                };
                pawnView.SyncState(newState);
            }
        }
        SSetupPawn[] validSetup = SGameState.GenerateValidSetup(lobbyParameters.hostTeam, lobbyParameters);
        foreach (SSetupPawn setupPawn in validSetup)
        {
            PawnView pawnView = GetPawnViewFromPurgatoryByPawnDef((Team)lobbyParameters.hostTeam, setupPawn.def.ToUnity());
            SPawn newState = new(pawnView.pawn)
            {
                isAlive = true,
                pos = setupPawn.pos,
            };
            pawnView.SyncState(newState);
            pawnView.LockMovementToTransform(bm.GetTileViewByPos(pawnView.pawn.pos).pawnOrigin);
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

        if (Rules.IsSetupValid((int)bm.team, lobbyParameters, setupPawns))
        {
            GameManager.instance.client.SendSetupSubmissionRequest(setupPawns);
        }
    }
    
    PawnView GetPawnViewFromPurgatoryByPawnDef(Team targetTeam, PawnDef pawnDef)
    {
        foreach (PawnView pawnView in 
                 from pawnView in bm.pawnViews 
                 let pawn = pawnView.pawn 
                 where pawn.team == targetTeam 
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

    void TileIntroAnimation(List<Vector3> startingPositions, float waveSpeed)
    {
        float minOfMinDistances = Mathf.Infinity;
        Dictionary<Vector2Int, float> minDistances = new();
        foreach (TileView tileView in bm.tileViews.Values)
        {
            
            float minDistance = Mathf.Infinity;
            foreach (Vector3 startingPosition in startingPositions)
            {
                float distance = Vector3.Distance(tileView.pawnOrigin.position, startingPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
                if (minDistance < minOfMinDistances)
                {
                    minOfMinDistances = minDistance;
                }
            }
            minDistances[tileView.tile.pos] = minDistance;
        }
        foreach ((Vector2Int pos, float minDistance) in minDistances)
        {
            float delay = (minDistance - minOfMinDistances) / waveSpeed;
            bm.tileViews[pos].FallingAnimation(delay);
        }
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

    public void Update() {}
    public void OnHover(Vector2Int newPos, Vector2Int oldPos, TileView currentHoveredTileView, TileView previousHoveredTileView,
        PawnView currentHoveredPawnView, PawnView previousHoveredPawnView) {}

    public void OnClick(Vector2Int hoveredPosition, TileView currentHoveredTileView, PawnView currentHoveredPawnView) {}
}

public class MovePhase : IPhase
{
    BoardManager bm;
    SGameState gameStateForHoldingOnly;
    
    PawnView selectedPawnView;
    TileView selectedTileView;
    SQueuedMove? maybeQueuedMove;
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
            List<PawnView> pawnViews = new();
            foreach (SPawn pawnState in gameStateForHoldingOnly.pawns)
            {
                
                Pawn newPawn = pawnState.ToUnity();
                GameObject pawnObject = UnityEngine.Object.Instantiate(bm.pawnPrefab, bm.transform);
                PawnView pawnView = pawnObject.GetComponent<PawnView>();
                TileView tileView = bm.GetTileViewByPos(pawnState.pos);
                pawnView.Initialize(newPawn, tileView);
                pawnViews.Add(pawnView);
            }
            bm.pawnViews = pawnViews;
            bm.serverGameState = gameStateForHoldingOnly;
        }
        else
        {
            bm.vortex.EndVortex();
        }
    }

    public void ExitState()
    {
        Debug.Assert(moveSubmitted);
        //bm.ClearOutlineEffects();
        ClearSelection();
        if (!maybeQueuedMove.HasValue)
        {
            throw new Exception("maybeQueuedMove cant be null when exiting resolve phase");
        }
        TileView queuedTileMove = bm.GetTileViewByPos(maybeQueuedMove.Value.pos);
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

    public void Update() {}

    public void OnHover(Vector2Int newPos, Vector2Int oldPos, TileView currentHoveredTileView, TileView previousHoveredTileView,
        PawnView currentHoveredPawnView, PawnView previousHoveredPawnView)
    {
        if (previousHoveredTileView && previousHoveredTileView != selectedTileView && IsPawnViewSelectable(previousHoveredPawnView))
        {
            //Debug.LogWarning($"Elevate caused by hover exit to {previousHoveredTileView.tile.pos}");
            ElevatePos(oldPos, 0);
        }
        if (currentHoveredTileView && currentHoveredTileView != selectedTileView && IsPawnViewSelectable(currentHoveredPawnView))
        {
            //Debug.LogWarning($"Elevate caused by hover to {currentHoveredTileView.tile.pos}");
            ElevatePos(newPos, Globals.HoveredHeight);
        }
    }

    public void OnClick(Vector2Int hoveredPosition, TileView currentHoveredTileView, PawnView currentHoveredPawnView)
    {
        // selection logic
        if (!bm.IsPosValid(hoveredPosition))
        {
            ClearSelection();
            return;
        }
        if (selectedPawnView)
        {
            // if move is valid
            if (highlightedTileViews.Contains(currentHoveredTileView))
            {
                // NOTE: queue move and THEN clear selection
                QueueMove(selectedPawnView, hoveredPosition);
                if (PlayerPrefs.GetInt("FASTMODE") == 1)
                {
                    OnSubmitMove();
                    return;
                }
            }
        }
        ClearSelection();
        // if is selectable
        if (IsPawnViewSelectable(currentHoveredPawnView))
        {
            SelectPawnViewAndTileView(currentHoveredPawnView, currentHoveredTileView);
            ElevatePos(hoveredPosition, Globals.SelectedHoveredHeight);
            maybeQueuedMove = null;
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
    
    void ClearSelection()
    {
        if (selectedPawnView)
        {
            selectedPawnView.OnSelect(false);
        }
        if (selectedTileView)
        {
            selectedTileView.OnSelect(false);
            selectedTileView.Elevate(0);
        }
        selectedPawnView = null;
        selectedTileView = null;
        // clear highlights
        foreach (TileView highlightedTileView in highlightedTileViews)
        {
            highlightedTileView.OnHighlight(false);
        }
        highlightedTileViews.Clear();
        foreach (PawnView highlightedPawnView in highlightedPawnViews)
        {
            highlightedPawnView.OnHighlight(false);
        }
        highlightedPawnViews.Clear();
    }

    void ElevatePos(Vector2Int pos, float height)
    {
        if (!bm.IsPosValid(pos))
        {
            return;
        }
        PawnView pawnView = bm.GetPawnViewByPos(pos);
        TileView tileView = bm.GetTileViewByPos(pos);
        tileView.Elevate(height);
    }
    
    bool IsPawnViewSelectable(PawnView pawnView)
    {
        if (!pawnView)
        {
            return false;
        }
        if (pawnView.pawn.team != bm.team)
        {
            return false;
        }
        return true;
    }
    
    void SelectPawnViewAndTileView(PawnView pawnView, TileView tileView)
    {
        Debug.Log($"SelectPawnViewAndTileView {pawnView}");
        Debug.Assert(pawnView.pawn.pos == tileView.tile.pos);
        selectedPawnView = pawnView;
        selectedTileView = tileView;
        if (selectedPawnView)
        {
            selectedPawnView.OnSelect(true);
            // reapply highlights
            SPawn selectedPawnState = bm.serverGameState.GetPawnById(selectedPawnView.pawn.pawnId);
            STile[] tiles = bm.serverGameState.GetMovableTiles(selectedPawnState);
            foreach (STile movableTile in tiles)
            {
                TileView movableTileView = bm.GetTileViewByPos(movableTile.pos);
                movableTileView.OnHighlight(true);
                highlightedTileViews.Add(movableTileView);
                PawnView pawnViewOnMovableTile = bm.GetPawnViewByPos(movableTile.pos);
                if (pawnViewOnMovableTile)
                {
                    highlightedPawnViews.Add(pawnViewOnMovableTile);
                    pawnViewOnMovableTile.OnHighlight(true);
                }
            }
        }
        if (selectedTileView)
        {
            selectedTileView.OnSelect(true);
        }
        Debug.Assert(selectedPawnView);
        Debug.Assert(selectedTileView);
    }
    
    void QueueMove(PawnView pawnView, Vector2Int pos)
    {
        Debug.Log($"TryQueueMove at {pos}");
        // check if valid move
        // queue a new PawnAction to go to that position
        if (maybeQueuedMove.HasValue)
        {
            TileView oldTileView = bm.GetTileViewByPos(maybeQueuedMove.Value.pos);
            oldTileView.OnArrow(false);
        }
        maybeQueuedMove = new SQueuedMove((int)bm.team, pawnView.pawn.pawnId,  pawnView.pawn.pos,pos);
        TileView tileView = bm.GetTileViewByPos(maybeQueuedMove.Value.pos);
        tileView.OnArrow(true);
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
        bm.vortex.StartVortex();
    }

    public void ExitState()
    {

    }

    public void Update() {}
    
    public void OnHover(Vector2Int newPos, Vector2Int oldPos, TileView currentHoveredTileView, TileView previousHoveredTileView,
        PawnView currentHoveredPawnView, PawnView previousHoveredPawnView)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition, TileView currentHoveredTileView, PawnView currentHoveredPawnView)
    {

    }
}

public class EndPhase : IPhase
{
    BoardManager bm;
    public int winner;
    
    public EndPhase(BoardManager inBoardManager, int inWinner)
    {
        bm = inBoardManager;
        winner = inWinner;
    }
    
    public void EnterState()
    {

    }

    public void ExitState()
    {

    }

    public void Update() {}

    public void OnHover(Vector2Int newPos, Vector2Int oldPos, TileView currentHoveredTileView, TileView previousHoveredTileView,
        PawnView currentHoveredPawnView, PawnView previousHoveredPawnView)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition, TileView currentHoveredTileView, PawnView currentHoveredPawnView)
    {

    }
}