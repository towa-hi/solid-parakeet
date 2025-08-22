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

    public RenderEffect sphereRenderEffect;
    
    // never changes
    public Vector2Int posView; // this is the key of tileView it must never change
    public bool passableView;
    public Team setupView;
    public uint setupZoneView;
    
    
    public bool isHovered = false;
    public bool isSelected = false;
    public bool isTargetable = false;
    public bool isMovePairStart = false;
    public bool isMovePairTarget = false;
    public bool isSetupTile = false;
    
    
    public Color redTeamColor;
    public Color blueTeamColor;

    public Color flatColor;
    
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    public TweenSettings<Vector3> startTweenSettings;
    public Sequence startSequence;
    public Tween currentTween;
    Vector3 initialElevatorLocalPos;
    
    static Vector3 hoveredElevatorLocalPos = new Vector3(0, Globals.HoveredHeight, 0);
    static Vector3 selectedElevatorLocalPos = new Vector3(0, Globals.SelectedHoveredHeight, 0);
    
    // Base color pulse config
    bool pulseBaseColor;
    [SerializeField] float emissionPulseSpeed = 2f;
    [SerializeField] float minEmissionAlpha = 0.25f;
    [SerializeField] float maxEmissionAlpha = 0.5f;
    
    public void Initialize(TileState tile, bool hex)
    {
        // never changes
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
        tileModel = hex ? hexTileModel : squareTileModel;
        tileModel.gameObject.SetActive(passableView);
    }
    
    void OnDestroy()
    {
    }

    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        // what to do
        bool? setIsHovered = null;
        bool? setIsSelected = null;
        bool? setIsTargetable = null;
        bool? setIsMovePairStart = null;
        bool? setIsMovePairTarget = null;
        bool? setIsSetupTile = null;
        bool? setIsTargetableSphere = null;
        MoveInputTool? setMoveInputTool = null;
        Transform elevator = tileModel.elevator;
        // figure out what to do based on what happened
        // for net changes
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            setIsHovered = false;
            setIsSelected = false;
            setIsTargetable = false;
            setIsMovePairStart = false;
            setIsMovePairTarget = false;
            setIsSetupTile = false;
            setIsTargetableSphere = false;
            switch (netStateUpdated.phase)
            {
                case SetupCommitPhase setupCommitPhase:
                    setIsSetupTile = true;
                    break;
                case ResolvePhase resolvePhase:
                    setIsHovered = resolvePhase.hoveredPos == posView;
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
                    setIsTargetableSphere = moveCommitPhase.selectedPos == null && moveCommitPhase.hoveredValidTargetPositions.Contains(posView);
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
        bool pulse = false;
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
            pulse = true;
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
        SetTopColor(finalColor);
        pulseBaseColor = pulse;
        if (!pulseBaseColor)
        {
            // Ensure base color matches the static color when not pulsing
            Material mat = tileModel.flatRenderer.material;
            mat.SetColor(BaseColorProperty, flatColor);
        }
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
            currentTween = Tween.LocalPositionAtSpeed(elevator, finalTargetPos, 0.3f, Ease.OutCubic).OnComplete(() =>
            {
                elevator.localPosition = finalTargetPos;
            });
        }
        if (setIsTargetableSphere is bool inIsTargetableSphere)
        {
            sphereRenderEffect.SetEffect(EffectType.SELECTOUTLINE, inIsTargetableSphere);
        }
    }
    
    void SetTopColor(Color color)
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
    
    void Update()
    {
        if (!pulseBaseColor)
        {
            return;
        }
        // Pulse the alpha of the base color using absolute time
        float t = (Mathf.Sin(Time.time * emissionPulseSpeed) + 1f) * 0.5f; // 0..1
        float alpha = Mathf.Lerp(minEmissionAlpha, maxEmissionAlpha, t);
        Material mat = tileModel.flatRenderer.material;
        Color c = flatColor;
        c.a = alpha;
        mat.SetColor(BaseColorProperty, c);
    }
    
    
}
