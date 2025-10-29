using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Contract;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
using PrimeTween;
using Random = UnityEngine.Random;

public class PawnView : MonoBehaviour
{
    static readonly int animatorIsSelected = Animator.StringToHash("IsSelected");
    static readonly int Hurt = Animator.StringToHash("Hurt");
    Billboard billboard;

    public Badge badge;
    public GameObject model;
    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;

    public Animator animator;
    public RenderEffect renderEffect;
    // immutable
    public PawnId pawnId;
    public Team team;

    // cached values strictly for checking redundant setting
    public PawnDef cachedPawnDef;

    public static event Action<PawnId> OnMoveAnimationCompleted;
    
    

    public void TestSetSprite(Rank testRank, Team testTeam)
    {
        // fix this later
        // team = testTeam;
        // rankView = testRank;
        // DisplayRankView(testRank);
    }

    // Subscriptions are managed by board lifecycle; avoid toggling on enable/disable
    public void AttachSubscriptions()
    {
        ViewEventBus.OnSetupPendingChanged += HandleSetupPendingChanged;
        ViewEventBus.OnClientModeChanged += HandleClientModeChanged;
        ViewEventBus.OnMoveSelectionChanged += HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged += HandleMovePairsChanged;
        ViewEventBus.OnResolveCheckpointChanged += HandleResolveCheckpointChanged;
    }

    public void DetachSubscriptions()
    {
        StopAllCoroutines();
        ViewEventBus.OnSetupPendingChanged -= HandleSetupPendingChanged;
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
        ViewEventBus.OnMoveSelectionChanged -= HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged -= HandleMovePairsChanged;
        ViewEventBus.OnResolveCheckpointChanged -= HandleResolveCheckpointChanged;
    }
    public void TestSpriteSelectTransition(bool newAnimationState)
    {
        animator.SetBool(animatorIsSelected, newAnimationState);
    }

    void HandleMoveSelectionChanged(Vector2Int? selectedPos, HashSet<Vector2Int> validTargets)
    {
        if (selectedPos.HasValue)
        {
            TileView tv = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(selectedPos.Value) : null;
            bool isSelectedNow = IsBoundToTile(tv);
            SetAnimatorIsSelected(isSelectedNow);
        }
        else
        {
            SetAnimatorIsSelected(false);
        }
    }

    void HandleMovePairsChanged(Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs)
    {
        bool isInvolved = newPairs.ContainsKey(pawnId);
        SetAnimatorIsSelected(isInvolved);
    }

    public void HurtAnimation()
    {
        animator.SetBool(Hurt, true);
    }
    public void ResetHurtAnimation()
    {
        animator.SetBool(Hurt, false);
    }
    
    public void FadeOut()
    {
        // lerp _FadeAmount from 0 to 1 over 0.5 seconds
        PawnSprite pawnSprite = GetComponentInChildren<PawnSprite>();
        Material material = pawnSprite.GetComponent<MeshRenderer>().material;
        PrimeTween.Tween.Custom(0f, 1f, 1f, (float value) =>
        {
            material.SetFloat("_FadeAmount", value);
        }, PrimeTween.Ease.OutQuad);
    }

    public void ResetShaderProperties()
    {
        PawnSprite pawnSprite = GetComponentInChildren<PawnSprite>(true);
        if (pawnSprite == null) return;
        MeshRenderer meshRenderer = pawnSprite.GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;
        Material material = meshRenderer.material;
        if (material == null) return;
        
        // Stop any active fade animations on this material
        Tween.StopAll(material);
        
        if (material.HasProperty("_FadeAmount"))
        {
            material.SetFloat("_FadeAmount", 0f);
        }
    }

    public void ResetAnimatedValues()
    {
        animator.Rebind();
        SetAnimatorIsSelected(false);
        ResetShaderProperties();
        ResetSpriteStates();
    }
    
    public void ResetSpriteStates()
    {
        // Reset all PawnSprite components (including ShadeSprite)
        // Clear the 'last' sprite cache so that when animator sets currentSprite, it will update
        PawnSprite[] pawnSprites = GetComponentsInChildren<PawnSprite>(true);
        foreach (PawnSprite ps in pawnSprites)
        {
            if (ps != null)
            {
                // Clear last so sprite will update when animator sets currentSprite
                ps.last = null;
            }
        }
        
        // Find and reset ShadeSprite GameObject active state (should be inactive at start of battle)
        // The reveal animations will turn it on/off as needed
        Transform shadeSpriteTransform = transform.Find("Model/Billboard/ShadeSprite");
        if (shadeSpriteTransform != null)
        {
            shadeSpriteTransform.gameObject.SetActive(false);
        }
    }
    
    public void EnsureModelVisible()
    {
        if (model != null && !model.activeSelf)
        {
            model.SetActive(true);
        }
    }
    
    public void StartIdleAnimation()
    {
        if (animator != null && animator.enabled)
        {
            SetAnimatorIdleRandomized();
        }
    }

    public void Initialize(PawnState pawn, TileView tileView)
    {
        // never changes
        pawnId = pawn.pawn_id;
        team = pawnId.Decode().Item2;
        gameObject.name = $"Pawn {pawnId} team {pawn.GetTeam()} startPos {pawn.GetStartPosition()}";
        SetPosSnap(tileView, pawn);
        SetRank(Rank.UNKNOWN);
    }

    void HandleClientModeChanged(GameSnapshot snapshot)
    {
        SetAnimatorIsSelected(false);

        PawnState p = snapshot.Net.GetPawnFromId(pawnId);
        Rank known = p.GetKnownRank(snapshot.Net.userTeam) ?? Rank.UNKNOWN;
		switch (snapshot.Mode)
		{
            case ClientMode.Setup:
			{
				SetRank(known);
				bool shouldBeVisible = known != Rank.UNKNOWN;
                // Don't touch fog in Setup; leave tiles clear
                SetModelVisible(shouldBeVisible);
				break;
			}
			case ClientMode.Resolve:
			{
				// Initialize position and fog from current network state when entering Resolve mode
				// This preserves fog until HandleResolveCheckpointChanged updates it with Pre checkpoint state
				SetRank(known);
				SetModelVisible(p.alive);
				TileView currentTile = ViewEventBus.TileViewResolver(p.pos);
				SetPosSnap(currentTile, p);
				break;
			}
			case ClientMode.Move:
			case ClientMode.Finished:
			case ClientMode.Aborted:
			default:
			{
				SetRank(known);
                SetModelVisible(p.alive);
			// Snap to the authoritative position from the store when entering non-Resolve modes
			TileView finalTile = ViewEventBus.TileViewResolver(p.pos);
			SetPosSnap(finalTile, p);
				break;
			}
		}
    }

    void HandleResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        //Debug.Log($"[PawnView] Begin Resolve checkpoint={checkpoint} idx={battleIndex} pawn={pawnId}");
        bool isMyTeam = team == net.userTeam;
        PawnState current = net.GetPawnFromId(pawnId);
        tr.pawnDeltas.TryGetValue(pawnId, out SnapshotPawnDelta delta);
        Rank GetRankFromSnapshot(PawnState state)
        {
            if (isMyTeam)
            {
                return current.GetKnownRank(net.userTeam).Value;
            }
            else
            {
                if (state.zz_revealed)
                {
                    return state.rank.Value;
                }
                else
                {
                    return Rank.UNKNOWN;
                }
            }
        }
        // We'll resolve tiles from the snapshot states directly per checkpoint
        switch (checkpoint)
        {
            case ResolveCheckpoint.Pre:
            {
                PawnState preState = tr.preSnapshot.First(p => p.pawn_id == pawnId);
                // Only set model visibility; fog handled in position setters
                SetModelVisible(preState.alive);
                SetRank(GetRankFromSnapshot(preState));
                // Bind to pre tile and set fog strictly from pre snapshot
                TileView preTile = ViewEventBus.TileViewResolver(preState.pos);
                SetPosSnap(preTile, preState);
                break;
            }
            case ResolveCheckpoint.PostMoves:
            {
                PawnState preState = tr.preSnapshot.First(p => p.pawn_id == pawnId);
                
                PawnState postMovesState = tr.postMovesSnapshot.First(p => p.pawn_id == pawnId);

                SetModelVisible(postMovesState.alive);
                SetRank(GetRankFromSnapshot(postMovesState));
                if (tr.moves != null && tr.moves.TryGetValue(pawnId, out MoveEvent mv) && mv.from != mv.target)
                {
                    TileView targetTile = ViewEventBus.TileViewResolver(mv.target);
                    SetPosArc(targetTile, postMovesState);
                }
                else {
                    TileView sameTile = ViewEventBus.TileViewResolver(postMovesState.pos);
                    SetPosSnap(sameTile, postMovesState);
                }
                break;
            }
            case ResolveCheckpoint.Battle:
            {
                PawnState battleState = tr.battleSnapshots[battleIndex].First(p => p.pawn_id == pawnId);
                SetModelVisible(battleState.alive);
                SetRank(GetRankFromSnapshot(battleState));
                TileView battleTile = ViewEventBus.TileViewResolver(battleState.pos);
                SetPosSnap(battleTile, battleState);
                break;
            }
            case ResolveCheckpoint.Final:
            default:
            {
                SetModelVisible(current.alive);
                SetRank(current.GetKnownRank(net.userTeam) ?? Rank.UNKNOWN);
                TileView finalTile = ViewEventBus.TileViewResolver(current.pos);
                SetPosSnap(finalTile, current);
                break;
            }
        }
        //Debug.Log($"[PawnView] End Resolve checkpoint={checkpoint} idx={battleIndex} pawn={pawnId}");
    }

    void HandleSetupPendingChanged(Dictionary<PawnId, Rank?> oldMap, Dictionary<PawnId, Rank?> newMap)
    {
        // Determine committed rank (if any) for this pawn
        Rank? maybeRank;
        bool hasEntry = newMap.TryGetValue(pawnId, out maybeRank);
        Rank displayRank = hasEntry && maybeRank is Rank r ? r : Rank.UNKNOWN;
        SetRank(displayRank);
        // In setup mode, unknowns should not render at all (event is setup-specific)
        bool shouldBeVisible = displayRank != Rank.UNKNOWN;
        SetModelVisible(shouldBeVisible);
    }

    void SetConstraintToTile([CanBeNull] TileView tileView)
    {
        Transform source = GameManager.instance.purgatory;
        if (tileView)
        {
            source = tileView.tileModel.tileOrigin;
        }
        parentConstraint.constraintActive = false;
        for (int i = parentConstraint.sourceCount - 1; i >= 0; i--)
        {
            parentConstraint.RemoveSource(i);
        }
        ConstraintSource cs = new ()
        {
            sourceTransform = source,
            weight = 1f,
        };
        parentConstraint.AddSource(cs);
        parentConstraint.SetTranslationOffset(0, Vector3.zero);
        parentConstraint.SetRotationOffset(0, Vector3.zero);
        parentConstraint.weight = 1f;
        parentConstraint.constraintActive = true;
    }

    // ===== Tier 1: Direct view mutators / setters =====

    void SetModelVisible(bool visible)
    {
		if (model.activeSelf != visible)
        {
            model.SetActive(visible);
        }
    }

    void SetModelVisible(bool visible, PawnState snapshot)
    {
        // Deprecated fog update path: only keep visibility change here
        SetModelVisible(visible);
    }

    void SetAnimatorIsSelected(bool selected)
    {
		if (animator.GetBool(animatorIsSelected) != selected)
        {
            animator.SetBool(animatorIsSelected, selected);
        }
    }

	void SetAnimatorIdleRandomized()
    {
        float randNormTime = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randNormTime);
        animator.Update(0f);
    }

    void SetTransformToTile([CanBeNull] TileView tileView)
    {
        if (!tileView) return;
        Transform target = tileView.tileModel.tileOrigin;
        transform.position = target.position;
        transform.rotation = target.rotation;
    }

    void SetRenderEffect(EffectType effect, bool enabled)
    {
        renderEffect.SetEffect(effect, enabled);
    }

    bool IsBoundToTile([CanBeNull] TileView tileView)
    {
        if (!tileView) return false;
        if (parentConstraint.sourceCount == 0) return false;
        ConstraintSource cs = parentConstraint.GetSource(0);
        return cs.sourceTransform == tileView.tileModel.tileOrigin;
    }

    [CanBeNull]
    TileView GetBoundTileView()
    {
        if (parentConstraint.sourceCount == 0) return null;
        
        ConstraintSource cs = parentConstraint.GetSource(0);
        Transform bound = cs.sourceTransform;
        if (!bound) return null;
        if (bound == GameManager.instance.purgatory) return null;
        TileView tv = bound.GetComponentInParent<TileView>();
        //Debug.Log($"PawnView[{pawnId}]: GetBoundTileView bound={bound} tv={tv.posView}");
        return tv;
    }

    void SetRank(Rank rank)
    {
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(rank);
        if (pawnDef == cachedPawnDef)
        {
            return;
        }
        cachedPawnDef = pawnDef;
        //Debug.Log($"SetRank: {gameObject.name} {rank}");
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = info.fullPathHash;
        float norm = info.normalizedTime - Mathf.Floor(info.normalizedTime);
        RuntimeAnimatorController controller = team switch
        {
            Team.RED => pawnDef.redAnimatorOverrideController,
            Team.BLUE => pawnDef.blueAnimatorOverrideController,
            _ => null,
        };
        animator.runtimeAnimatorController = controller;
        bool restored = false;
        try
        {
            animator.Play(stateHash, 0, norm);
            restored = true;
        }
        catch (Exception)
        {
        }
        animator.Update(0f);
        if (!restored)
        {
            SetAnimatorIdleRandomized();
        }
        TileView bound = GetBoundTileView();
        if (bound)
        {
            // Tooltip is driven by TileView from GameSnapshot; no direct calls here
        }
        badge.SetBadge(team, rank);
    }

    void SetPosSnap(TileView targetTile, PawnState snapshot)
    {
        StopAllCoroutines();
        TileView initial = GetBoundTileView();
        if (initial)
        {
            if (initial != targetTile)
            {
                initial.ClearFogImmediate();
            }
            initial.ClearTooltip();
        }
        if (!snapshot.alive)
        {
            //Debug.Log($"PawnView[{pawnId}]: SetPosSnap snapshot={snapshot} not alive, setting constraint to null");
            SetConstraintToTile(null);
            SetTransformToTile(null);
            return;
        }
        SetConstraintToTile(targetTile);
        SetTransformToTile(targetTile);
        // Apply fog exactly once based on snapshot
        ApplyFogForSnapshot(targetTile, snapshot);
    }

    void SetPosArc([CanBeNull] TileView targetTile, PawnState snapshot)
    {
        if (!snapshot.alive)
        {
            Debug.LogWarning($"PawnView[{pawnId}]: SetPosArc snapshot={snapshot} not alive, skipping");
            return;
        }
        StopAllCoroutines();
        TileView initial = GetBoundTileView();
        if (initial)
        {
            parentConstraint.constraintActive = false;
            transform.position = initial.origin.position;
            transform.rotation = initial.origin.rotation;
            // During arc: ensure no fog on initial
            initial.ClearFogImmediate();
            initial.ClearTooltip();
        }
        StartCoroutine(ArcToTileNoNotify(targetTile, snapshot));
    }

    IEnumerator ArcToTileNoNotify(TileView targetTile, PawnState snapshot)
    {
        yield return ArcToPosition(targetTile.origin, Globals.PawnMoveDuration, 0.5f);
        SetConstraintToTile(targetTile);
        SetTransformToTile(targetTile);
        // Apply fog exactly once on arrival
        ApplyFogForSnapshot(targetTile, snapshot);
    }

    // Single fog application per frame/checkpoint driven from PawnView
    void ApplyFogForSnapshot(TileView tile, PawnState snapshot)
    {
        // revealed -> no fog; unrevealed -> heavy; if moved and unrevealed -> light
        const float LightFogAlpha = 150f / 255f;
        const float HeavyFogAlpha = 200f / 255f;
        float alpha = 0f;
        if (!snapshot.zz_revealed)
        {
            alpha = snapshot.moved ? LightFogAlpha : HeavyFogAlpha;
        }
        tile.SetFogAlphaImmediate(alpha);
    }

    public IEnumerator ArcToPosition(Transform target, float duration, float arcHeight)
    {
        parentConstraint.constraintActive = false;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        bool playedLandClip = false;
        while (elapsedTime < duration)
        {
            // Calculate the normalized time (0 to 1)
            float t = elapsedTime / duration;
            t = Shared.EaseOutQuad(t);

            // Interpolate position horizontally
            Vector3 horizontalPosition = Vector3.Lerp(startPosition, target.position, t);

            // Calculate vertical arc using a parabolic equation
            float verticalOffset = arcHeight * (1 - Mathf.Pow(2 * t - 1, 2)); // Parabolic equation: a(1 - (2t - 1)^2)
            horizontalPosition.y += verticalOffset;

            // Apply the calculated position
            transform.position = horizontalPosition;
            if (!playedLandClip && t > 0.9f)
            {
                AudioManager.PlayOneShot(ResourceRoot.Instance.pawnLandClip);
                playedLandClip = true;
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        // Leave the constraint disabled here; caller will rebind to the destination tile
    }

    public void PublicSetArcToTile([CanBeNull] TileView initialTile, TileView targetTile)
    {
        //SetArcToTile(initialTile, targetTile);
    }
}
