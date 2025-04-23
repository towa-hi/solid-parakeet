using System;
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

    TestBoardManager bm;
    
    public void Initialize(Tile inTile, TestBoardManager inBoardManager)
    {
        tile = inTile;
        bm = inBoardManager;
        //bm.clickInputManager.OnPositionHovered += OnHover;
        bm.OnClientGameStateChanged += OnClientGameStateChanged;
        bm.clickInputManager.OnPositionHovered += OnHover;
        gameObject.name = $"Tile ({tile.pos.x}, {tile.pos.y})";
        hexTileModel.gameObject.SetActive(false);
        squareTileModel.gameObject.SetActive(false);
        tileModel = bm.boardDef.isHex ? hexTileModel : squareTileModel;
        tileModel.gameObject.SetActive(true);
        ShowTile(tile.isPassable);
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

    void OnHover(Vector2Int pos)
    {
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, tile.pos == pos);
                break;
            case SetupTestPhase setupTestPhase:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    uint oldPhase = 999;
    void OnClientGameStateChanged(Lobby lobby, ITestPhase phase)
    {
        bool phaseChanged = lobby.phase != oldPhase;
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                if (phaseChanged)
                {
                    SetSetupEmissionHighlight(false);
                }
                StopPulse();
                tileModel.renderEffect.SetEffect(EffectType.FILL, false);
                SetTopEmission(Color.clear);

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
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        oldPhase = lobby.phase;
    }

    void SetSetupEmissionHighlight(bool highlight)
    {
        if (highlight)
        {
            switch (tile.setupTeam)
            {
                case Team.NONE:
                    SetTopEmission(baseColor);
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
