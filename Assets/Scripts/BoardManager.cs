using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using UnityEngine;

// BoardManager is responsible for managing the board state, including tiles and pawns.
// It handles the setup of the board and pawns for a given player.

public class BoardManager : MonoBehaviour
{
    public Transform purgatory;
    public GameObject tilePrefab;
    public GameObject pawnPrefab;
    public BoardGrid grid;
    public ClickInputManager clickInputManager;
    
    public Player player;
    public Player opponentPlayer;
    public Dictionary<Vector2Int, TileView> tileViews = new();
    public List<PawnView> pawnViews = new();
    
    public Vector2Int hoveredPos;
    public PawnView currentHoveredPawnView;
    public TileView currentHoveredTileView;

    public Transform waveStartPositionOne;
    public Transform waveStartPositionTwo;
    public float waveSpeed;
    
    // game stuff
    public SGameState serverGameState;
    
    // resolve stuff
    bool isBattleHappening; // Move this into resolve phase later

    public IPhase currentPhase;
    public Renderer floorRenderer;

    public event Action<IPhase> OnPhaseChanged;
    public event Action<PawnDef> OnSetupStateChanged;
    public void InvokeOnSetupStateChanged(PawnDef selectedPawnDef) {OnSetupStateChanged?.Invoke(selectedPawnDef);}
    
    static readonly int timeScaleID = Shader.PropertyToID("_TimeScale");
    static readonly int breatheFloorID = Shader.PropertyToID("_BreatheFloor");
    static readonly int breatheTimeID = Shader.PropertyToID("_BreatheTime");
    static readonly int breathePowerID = Shader.PropertyToID("_BreathePower");
    static readonly int twistednessID = Shader.PropertyToID("_Twistedness");

    public float vortexTransitionDuration = 0.25f;
    public Light directionalLight;
    public Light spotLight;
    public SpotLight spotLightHandler;
    
    void Start()
    {
        clickInputManager.Initialize(this);
        SetPhase(new UninitializedPhase(this));
    }
    
    void SetPhase(IPhase newPhase)
    {
        currentPhase?.ExitState();
        currentPhase = newPhase;
        currentPhase.EnterState();
        OnPhaseChanged?.Invoke(currentPhase);
    }

    bool isVortexOn;
    Coroutine currentVortexLerp;
    public void StartVortex()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        floorRenderer.GetPropertyBlock(block);
        float currentTimeScale = block.GetFloat(timeScaleID);
        Debug.Log($"StartVortex started timescale at {currentTimeScale}");
        isVortexOn = true;
        if (currentVortexLerp != null)
        {
            StopCoroutine(currentVortexLerp);
        }
        currentVortexLerp = StartCoroutine(LerpVortex(true));
    }

    public void EndVortex()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        floorRenderer.GetPropertyBlock(block);
        float currentTimeScale = block.GetFloat(timeScaleID);
        Debug.Log($"EndVortex started timescale at {currentTimeScale}");
        isVortexOn = false;
        Debug.Log("EndVortex");
        if (currentVortexLerp != null)
        {
            StopCoroutine(currentVortexLerp);
        }
        currentVortexLerp = StartCoroutine(LerpVortex(false));
    }
    
    float EaseInOutQuad(float t) {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
    
    IEnumerator LerpVortex(bool isOn)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        floorRenderer.GetPropertyBlock(block);
        float initTwistedness = block.GetFloat(twistednessID);
        float initTimeScale = block.GetFloat(timeScaleID);
        float initBreatheFloor = block.GetFloat(breatheFloorID);
        float initBreatheTime = block.GetFloat(breatheTimeID);
        float initDirectionalLightIntensity = directionalLight.intensity;
        float initSpotLightIntensity = spotLight.intensity;
        //float initBreathePower = block.GetFloat(breathePowerID);
        float targetTwistedness = isOn ? 0f : 1f;
        float targetTimeScale = isOn? -1f : -0.1f;
        float targetDirectionalLightIntensity = isOn ? 1f : 2.5f;
        float targetSpotLightIntensity = isOn ? 40f : 0f;
       // float targetBreatheFloor = isOn? 4f : 1.88f;
        //float targetBreatheTime = isOn ? 5.3f : 2f;
        //float targetBreathePower = isOn ? 200f : 0.12f;
        
        float elapsedTime = 0f;
        while (elapsedTime < vortexTransitionDuration)
        {
            float t = elapsedTime / vortexTransitionDuration;
            float easedT = EaseInOutQuad(t);
            elapsedTime += Time.deltaTime;
            float currentTwistedness = Mathf.Lerp(initTwistedness, targetTwistedness, easedT);
            float currentTimeScale = Mathf.Lerp(initTimeScale, targetTimeScale, easedT);
            float currentDirectionalLightIntensity = Mathf.Lerp(initDirectionalLightIntensity, targetDirectionalLightIntensity, easedT);
            float currentSpotLightIntensity = Mathf.Lerp(initSpotLightIntensity, targetSpotLightIntensity, easedT);
            //float currentBreatheFloor = Mathf.Lerp(initBreatheFloor, targetBreatheFloor, delta);
            //float currentBreatheTime = Mathf.Lerp(initBreatheTime, targetBreatheTime, delta);
            //float currentBreathePower = Mathf.Lerp(initBreathePower, targetBreathePower, delta);
            block.SetFloat(timeScaleID, currentTimeScale);
            block.SetFloat(twistednessID, currentTwistedness);
            directionalLight.intensity = currentDirectionalLightIntensity;
            spotLight.intensity = currentSpotLightIntensity;
            //currentBlock.SetFloat(breatheFloorID, currentBreatheFloor);
            //currentBlock.SetFloat(breatheTimeID, currentBreatheTime);
            //block.SetFloat(breathePowerID, currentBreathePower);
            floorRenderer.SetPropertyBlock(block);
            yield return null;
        }
        //block.SetFloat(breatheFloorID, targetBreatheFloor);
        //block.SetFloat(breatheTimeID, targetBreatheTime);
        //block.SetFloat(breathePowerID, targetBreathePower);
        
    }
    
    #region Responses

    public void OnGameStartTransitionFinished()
    {
        SetPhase(new SetupPhase(this, cachedResponse.data));
    }

    Response<SSetupParameters> cachedResponse;
    public void OnDemoStartedResponse(Response<SSetupParameters> response)
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
        if (receipt.gameState.winnerPlayer != 0)
        {
            SetPhase(new EndPhase(this, receipt.gameState.winnerPlayer));
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
                Vector3 target = GetTileViewByPos(eventState.targetPos).pawnOrigin.position;
                spotLightHandler.LookAt(pawnView.transform);
                if (pawnView.transform.position != target)
                {
                    yield return StartCoroutine(pawnView.ArcToPosition(target, Globals.PAWNMOVEDURATION, 0.25f));
                }
                break;
            case ResolveEvent.CONFLICT:
                PawnView defenderPawnView = GetPawnViewById(eventState.defenderPawnId);
                spotLightHandler.LookAt(defenderPawnView.transform);
                SPawn defenderPawnState = receipt.gameState.GetPawnById(eventState.defenderPawnId);
                pawnView.RevealPawn(pawnState);
                defenderPawnView.RevealPawn(defenderPawnState);
                Vector3 conflictTarget = GetTileViewByPos(eventState.targetPos).pawnOrigin.position;
                yield return StartCoroutine(pawnView.ArcToPosition(conflictTarget, Globals.PAWNMOVEDURATION, 0.25f));
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
                if (PlayerPrefs.GetInt("FASTMODE") == 0)
                {
                    yield return StartBattle(redPawnStateSwap, bluePawnStateSwap);
                }
                break;
            case ResolveEvent.DEATH:
                Vector3 purgatoryTarget = GameManager.instance.boardManager.purgatory.position;
                spotLightHandler.LookAt(null);
                pawnView.billboard.GetComponent<Shatter>().ShatterEffect();
                pawnView.transform.position = purgatoryTarget;
                //yield return StartCoroutine(pawnView.ArcToPosition(purgatoryTarget, Globals.PAWNMOVEDURATION, 2f));
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

    public void OnHover(Vector2Int oldPos, Vector2Int newPos);

    public void OnClick(Vector2Int hoveredPosition);
}

public class UninitializedPhase : IPhase
{
    BoardManager bm;
    public UninitializedPhase(BoardManager inBoardManager)
    {
        bm = inBoardManager;
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
        bm.grid.SetBoard(setupParameters.board);
        foreach (TileView tileView in bm.tileViews.Values)
        {
            UnityEngine.Object.Destroy(tileView);
        }
        
        Dictionary<Vector2Int, TileView> tileViews = new();
        foreach (STile sTile in setupParameters.board.tiles)
        {
            Vector3 worldPosition = bm.grid.CellToWorld(sTile.pos);
            GameObject tileObject = UnityEngine.Object.Instantiate(bm.tilePrefab, worldPosition, Quaternion.identity, bm.transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(bm, sTile, setupParameters.board.isHex);
            tileViews.Add(sTile.pos, tileView);
        }

        foreach (SMaxPawnsPerRank maxPawns in setupParameters.maxPawns)
        {
            for (int i = 0; i < maxPawns.max; i++)
            {
                PawnDef pawnDef = GameManager.instance.orderedPawnDefList.FirstOrDefault(def => def.rank == maxPawns.rank);
                Pawn pawn = new(pawnDef, (Player)setupParameters.player, true);
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

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {
        if (bm.clickInputManager.isOverUI)
        {
            // do nothing
        }
        else if (!bm.tileViews.Keys.Contains(bm.hoveredPos))
        {
            // do nothing
        }
        else if (!setupParameters.board.GetTileFromPos(hoveredPosition).IsTileEligibleForPlayer((int)bm.player))
        {
            // do nothing
        }
        else if (bm.currentHoveredPawnView != null)
        {
            PawnView deadPawnView = bm.currentHoveredPawnView;
            SPawn newState = new(deadPawnView.pawn)
            {
                isAlive = false,
                pos = Globals.PURGATORY,
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
                pos = bm.hoveredPos,
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
                    pos = Globals.PURGATORY,
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

        if (Rules.IsSetupValid((int)bm.player, setupParameters, setupPawns))
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
            bm.EndVortex();
        }
    }

    public void ExitState()
    {
        Debug.Assert(moveSubmitted);
        bm.ClearOutlineEffects();
        SelectPawnView(null);
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

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {
        if (bm.clickInputManager.isOverUI)
        {
            // do nothing
        }
        else if (!bm.IsPosValid(hoveredPosition))
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
                    if (PlayerPrefs.GetInt("FASTMODE") == 1)
                    {
                        OnSubmitMove();
                    }
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
                TileView tileView = bm.GetTileViewByPos(tile.pos);
                tileView.OnHighlight(true);
                highlightedTileViews.Add(tileView);
                PawnView pawnViewOnTile = bm.GetPawnViewByPos(tile.pos);
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
        bool moveIsValid = movableTilesList.Any(tile => tile.pos == pos);
        if (!moveIsValid)
        {
            return false;
        }
        if (maybeQueuedMove.HasValue)
        {
            TileView oldTileView = bm.GetTileViewByPos(maybeQueuedMove.Value.pos);
            oldTileView.OnArrow(false);
        }
        maybeQueuedMove = new SQueuedMove((int)bm.player, pawnView.pawn.pawnId,  pawnView.pawn.pos,pos);
        TileView tileView = bm.GetTileViewByPos(maybeQueuedMove.Value.pos);
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
        bm.StartVortex();
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

    public void OnHover(Vector2Int oldPos, Vector2Int newPos)
    {
        
    }

    public void OnClick(Vector2Int hoveredPosition)
    {

    }
}