using System;
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
    
    // Track whether this tile is currently selected in Move mode
    bool isSelected;
    
    static Vector3 hoveredElevatorLocalPos = new Vector3(0, Globals.HoveredHeight, 0);
    static Vector3 selectedElevatorLocalPos = new Vector3(0, Globals.SelectedHoveredHeight, 0);
    
    // Fog/reveal visual state
    public Tween fogAlphaTween;
    Tween revealFadeTween;
    float currentFogAlpha01 = 0f;
    bool? lastRevealedState = null;
    const float LightFogAlpha = 150f / 255f;
    const float HeavyFogAlpha = 200f / 255f;
    const float UnrevealedFadeAmount = 1f; // as requested
    const float RevealedFadeAmount = 1f;   // as requested
    
    public void Initialize(TileState tile, bool hex)
    {
        // never changes
        posView = tile.pos;
        gameObject.name = $"Tile {posView}";
        
        
        SetTile(tile, hex);
        initialElevatorLocalPos = tileModel.elevator.localPosition;
        // Ensure fog is disabled on initialization
        SetFogState(FogState.NONE);
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
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
        ViewEventBus.OnSetupHoverChanged -= HandleSetupHoverChanged;
        ViewEventBus.OnMoveHoverChanged -= HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged -= HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged -= HandleMovePairsChanged;
    }

	void HandleClientModeChanged(GameSnapshot snapshot)
    {
        if (enableDebugLogs) Debug.Log($"TileView[{posView}]: ClientModeChanged mode={snapshot.Mode}");
        // Reset base visuals common to any mode switch
		SetArrow(null);
		SetRenderEffect(EffectType.HOVEROUTLINE, false);
		SetRenderEffect(EffectType.SELECTOUTLINE, false);
		SetRenderEffect(EffectType.FILL, false);
		SetElevator(initialElevatorLocalPos.y);
        isSelected = false;
        // Mode-specific initialization
        SetTileDebug();
        TileState tile = snapshot.Net.GetTileUnchecked(posView);
        SetTopColor(Color.clear);
        // When entering modes, seed arrows immediately for Resolve only (Move arrows will be driven by events)
        switch (snapshot.Mode)
        {
			case ClientMode.Setup:
			{
				ApplyTeamColor(tile.setup);
				break;
			}
            case ClientMode.Move:
			{
				break;
			}
            case ClientMode.Resolve:
            {
                // Use resolve payload moves for arrows
                bool start = false;
                bool target = false;
                TileView targetTile = null;
                var tr = snapshot.Ui.ResolveData;
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
    }

    void HandleSetupHoverChanged(Vector2Int hoveredPos, bool isMyTurn)
    {
        if (enableDebugLogs) Debug.Log($"TileView[{posView}]: SetupHover pos={hoveredPos} myTurn={isMyTurn}");
        bool hovered = isMyTurn && posView == hoveredPos;
        SetRenderEffect(EffectType.HOVEROUTLINE, hovered);
    }

    void HandleMoveHoverChanged(Vector2Int hoveredPos, bool isMyTurn, System.Collections.Generic.HashSet<Vector2Int> hoverTargets)
    {
        bool hovered = isMyTurn && posView == hoveredPos;
        SetRenderEffect(EffectType.HOVEROUTLINE, hovered);
        // Elevate lightly only in movement when selection intent
		Vector3 target = isSelected ? selectedElevatorLocalPos : ((hovered && hoverTargets != null && hoverTargets.Count > 0) ? hoveredElevatorLocalPos : initialElevatorLocalPos);
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
		isSelected = selectedNow;
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

    // void SetFogFade(bool isRevealed)
	// {
	// 	Renderer r = tileModel != null ? (tileModel.topRenderer != null ? tileModel.topRenderer : tileModel.flatRenderer) : null;
	// 	if (r == null) return;
	// 	Material m = r.material;
	// 	if (!m.HasProperty(FadeAmountProperty)) return;
	// 	float from = m.GetFloat(FadeAmountProperty);
	// 	float to = isRevealed ? 0f : 1f;
	// 	if (revealFadeTween.isAlive) revealFadeTween.Stop();
	// 	revealFadeTween = PrimeTween.Tween.Custom(from, to, 0.25f, (val) =>
	// 	{
	// 		m.SetFloat(FadeAmountProperty, val);
	// 	}, Ease.OutCubic);
	// }

    void SetFogColor(Color targetColor)
	{
		// Ensure fog object active
        //Debug.Log($"TileView[{posView}]: SetFogColor targetColor={targetColor}");
		if (tileModel != null && tileModel.fogObject != null && !tileModel.fogObject.activeSelf)
		{
			tileModel.fogObject.SetActive(true);
		}
		Renderer fogRenderer = tileModel.fogObject.GetComponent<Renderer>();
		Material fogMat = fogRenderer.material;

		Color baseColor = fogMat.HasProperty(FogColorProperty) ? fogMat.GetColor(FogColorProperty) : fogMat.color;
		Color startColor = baseColor;
		Color endColor = new Color(baseColor.r, baseColor.g, baseColor.b, targetColor.a);
		fogMat.SetColor(FogColorProperty, endColor);
		// if (Mathf.Approximately(startColor.a, endColor.a))
		// {
		// 	if (fogMat.HasProperty(FogColorProperty)) fogMat.SetColor(FogColorProperty, endColor); else fogMat.color = endColor;
		// 	currentFogAlpha01 = endColor.a;
		// 	return;
		// }
		// if (fogAlphaTween.isAlive) fogAlphaTween.Stop();
		// fogAlphaTween = PrimeTween.Tween.Custom(startColor.a, endColor.a, 0.2f, (val) =>
		// {
		// 	Color c = baseColor;
		// 	c.a = val;
		// 	if (fogMat.HasProperty(FogColorProperty)) fogMat.SetColor(FogColorProperty, c); else fogMat.color = c;
		// 	currentFogAlpha01 = val;
		// }, Ease.OutCubic);
	}

    public FogState fogState;

    void SetFogState(FogState state)
    {
        //Debug.Log($"TileView[{posView}]: SetFogState state={state}");
        fogState = state;
        switch (state)
        {
            case FogState.NONE:
                SetFogColor(Color.clear);
                break;
            case FogState.LIGHT:
                SetFogColor(new Color(0f, 0f, 0f, LightFogAlpha));
                break;
            case FogState.HEAVY:
                SetFogColor(new Color(0f, 0f, 0f, HeavyFogAlpha));
                break;
        }
    }

    public PawnState? pawnFogOwner;
    // Single public entrypoint for fog updates driven by pawn state
    public void UpdateFogFromPawnState(PawnState pawn)
    {
        //Debug.Log($"TileView[{posView}]: UpdateFogFromPawnState pawn={pawn}");
        pawnFogOwner = pawn;
        FogState state = FogState.NONE;
        if (!pawn.zz_revealed)
        {
            state = FogState.HEAVY;
            if (pawn.moved)
            {
                state = FogState.LIGHT;
            }
        }
        SetFogState(state);
    }

    public void ClearFog(PawnState pawn)
    {
        if (pawnFogOwner == pawn)
        {
            pawnFogOwner = null;
            SetFogState(FogState.NONE);
        }
        else
        {
            Debug.LogError($"TileView[{posView}]: ClearFog failed to clear fog for pawn={pawn}");
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

	void ApplyTeamColor(Team team)
	{
		Color c = Color.clear;
		switch (team)
		{
			case Team.RED:
				c = redTeamColor;
				break;
			case Team.BLUE:
				c = blueTeamColor;
				break;
		}
		SetTopColor(c);
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

public enum FogState
{
    NONE,
    LIGHT,
    HEAVY,
}