using System;
using System.Linq;
using Contract;
using UnityEngine;
using PrimeTween;
using Random = UnityEngine.Random;

public class TestTileView : MonoBehaviour
{
    public Transform origin;
    
    public TileModel tileModel;
    public TileModel hexTileModel;
    public TileModel squareTileModel;
    public Tile tile;
    public Color baseColor;
    public Color redTeamColor;
    public Color blueTeamColor;

    public Color flatColor;
    
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    Tween pulseTween;
    public TweenSettings<Vector3> startTweenSettings;
    public Sequence startSequence;
    public Tween currentTween;
    Vector3 initialElevatorLocalPos;
    
    static Vector3 hoveredElevatorLocalPos = new Vector3(0, Globals.HoveredHeight, 0);
    static Vector3 selectedElevatorLocalPos = new Vector3(0, Globals.SelectedHoveredHeight, 0);

    TestBoardManager bm;
    
    public void Initialize(Tile inTile, TestBoardManager inBoardManager)
    {
        tile = inTile;
        bm = inBoardManager;
        //bm.clickInputManager.OnPositionHovered += OnHover;
        bm.OnClientGameStateChanged += OnClientGameStateChanged;
        bm.OnGameHover += OnGameHover;
        gameObject.name = $"Tile ({tile.pos.x}, {tile.pos.y})";
        hexTileModel.gameObject.SetActive(false);
        squareTileModel.gameObject.SetActive(false);
        tileModel = bm.boardDef.isHex ? hexTileModel : squareTileModel;
        tileModel.gameObject.SetActive(true);
        ShowTile(tile.isPassable);
        initialElevatorLocalPos = tileModel.elevator.localPosition;
    }

    void OnDestroy()
    {
        pulseTween.Stop();
    }

    public void StartPulse()
    {
        // Stop any existing pulse
        pulseTween.Stop();

        // Get the material
        Material mat = tileModel.flatRenderer.material;
        Color color = flatColor;

        // Create a sequence that pulses alpha between 0.5 and 0.25
        TweenSettings settings = new TweenSettings(
            duration: 1f,
            cycles: -1, 
            cycleMode: CycleMode.Yoyo
        );
        
        pulseTween = Tween.Custom(
            0.5f,  // Start alpha
            0.25f, // End alpha
            onValueChange: alpha => {
                color.a = alpha;
                mat.SetColor(BaseColorProperty, color);
            },
            settings: settings
        );
    }

    public void StopPulse()
    {
        pulseTween.Stop();
        
        // Reset alpha to default
        Material mat = tileModel.flatRenderer.material;
        mat.SetColor(BaseColorProperty, Color.clear);
    }

    public Vector3 targetElevatorLocalPosition;
    void OnGameHover(Vector2Int hoveredPos, TestTileView tileView, TestPawnView pawnView, ITestPhase phase)
    {
        bool isHovered = tile.pos == hoveredPos;
        bool elevateTile = false;
        bool drawOutline = false;
        Transform elevator = tileModel.elevator;
        tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, false);
        switch (phase)
        {
            case MovementTestPhase movementTestPhase:
                Contract.Pawn? pawnOnTile = bm.cachedLobby.GetPawnByPosition(tile.pos);
                switch (movementTestPhase.clientState.subState)
                {
                    case ResolvingMovementClientSubState resolvingMovementClientSubState:
                        break;
                    case SelectingPawnMovementClientSubState selectingPawnMovementClientSubState:
                        if (pawnOnTile.HasValue)
                        {
                            if (selectingPawnMovementClientSubState.selectedPawnId.HasValue)
                            {
                                if (selectingPawnMovementClientSubState.selectedPawnId.Value.ToString() == pawnOnTile.Value.pawn_id)
                                {
                                    elevateTile = true;
                                }
                            }
                        }
                        if (isHovered)
                        {
                            if (pawnOnTile.HasValue)
                            {
                                if ((Team)pawnOnTile.Value.team == movementTestPhase.clientState.team)
                                {
                                    elevateTile = true;
                                    drawOutline = true;
                                }
                            }
                        }
                        break;
                    case SelectingPosMovementClientSubState selectingPosMovementClientSubState:
                        if (pawnOnTile.HasValue)
                        {
                            if (selectingPosMovementClientSubState.selectedPawnId.ToString() == pawnOnTile.Value.pawn_id)
                            {
                                elevateTile = true;
                            }
                        }
                        if (isHovered)
                        {
                            if (pawnOnTile.HasValue)
                            {
                                if ((Team)pawnOnTile.Value.team == movementTestPhase.clientState.team)
                                {
                                    elevateTile = true;
                                    drawOutline = true;
                                }
                            }
                            if (selectingPosMovementClientSubState.highlightedTiles.Contains(tile.pos))
                            {
                                drawOutline = true;
                            }
                        }
                        break;
                    case WaitingOpponentHashMovementClientSubState waitingOpponentHashMovementClientSubState:
                        break;
                    case WaitingOpponentMoveMovementClientSubState waitingOpponentMoveMovementClientSubState:
                        break;
                    case WaitingUserHashMovementClientSubState waitingUserHashMovementClientSubState:
                        break;
                    case GameOverMovementClientSubState gameOverMovementClientSubState:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                break;
            case SetupTestPhase setupTestPhase:
                if (isHovered)
                {
                    bool isOccupied = setupTestPhase.clientState.commitments.Values.Any(commitment => commitment.starting_pos.ToVector2Int() == tile.pos);
                    if (isOccupied)
                    {
                        drawOutline = true;
                    }
                    else
                    {
                        if (setupTestPhase.clientState.selectedRank.HasValue)
                        {
                            if (setupTestPhase.clientState.GetUnusedCommitment(setupTestPhase.clientState.selectedRank.Value).HasValue)
                            {
                                if (tile.IsTileSetupAllowed(setupTestPhase.clientState.team))
                                {
                                    drawOutline = true;
                                }
                            }
                        }
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        if (drawOutline)
        {
            tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, true);
        }
        if (elevateTile)
        {
            targetElevatorLocalPosition = hoveredElevatorLocalPos;
        }
        else
        {
            targetElevatorLocalPosition = initialElevatorLocalPos;
        }
        if (elevator.localPosition != targetElevatorLocalPosition)
        {
            //Debug.Log($"{tile.pos} elevator: {elevator.localPosition} target: {targetElevatorLocalPosition}");
            currentTween = Tween.LocalPositionAtSpeed(elevator, targetElevatorLocalPosition, 0.3f, Ease.OutCubic).OnComplete(() =>
            {
                elevator.localPosition = targetElevatorLocalPosition;
            });
        }
    }
    
    uint oldPhase = 999;
    void OnClientGameStateChanged(Lobby lobby, ITestPhase phase)
    {
        bool phaseChanged = lobby.phase != oldPhase;
        switch (phase)
        {
            case MovementTestPhase movementTestPhase:
                Contract.Pawn? pawnOnTile = lobby.GetPawnByPosition(tile.pos);
                if (phaseChanged)
                {
                    SetSetupEmissionHighlight(false);
                }
                currentTween.Stop();
                StopPulse();
                tileModel.renderEffect.SetEffect(EffectType.FILL, false);
                SetTopEmission(Color.clear);
                if (pawnOnTile.HasValue)
                {
                    Contract.Pawn p = pawnOnTile.Value;
                    if (PlayerPrefs.GetInt("CHEATMODE") == 1 || (Team)p.team == movementTestPhase.clientState.team || p.is_revealed)
                    {
                        PawnDef def = Globals.FakeHashToPawnDef(pawnOnTile.Value.pawn_def_hash);
                        if (def.movementRange == 0)
                        {
                            SetTopEmission(new Color(0, 0, 0, 100f));
                        }
                    }
                }
                switch (movementTestPhase.clientState.subState)
                {
                    case SelectingPawnMovementClientSubState selectingPawnSubState:
                        if (selectingPawnSubState.selectedPawnId.HasValue)
                        {
                            Contract.Pawn queuedPawn = lobby.GetPawnById(selectingPawnSubState.selectedPawnId.Value);
                            bool isOriginSelectingPawn = queuedPawn.pos.ToVector2Int() == tile.pos;
                            if (isOriginSelectingPawn)
                            {
                                SetTopEmission(Color.red);
                            }
                            if (selectingPawnSubState.selectedPos == tile.pos)
                            {
                                SetTopEmission(Color.green);
                            }
                        }
                        break;
                    case SelectingPosMovementClientSubState selectingPosSubState:
                        // If we have a selected pawn, highlight its position and possible moves
                        Contract.Pawn selectedPawn = lobby.GetPawnById(selectingPosSubState.selectedPawnId);
                        bool isOriginSelectingPos = selectedPawn.pos.ToVector2Int() == tile.pos;
                        if (isOriginSelectingPos)
                        {
                            SetTopEmission(Color.red);
                            StartPulse();
                        }
                        if (selectingPosSubState.highlightedTiles.Contains(tile.pos))
                        {
                            SetTopEmission(Color.green);
                            StartPulse();
                        }
                        break;
                    case WaitingUserHashMovementClientSubState:
                        bool isTarget = movementTestPhase.clientState.myTurnMove.pos.ToVector2Int() == tile.pos;
                        Contract.Pawn pawn = lobby.GetPawnById(movementTestPhase.clientState.myTurnMove.pawn_id);
                        bool isOrigin = pawn.pos.ToVector2Int() == tile.pos;
                        if (isOrigin)
                        {
                            SetTopEmission(Color.red);
                        }
                        if (isTarget)
                        {
                            SetTopEmission(Color.green);
                        }
                        break;
                }
                break;
                
            case SetupTestPhase setupTestPhase:
                if (phaseChanged)
                {
                    SetSetupEmissionHighlight(true);
                    PlayStartAnimation();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        oldPhase = lobby.phase;
    }

    public float delayFactor = 0.1f;
    float delay = 0.001f;
    void PlayStartAnimation()
    {
        if (!tileModel.isActiveAndEnabled) return;
        if (startSequence.isAlive) return;
        if (!bm) return;
        // get the delay
        // get distance to closest waveOrigin
        float distanceToWave1 = Vector3.Distance(origin.position, bm.waveOrigin1.position);
        float distanceToWave2 = Vector3.Distance(origin.position, bm.waveOrigin2.position);
        float minDistance = Mathf.Min(distanceToWave1, distanceToWave2);
        delay = minDistance;
        delay = delay - 8f;
        if (delay <= 0f)
        {
            delay = 0.01f;
        }
        Vector3 destination = tileModel.transform.localPosition;
        startTweenSettings.endValue = destination;
        startSequence = Sequence.Create()
            .ChainCallback(() =>
            {
                ShowTile(false);
            })
            .ChainDelay(delay)
            .ChainCallback(() =>
            {
                ShowTile(true);
            })
            .Chain(Tween.LocalPosition(tileModel.transform, startTweenSettings));
    }
    
    void SetSetupEmissionHighlight(bool highlight)
    {
        if (highlight)
        {
            switch (tile.setupTeam)
            {
                case Team.NONE:
                    SetTopEmission(Color.clear);
                    break;
                case Team.RED:
                    SetTopEmission(redTeamColor);
                    break;
                case Team.BLUE:
                    SetTopEmission(blueTeamColor);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            SetTopEmission(baseColor);
        }
    }
    
    void ShowTile(bool show)
    {
        tileModel.gameObject.SetActive(show);
    }
    
    void SetTopEmission(Color color)
    {
        Material mat = tileModel.flatRenderer.material;
        if (color != Color.clear)
        {
            Color colorWithAlpha = new Color(color.r, color.g, color.b, 0.5f);
            flatColor = colorWithAlpha;
        }
        else
        {
            flatColor = color;
        }
        mat.SetColor(BaseColorProperty, flatColor);
    }
    
    
}
