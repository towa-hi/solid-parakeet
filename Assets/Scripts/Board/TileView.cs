using System;
using System.Linq;
using Contract;
using UnityEngine;
using PrimeTween;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class TileView : MonoBehaviour
{
    public Transform origin;
    
    public TileModel tileModel;
    public TileModel hexTileModel;
    public TileModel squareTileModel;
    
    // never changes
    public Vector2Int posView; // this is the key of tileView it must never change
    public bool passableView;
    public Team setupView;
    public uint setupZoneView;
    
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
    public Vector3[] tileHeights;
    
    public void Initialize(TileState tile, bool hex)
    {
        // never changes
        tileHeights = new[]
        {
            new Vector3(0, 0),
            new Vector3(0, Globals.HoveredHeight, 0),
            new Vector3(0, Globals.SelectedHoveredHeight, 0),
        };
        posView = tile.pos;
        gameObject.name = $"Tile {posView}";
        
        
        SetTile(tile, hex);
        initialElevatorLocalPos = tileModel.elevator.localPosition;
    }

    void SetTile(TileState tile, bool hex)
    {
        if (tile.pos != posView)
        {
            throw new ArgumentException("set to wrong tile pos");
        }
        
        passableView = tile.passable;
        setupView = tile.setup;
        setupZoneView = tile.setup_zone;
        hexTileModel.gameObject.SetActive(false);
        squareTileModel.gameObject.SetActive(false);
        Debug.Log("SetTile setting tileModel to " + (hex ? "hex" : "square"));
        tileModel = hex ? hexTileModel : squareTileModel;
        // update view except posView which cant be updated because only boardManager can do that
        tileModel.gameObject.SetActive(passableView);
    }
    
    void OnDestroy()
    {
        pulseTween.Stop();
    }

    void DisplaySetupView(bool display)
    {
        if (display)
        {
            switch (setupView)
            {
                case Team.RED:
                    SetTopEmission(redTeamColor);
                    break;
                case Team.BLUE:
                    SetTopEmission(blueTeamColor);
                    break;
                case Team.NONE:
                    SetTopEmission(Color.clear);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            SetTopEmission(Color.clear);
        }
    }

    public bool isHovered = false;
    public bool isSelected = false;
    public bool isTargetable = false;
    public bool isMovePairStart = false;
    public bool isMovePairTarget = false;
    public bool isSetupTile = false;

    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        // what to do
        bool? setIsHovered = null;
        bool? setIsSelected = null;
        bool? setIsTargetable = null;
        bool? setIsMovePairStart = null;
        bool? setIsMovePairTarget = null;
        bool? setIsSetupTile = null;
        MoveInputTool? setMoveInputTool = null;
        // bool? setHoverOutline = null;
        //bool? setSelectOutline = null;
        //bool? setTargetableFill = null;
        //bool? setTargetEmission = null;
        //Color? overrideTargetEmissionColor = null;
        //bool? markBasePlannedStart = null;
        //bool? markBasePlannedTarget = null;
        //bool? markOverlayStart = null;
        //bool? markOverlayTarget = null;
        Transform elevator = tileModel.elevator;
        // figure out what to do based on what happened
        // for net changes
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            // Clear all visuals on any NetStateUpdated for simplicity
            SetTopEmission(Color.clear);
            setIsHovered = false;
            setIsSelected = false;
            setIsTargetable = false;
            setIsMovePairStart = false;
            setIsMovePairTarget = false;
            setIsSetupTile = false;
            switch (netStateUpdated.phase)
            {
                case SetupCommitPhase setupCommitPhase:
                    setIsSetupTile = true;
                    break;
                case MoveCommitPhase moveCommitPhase:
                {
                    setIsHovered = moveCommitPhase.hoveredPos == posView;
                    setIsSelected = moveCommitPhase.selectedPos.HasValue &&
                                    moveCommitPhase.selectedPos.Value == posView;
                    setIsTargetable = moveCommitPhase.validTargetPositions.Contains(posView);
                    if (moveCommitPhase.cachedNetState.IsMySubphase())
                    {
                        setIsMovePairStart = moveCommitPhase.movePairs.Any(kv => kv.Value.Item1 == posView);
                        setIsMovePairTarget = moveCommitPhase.movePairs.Any(kv => kv.Value.Item2 == posView);
                    }
                    else
                    {
                        setIsMovePairStart = moveCommitPhase.turnHiddenMoves.Any(hm => hm.start_pos == posView);
                        setIsMovePairTarget = moveCommitPhase.turnHiddenMoves.Any(hm => hm.target_pos == posView);
                    }
                    break;
                }
                case MoveProvePhase moveProvePhase:
                {
                    setIsHovered = moveProvePhase.hoveredPos == posView;
                    setIsMovePairStart = moveProvePhase.turnHiddenMoves.Any(hm => hm.start_pos == posView);
                    setIsMovePairTarget = moveProvePhase.turnHiddenMoves.Any(hm => hm.target_pos == posView);
                    break;
                }
                case RankProvePhase rankProvePhase:
                {
                    setIsHovered = rankProvePhase.hoveredPos == posView;
                    setIsMovePairStart = rankProvePhase.turnHiddenMoves.Any(hm => hm.start_pos == posView);
                    setIsMovePairTarget = rankProvePhase.turnHiddenMoves.Any(hm => hm.target_pos == posView);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(netStateUpdated.phase));
            }
        }

        // for local changes
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupHoverChanged(_, var newHoveredPos, var setupCommitPhase):
                {
                    setIsHovered = setupCommitPhase.cachedNetState.IsMySubphase() && posView == newHoveredPos;
                    break;
                }
                case MoveHoverChanged(var moveInputTool, var newHoveredPos, var moveCommitPhase):
                {
                    setMoveInputTool = moveInputTool;
                    setIsHovered = moveCommitPhase.cachedNetState.IsMySubphase() && posView == newHoveredPos;
                    break;
                }
                case MovePosSelected(var newPos, var targetablePositions, var movePairsSnapshot):
                    setIsSelected = newPos.HasValue && posView == newPos.Value;
                    setIsTargetable = targetablePositions.Contains(posView);
                    break;
                case MovePairUpdated(var movePairsSnapshot2, var changedPawnId, var phaseRef):
                    setIsSelected = false;
                    setIsTargetable = false;
                    setIsMovePairStart = movePairsSnapshot2.Any(kv => kv.Value.Item1 == posView);
                    setIsMovePairTarget = movePairsSnapshot2.Any(kv => kv.Value.Item2 == posView);
                    break;
            }
        }
        // now do the stuff

        if (setIsHovered is bool inIsHovered)
        {
            isHovered = inIsHovered;
        }
        if (setIsSelected is bool inIsSelected)
        {
            isSelected = inIsSelected;
        }
        if (setIsTargetable is bool inIsTargetable)
        {
            isTargetable = inIsTargetable;
        }
        if (setIsMovePairStart is bool inIsMovePairStart)
        {
            isMovePairStart = inIsMovePairStart;
        }
        if (setIsMovePairTarget is bool inIsMovePairTarget)
        {
            isMovePairTarget = inIsMovePairTarget;
        }

        if (setIsSetupTile is bool inIsSetupTile)
        {
            isSetupTile = inIsSetupTile;
        }
        
        tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, isHovered);
        tileModel.renderEffect.SetEffect(EffectType.SELECTOUTLINE, isSelected);
        tileModel.renderEffect.SetEffect(EffectType.FILL, isTargetable);

        Color finalColor = Color.clear;
        if (isMovePairStart)
        {
            finalColor = Color.green * 0.5f;
        }
        if (isMovePairTarget)
        {
            finalColor = Color.blue * 0.5f;
        }
        if (isSelected)
        {
            finalColor = Color.green;
        }
        if (isSetupTile)
        {
            finalColor = setupView switch
            {
                Team.RED => redTeamColor,
                Team.BLUE => blueTeamColor,
                _ => finalColor,
            };
        }
        SetTopEmission(finalColor);
        Vector3 finalTargetPos = initialElevatorLocalPos;
        if (isHovered)
        {
            if (setMoveInputTool is MoveInputTool.SELECT)
            {
                finalTargetPos = hoveredElevatorLocalPos;
            }
        }
        if (isSelected || isMovePairStart)
        {
            finalTargetPos = selectedElevatorLocalPos;
        }
        if (elevator.localPosition != finalTargetPos) 
        {
            Debug.Log($"moving to pos {finalTargetPos}");
            currentTween = Tween.LocalPositionAtSpeed(elevator, finalTargetPos, 0.3f, Ease.OutCubic).OnComplete(() =>
            {
                elevator.localPosition = finalTargetPos;
            });
        }
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
