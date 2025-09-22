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
    public bool enableDebugLogs;
    public ProceduralArrow arrow;
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

    public TileView pointedTile;
    
    
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


    public void SetTileDebug()
    {
        Color finalColor = setupView switch
        {
            Team.RED => redTeamColor,
            Team.BLUE => blueTeamColor,
            _ => Color.clear,
        };
        SetTopColor(finalColor);
        sphereRenderEffect.SetEffect(EffectType.SELECTOUTLINE, false);
    }
    
    void OnDestroy()
    {
    }

    // Subscriptions are managed by board lifecycle; avoid toggling on enable/disable
    public void AttachSubscriptions()
    {
        ViewEventBus.OnClientModeChanged += HandleClientModeChanged;
        ViewEventBus.OnSetupHoverChanged += HandleSetupHoverChanged;
        ViewEventBus.OnMoveHoverChanged += HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged += HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged += HandleMovePairsChanged;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
        ViewEventBus.OnSetupHoverChanged -= HandleSetupHoverChanged;
        ViewEventBus.OnMoveHoverChanged -= HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged -= HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged -= HandleMovePairsChanged;
    }

    void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        if (enableDebugLogs) Debug.Log($"TileView[{posView}]: ClientModeChanged mode={mode}");
        // Reset base visuals common to any mode switch
        isHovered = false;
        isSelected = false;
        isTargetable = false;
        isMovePairStart = false;
        isMovePairTarget = false;
        pointedTile = null;
        arrow.gameObject.SetActive(false);
        tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, false);
        tileModel.renderEffect.SetEffect(EffectType.SELECTOUTLINE, false);
        tileModel.renderEffect.SetEffect(EffectType.FILL, false);
        tileModel.elevator.localPosition = initialElevatorLocalPos;
        // Mode-specific initialization
        isSetupTile = (mode == ClientMode.Setup) && setupView != Team.NONE;
        SetTileDebug();
        // When entering modes, seed arrows immediately for Resolve only (Move arrows will be driven by events)
        switch (mode)
        {
            case ClientMode.Move:
                SetTopColor(Color.clear);
                break;
            case ClientMode.Resolve:
            {
                // Use resolve payload moves for arrows
                bool start = false;
                bool target = false;
                TileView targetTile = null;
                var tr = ui.ResolveData;
                if (tr.moves != null)
                {
                    foreach (var kv in tr.moves)
                    {
                        var mv = kv.Value;
                        if (mv.from == posView)
                        {
                            start = true;
                            if (ViewEventBus.TileViewResolver != null) targetTile = ViewEventBus.TileViewResolver(mv.target);
                        }
                        if (mv.target == posView) target = true;
                    }
                }
                isMovePairStart = start;
                isMovePairTarget = target;
                arrow.gameObject.SetActive(start && targetTile != null);
                if (start && targetTile != null)
                {
                    arrow.ArcFromTiles(this, targetTile);
                    pointedTile = targetTile;
                }
                Color col = Color.clear;
                if (isMovePairStart) col = Color.green * 0.5f;
                if (isMovePairTarget) col = Color.blue * 0.5f;
                SetTopColor(col);
                break;
            }
        }
    }

    void HandleSetupHoverChanged(Vector2Int hoveredPos, bool isMyTurn, SetupInputTool _)
    {
        if (enableDebugLogs) Debug.Log($"TileView[{posView}]: SetupHover pos={hoveredPos} myTurn={isMyTurn}");
        isHovered = isMyTurn && posView == hoveredPos;
        tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, isHovered);
        // In setup mode we do NOT elevate tiles on hover. Ensure elevator stays at base.
        if (tileModel.elevator.localPosition != initialElevatorLocalPos)
        {
            tileModel.elevator.localPosition = initialElevatorLocalPos;
        }
    }

    void HandleMoveHoverChanged(Vector2Int hoveredPos, bool isMyTurn, MoveInputTool tool, System.Collections.Generic.HashSet<Vector2Int> hoverTargets)
    {
        if (!isMyTurn) { isHovered = false; }
        else { isHovered = posView == hoveredPos; }
        tileModel.renderEffect.SetEffect(EffectType.HOVEROUTLINE, isHovered);
        // Elevate lightly only in movement when selection intent
        Vector3 target = (isHovered && tool == MoveInputTool.SELECT) ? hoveredElevatorLocalPos : initialElevatorLocalPos;
        if (tileModel.elevator.localPosition != target)
        {
            currentTween = PrimeTween.Tween.LocalPositionAtSpeed(tileModel.elevator, target, 0.3f, PrimeTween.Ease.OutCubic).OnComplete(() =>
            {
                tileModel.elevator.localPosition = target;
            });
        }
        // Hover targetable sphere
        bool show = hoverTargets != null && hoverTargets.Contains(posView);
        sphereRenderEffect.SetEffect(EffectType.SELECTOUTLINE, show);
    }

    void HandleMoveSelectionChanged(Vector2Int? selected, System.Collections.Generic.HashSet<Vector2Int> validTargets)
    {
        isSelected = selected.HasValue && selected.Value == posView;
        isTargetable = validTargets.Contains(posView);
        tileModel.renderEffect.SetEffect(EffectType.SELECTOUTLINE, isSelected);
        tileModel.renderEffect.SetEffect(EffectType.FILL, isTargetable);
        Vector3 target = (isSelected) ? selectedElevatorLocalPos : initialElevatorLocalPos;
        if (tileModel.elevator.localPosition != target)
        {
            currentTween = PrimeTween.Tween.LocalPositionAtSpeed(tileModel.elevator, target, 0.3f, PrimeTween.Ease.OutCubic).OnComplete(() =>
            {
                tileModel.elevator.localPosition = target;
            });
        }
    }

    void HandleMovePairsChanged(System.Collections.Generic.Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, System.Collections.Generic.Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs)
    {
        // Recompute move pair flags for this tile
        isMovePairStart = false;
        isMovePairTarget = false;
        TileView targetTile = null;
        foreach (var kv in newPairs)
        {
            if (kv.Value.start == posView) { isMovePairStart = true; }
            if (kv.Value.target == posView) { isMovePairTarget = true; }
            if (kv.Value.start == posView)
            {
                targetTile = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(kv.Value.target) : null;
            }
        }
        arrow.gameObject.SetActive(isMovePairStart && targetTile != null);
        if (isMovePairStart && targetTile != null) { arrow.ArcFromTiles(this, targetTile); pointedTile = targetTile; }
        Color finalColor = Color.clear;
        if (isMovePairStart) finalColor = Color.green * 0.5f;
        if (isMovePairTarget) finalColor = Color.blue * 0.5f;
        SetTopColor(finalColor);
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

    public void OverrideArrow(Transform target)
    {
        arrow.Clear();
        if (target)
        {
            arrow.PointToTarget(origin, target);
        }
    }
}
