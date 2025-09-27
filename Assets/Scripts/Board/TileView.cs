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
    
    
    // Avoid caching transient UI flags; derive from events instead
    
    public Color redTeamColor;
    public Color blueTeamColor;

    public Color flatColor;

    public TileView pointedTile;
    
    static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    static readonly int FogColorProperty = Shader.PropertyToID("_Color");
    static readonly int FadeAmountProperty = Shader.PropertyToID("_FadeAmount");
    public TweenSettings<Vector3> startTweenSettings;
    public Sequence startSequence;
    public Tween currentTween;
    Vector3 initialElevatorLocalPos;
    
    static Vector3 hoveredElevatorLocalPos = new Vector3(0, Globals.HoveredHeight, 0);
    static Vector3 selectedElevatorLocalPos = new Vector3(0, Globals.SelectedHoveredHeight, 0);
    
    // Fog/reveal visual state
    Tween fogAlphaTween;
    Tween revealFadeTween;
    float currentFogAlpha01 = 0f;
    bool? lastRevealedState = null;
    const float HiddenMovedAlpha01 = 150f / 255f;
    const float HiddenUnmovedAlpha01 = 200f / 255f;
    const float UnrevealedFadeAmount = 1f; // as requested
    const float RevealedFadeAmount = 1f;   // as requested
    
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
        
        hexTileModel.gameObject.SetActive(false);
        squareTileModel.gameObject.SetActive(false);
        tileModel = hex ? hexTileModel : squareTileModel;
        SetVisible(tile.passable);
    }


    public void SetTileDebug()
    {
        SetTopColor(Color.clear);
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
        ViewEventBus.OnResolveCheckpointChanged += HandleResolveCheckpointChangedForTile;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
        ViewEventBus.OnSetupHoverChanged -= HandleSetupHoverChanged;
        ViewEventBus.OnMoveHoverChanged -= HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged -= HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged -= HandleMovePairsChanged;
        ViewEventBus.OnResolveCheckpointChanged -= HandleResolveCheckpointChangedForTile;
    }

	void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        if (enableDebugLogs) Debug.Log($"TileView[{posView}]: ClientModeChanged mode={mode}");
        // Reset base visuals common to any mode switch
		SetArrow(null);
		SetRenderEffect(EffectType.HOVEROUTLINE, false);
		SetRenderEffect(EffectType.SELECTOUTLINE, false);
		SetRenderEffect(EffectType.FILL, false);
		SetElevator(initialElevatorLocalPos.y);
        // Mode-specific initialization
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
                SetArrow((start && targetTile != null) ? targetTile : null);
                Color col = Color.clear;
                if (start) col = Color.green * 0.5f;
                if (target) col = Color.blue * 0.5f;
                SetTopColor(col);
                break;
            }
        }

        // Update fog visuals based on current mode/context
        UpdateFogForContext(mode, net, ui);
    }

	void HandleSetupHoverChanged(Vector2Int hoveredPos, bool isMyTurn, SetupInputTool _)
    {
        if (enableDebugLogs) Debug.Log($"TileView[{posView}]: SetupHover pos={hoveredPos} myTurn={isMyTurn}");
        bool hovered = isMyTurn && posView == hoveredPos;
        SetRenderEffect(EffectType.HOVEROUTLINE, hovered);
        // In setup mode we do NOT elevate tiles on hover. Ensure elevator stays at base.
        if (tileModel.elevator.localPosition != initialElevatorLocalPos)
        {
            tileModel.elevator.localPosition = initialElevatorLocalPos;
        }
    }

	void HandleMoveHoverChanged(Vector2Int hoveredPos, bool isMyTurn, MoveInputTool tool, System.Collections.Generic.HashSet<Vector2Int> hoverTargets)
    {
        bool hovered = isMyTurn && posView == hoveredPos;
        SetRenderEffect(EffectType.HOVEROUTLINE, hovered);
        // Elevate lightly only in movement when selection intent
		Vector3 target = (hovered && tool == MoveInputTool.SELECT) ? hoveredElevatorLocalPos : initialElevatorLocalPos;
		SetElevator(target.y);
        // Hover targetable sphere
        bool show = hoverTargets != null && hoverTargets.Contains(posView);
        sphereRenderEffect.SetEffect(EffectType.SELECTOUTLINE, show);
    }

	void HandleMoveSelectionChanged(Vector2Int? selected, System.Collections.Generic.HashSet<Vector2Int> validTargets)
    {
        bool selectedNow = selected.HasValue && selected.Value == posView;
        bool targetableNow = validTargets.Contains(posView);
        SetRenderEffect(EffectType.SELECTOUTLINE, selectedNow);
        SetRenderEffect(EffectType.FILL, targetableNow);
		Vector3 target = (selectedNow) ? selectedElevatorLocalPos : initialElevatorLocalPos;
		SetElevator(target.y);
    }

	void HandleMovePairsChanged(System.Collections.Generic.Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, System.Collections.Generic.Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs)
    {
        // Recompute move pair flags for this tile (no caching)
        bool isStart = false;
        bool isTarget = false;
        TileView targetTile = null;
        foreach (var kv in newPairs)
        {
            if (kv.Value.start == posView) { isStart = true; }
            if (kv.Value.target == posView) { isTarget = true; }
            if (kv.Value.start == posView)
            {
                targetTile = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(kv.Value.target) : null;
            }
        }
        SetArrow((isStart && targetTile != null) ? targetTile : null);
        Color finalColor = Color.clear;
        if (isStart) finalColor = Color.green * 0.5f;
        if (isTarget) finalColor = Color.blue * 0.5f;
        SetTopColor(finalColor);
    }

    // Resolve checkpoint updates: drive fog based on snapshot temporary states
    void HandleResolveCheckpointChangedForTile(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        UpdateFogForResolve(checkpoint, tr, battleIndex, net);
    }

    // Compute fog for non-resolve vs resolve contexts
    void UpdateFogForContext(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        if (mode == ClientMode.Resolve && ui != null && ui.ResolveData.pawnDeltas != null)
        {
            UpdateFogForResolve(ResolveCheckpoint.Pre, ui.ResolveData, -1, net);
            return;
        }
        // Default (Setup/Move/others): look at current net state
        UpdateFogFromNetwork(net);
    }

    void UpdateFogFromNetwork(GameNetworkState net)
    {
        PawnState? occ = net.GetAlivePawnFromPosChecked(posView);
        if (occ is PawnState pawn)
        {
            ApplyFogForPawn(true, pawn.moved);
            HandleRevealChange(pawn.zz_revealed);
            return;
        }
        // No pawn: clear fog
        ApplyFogForPawn(false, false);
        HandleRevealChange(true);
    }

    void UpdateFogForResolve(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        bool revealed = true;
        bool moved = false;
        bool hasPawn = false;
        switch (checkpoint)
        {
            case ResolveCheckpoint.Pre:
            {
                if (tr.preSnapshot != null)
                {
                    var occ = tr.preSnapshot.Where(p => p.alive && p.pos == posView);
                    if (occ.Any())
                    {
                        var p = occ.First();
                        hasPawn = true;
                        revealed = p.zz_revealed;
                        moved = p.moved;
                    }
                    else { revealed = true; }
                }
                else
                {
                    var preOccupants = tr.pawnDeltas.Values.Where(d => d.preAlive && d.prePos == posView);
                    if (preOccupants.Any())
                    {
                        var d = preOccupants.First();
                        hasPawn = true;
                        revealed = d.preRevealed;
                        // moved flag unknown in delta pre -> assume false
                        moved = false;
                    }
                    else { revealed = true; }
                }
                break;
            }
            case ResolveCheckpoint.PostMoves:
            {
                if (tr.postMovesSnapshot != null)
                {
                    var occ = tr.postMovesSnapshot.Where(p => p.alive && p.pos == posView);
                    if (occ.Any())
                    {
                        var p = occ.First();
                        hasPawn = true;
                        revealed = p.zz_revealed;
                        moved = p.moved;
                    }
                    else { revealed = true; }
                }
                else
                {
                    var postOccupants = tr.pawnDeltas.Values.Where(d => d.postAlive && d.postPos == posView);
                    if (postOccupants.Any())
                    {
                        var d = postOccupants.First();
                        hasPawn = true;
                        // After moves, we still only have preRevealed in deltas here
                        revealed = d.preRevealed;
                        moved = false;
                    }
                    else { revealed = true; }
                }
                break;
            }
            case ResolveCheckpoint.Battle:
            {
                if (tr.battleSnapshots != null && tr.battleSnapshots.Length > 0)
                {
                    int idx = Mathf.Clamp(battleIndex, 0, tr.battleSnapshots.Length - 1);
                    var snap = tr.battleSnapshots[idx];
                    var occ = snap.Where(p => p.alive && p.pos == posView);
                    if (occ.Any())
                    {
                        var p = occ.First();
                        hasPawn = true;
                        revealed = p.zz_revealed;
                        moved = p.moved;
                    }
                    else { revealed = true; }
                }
                else
                {
                    var postOccupants = tr.pawnDeltas.Values.Where(d => d.postAlive && d.postPos == posView);
                    if (postOccupants.Any())
                    {
                        var d = postOccupants.First();
                        hasPawn = true;
                        revealed = d.postRevealed;
                        moved = false;
                    }
                    else { revealed = true; }
                }
                break;
            }
            case ResolveCheckpoint.Final:
            default:
            {
                PawnState? occ = net.GetAlivePawnFromPosChecked(posView);
                if (occ is PawnState pawn)
                {
                    hasPawn = true;
                    revealed = pawn.zz_revealed;
                    moved = pawn.moved;
                }
                else { revealed = true; }
                break;
            }
        }
        ApplyFogForPawn(hasPawn, moved);
        HandleRevealChange(revealed);
    }


	// ===== Tier 1: Direct view mutators / tweens =====

	void SetVisible(bool visible)
	{
		if (tileModel == null || tileModel.gameObject == null) return;
		if (tileModel.gameObject.activeSelf != visible)
		{
			tileModel.gameObject.SetActive(visible);
		}
	}
	void SetElevator(float localY)
	{
		if (tileModel == null || tileModel.elevator == null) return;
		Vector3 current = tileModel.elevator.localPosition;
		Vector3 target = new Vector3(current.x, localY, current.z);
		if (current == target) return;
		currentTween = PrimeTween.Tween.LocalPositionAtSpeed(tileModel.elevator, target, 0.3f, PrimeTween.Ease.OutCubic).OnComplete(() =>
		{
			tileModel.elevator.localPosition = target;
		});
	}

	void SetRenderEffect(EffectType effect, bool enabled)
	{
		if (tileModel == null || tileModel.renderEffect == null) return;
		tileModel.renderEffect.SetEffect(effect, enabled);
	}

	void SetArrow(TileView target)
	{
		if (arrow == null) return;
		if (target != null)
		{
			arrow.gameObject.SetActive(true);
			arrow.ArcFromTiles(this, target);
			pointedTile = target;
		}
		else
		{
			arrow.Clear();
			arrow.gameObject.SetActive(false);
			pointedTile = null;
		}
	}

	void SetFogFade(bool isRevealed)
	{
		Renderer r = tileModel != null ? (tileModel.topRenderer != null ? tileModel.topRenderer : tileModel.flatRenderer) : null;
		if (r == null) return;
		Material m = r.material;
		if (!m.HasProperty(FadeAmountProperty)) return;
		float from = m.GetFloat(FadeAmountProperty);
		float to = isRevealed ? 0f : 1f;
		if (revealFadeTween.isAlive) revealFadeTween.Stop();
		revealFadeTween = PrimeTween.Tween.Custom(from, to, 0.25f, (val) =>
		{
			m.SetFloat(FadeAmountProperty, val);
		}, Ease.OutCubic);
	}

	void SetFogColor(Color targetColor)
	{
		// Ensure fog object active
		if (tileModel != null && tileModel.fogObject != null && !tileModel.fogObject.activeSelf)
		{
			tileModel.fogObject.SetActive(true);
		}
		Renderer fogRenderer = tileModel.fogObject.GetComponent<Renderer>();
		Material fogMat = fogRenderer.material;
		Color baseColor = fogMat.HasProperty(FogColorProperty) ? fogMat.GetColor(FogColorProperty) : fogMat.color;
		Color startColor = baseColor;
		Color endColor = new Color(baseColor.r, baseColor.g, baseColor.b, targetColor.a);
		if (Mathf.Approximately(startColor.a, endColor.a))
		{
			if (fogMat.HasProperty(FogColorProperty)) fogMat.SetColor(FogColorProperty, endColor); else fogMat.color = endColor;
			currentFogAlpha01 = endColor.a;
			return;
		}
		if (fogAlphaTween.isAlive) fogAlphaTween.Stop();
		fogAlphaTween = PrimeTween.Tween.Custom(startColor.a, endColor.a, 0.2f, (val) =>
		{
			Color c = baseColor;
			c.a = val;
			if (fogMat.HasProperty(FogColorProperty)) fogMat.SetColor(FogColorProperty, c); else fogMat.color = c;
			currentFogAlpha01 = val;
		}, Ease.OutCubic);
	}

    void ApplyFogForPawn(bool hasPawn, bool hasMoved)
    {
		float targetAlpha = hasPawn ? (hasMoved ? HiddenMovedAlpha01 : HiddenUnmovedAlpha01) : 0f;
		SetFogColor(new Color(0f, 0f, 0f, targetAlpha));
    }

    void HandleRevealChange(bool isRevealed)
    {
        if (lastRevealedState.HasValue && lastRevealedState.Value == isRevealed)
        {
            return;
        }
        lastRevealedState = isRevealed;
        SetFogFade(isRevealed);
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

    public void OverrideArrow(Transform target)
    {
        arrow.Clear();
        if (target)
        {
            arrow.PointToTarget(origin, target);
        }
    }
}
