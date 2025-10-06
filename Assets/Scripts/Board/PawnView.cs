using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Contract;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
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
        animator.SetTrigger(Hurt);
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

    void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        SetAnimatorIsSelected(false);

        PawnState p = net.GetPawnFromId(pawnId);
        Rank known = p.GetKnownRank(net.userTeam) ?? Rank.UNKNOWN;
		switch (mode)
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
				break;
			}
			case ClientMode.Move:
			case ClientMode.Finished:
			case ClientMode.Aborted:
			default:
			{
				SetRank(known);
                Debug.Log($"PawnView[{pawnId}]: HandleClientModeChanged mode={mode} alive={p.alive} known={known} Setting Model Visible from client mode");
                SetModelVisible(p.alive);
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
                Debug.Log($"PawnView[{pawnId}]: HandleResolveCheckpointChanged checkpoint={checkpoint} idx={battleIndex} Setting Model Visible from pre checkpoint");
                SetModelVisible(preState.alive, preState);
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
                
                if (pawnId == 80)
                {
                    Debug.Log("hi mom");
                    Debug.Log($"pre: {preState} post: {postMovesState} current: {current}");
                }
                Debug.Log($"PawnView[{pawnId}]: HandleResolveCheckpointChanged checkpoint={checkpoint} idx={battleIndex} Setting Model Visible from post moves checkpoint");
                SetModelVisible(postMovesState.alive, postMovesState);
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
                Debug.Log($"PawnView[{pawnId}]: HandleResolveCheckpointChanged checkpoint={checkpoint} idx={battleIndex} Setting Model Visible from battle checkpoint");
                SetModelVisible(battleState.alive, battleState);
                SetRank(GetRankFromSnapshot(battleState));
                TileView battleTile = ViewEventBus.TileViewResolver(battleState.pos);
                SetPosSnap(battleTile, battleState);
                break;
            }
            case ResolveCheckpoint.Final:
            default:
            {
                Debug.Log($"PawnView[{pawnId}]: HandleResolveCheckpointChanged checkpoint={checkpoint} idx={battleIndex} Setting Model Visible from final checkpoint");
                SetModelVisible(current.alive, current);
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
        SetModelVisible(visible);
        TileView bound = GetBoundTileView();
        if (bound)
        {
            Debug.Log($"PawnView[{pawnId}]: SetModelVisible visible={visible} snapshot={snapshot} bound posView={bound.posView}");
            bound.UpdateFogFromPawnState(snapshot);
        }
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
        Debug.Log($"PawnView[{pawnId}]: GetBoundTileView bound={bound} tv={tv.posView}");
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
        Debug.Log($"SetRank: {gameObject.name} {rank}");
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

        badge.SetBadge(team, rank);
    }

    void SetPosSnap(TileView targetTile, PawnState snapshot)
    {
        StopAllCoroutines();
        TileView initial = GetBoundTileView();
        if (initial)
        {
            initial.ClearFog(snapshot);
        }
        if (!snapshot.alive)
        {
            Debug.Log($"PawnView[{pawnId}]: SetPosSnap snapshot={snapshot} not alive, setting constraint to null");
            SetConstraintToTile(null);
            SetTransformToTile(null);
            return;
        }
        SetConstraintToTile(targetTile);
        SetTransformToTile(targetTile);
        // Drive fog on the bound tile based on provided snapshot
        Debug.Log($"PawnView[{pawnId}]: SetPosSnap targetTile={targetTile.posView} snapshot={snapshot}");
        targetTile.UpdateFogFromPawnState(snapshot);
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
            initial.ClearFog(snapshot);
        }
        StartCoroutine(ArcToTileNoNotify(targetTile, snapshot));
    }

    IEnumerator ArcToTileNoNotify(TileView targetTile, PawnState snapshot)
    {
        yield return ArcToPosition(targetTile.origin, Globals.PawnMoveDuration, 0.5f);
        SetConstraintToTile(targetTile);
        SetTransformToTile(targetTile);
        Debug.Log($"PawnView[{pawnId}]: SetPosArc targetTile={targetTile.posView} snapshot={snapshot}");
        targetTile.UpdateFogFromPawnState(snapshot);
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
