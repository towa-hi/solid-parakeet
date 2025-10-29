using System;
using Contract;
using UnityEngine;
using PrimeTween;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using System.Collections.Generic;

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
    static readonly int FadeAmountProperty = Shader.PropertyToID("_FadeAmount");
    public TweenSettings<Vector3> startTweenSettings;
    public Sequence startSequence;
    public Tween currentTween;
    Vector3 initialElevatorLocalPos;
    
    // Track whether this tile is currently selected in Move mode
    bool isSelected;

    static Vector3 hoveredElevatorLocalPos = new Vector3(0, Globals.HoveredHeight, 0);
    static Vector3 selectedElevatorLocalPos = new Vector3(0, Globals.SelectedHoveredHeight, 0);
    
    public void Initialize(TileState tile, bool hex)
    {
        // never changes
        posView = tile.pos;
        gameObject.name = $"Tile {posView}";
        
        
        SetTile(tile, hex);
        initialElevatorLocalPos = tileModel.elevator.localPosition;
        if (tileModel != null && tileModel.fogObject != null)
        {
            tileModel.fogObject.SetActive(false);
        }
        if (tileModel != null && tileModel.tooltipElement != null)
        {
            tileModel.tooltipElement.SetTilePosition(posView);
        }
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
				// Seed arrows/highlights when re-entering during MoveCommit and it's not our subphase
				// so the player can see their already-submitted moves while waiting
				bool isMyTurn = snapshot.Net.IsMySubphase();
				if (!isMyTurn && snapshot.Net.lobbyInfo.phase == Phase.MoveCommit)
				{
					var pairs = snapshot.Net.GetUserMove().GetSubmittedMovePairs();
					if (pairs.Count > 0)
					{
						bool start = false;
						bool target = false;
						TileView targetTile = null;
						Team? team = null;
						foreach (var p in pairs)
						{
							if (p.start == posView)
							{
								start = true;
								if (ViewEventBus.TileViewResolver != null)
								{
									targetTile = ViewEventBus.TileViewResolver(p.target);
								}
								// Get team from pawn on this tile
								var pawn = snapshot.Net.GetAlivePawnFromPosChecked(posView);
								if (pawn.HasValue)
								{
									team = pawn.Value.GetTeam();
								}
							}
							if (p.target == posView)
							{
								target = true;
							}
						}
						SetArrow((start && targetTile != null) ? targetTile : null, team);
						Color col = Color.clear;
						if (start) col = Color.green * 0.5f;
						if (target) col = Color.blue * 0.5f;
						SetTopColor(col);
					}
				}
				break;
			}
            case ClientMode.Resolve:
            {
                // Use resolve payload moves for arrows
                bool start = false;
                bool target = false;
                TileView targetTile = null;
                Team? team = null;
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
                            // Get team from PawnId
                            team = kv.Key.GetTeam();
                        }
                        if (mv.target == posView) target = true;
                    }
                }
                SetArrow((start && targetTile != null) ? targetTile : null, team);
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
        Team? team = null;
        foreach (var kv in newPairs)
        {
            if (kv.Value.start == posView) 
            { 
                isStart = true;
                team = kv.Key.GetTeam();
            }
            if (kv.Value.target == posView) { isTarget = true; }
            if (kv.Value.start == posView)
            {
                targetTile = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(kv.Value.target) : null;
            }
        }
        SetArrow((isStart && targetTile != null) ? targetTile : null, team);
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

	void SetArrow(TileView target, Team? team = null)
	{
		if (arrow == null) return;
		if (target != null)
		{
			// Set arrow color based on team (always update color if team is provided)
			if (team.HasValue)
			{
				Color arrowColor = Color.white;
				switch (team.Value)
				{
					case Team.RED:
						arrowColor = redTeamColor;
						break;
					case Team.BLUE:
						arrowColor = blueTeamColor;
						break;
				}
				arrow.SetColor(arrowColor);
			}
			
			// Skip re-animating if we're already pointing to the same tile
			if (pointedTile != null && (pointedTile == target || pointedTile.posView == target.posView))
			{
				return;
			}
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