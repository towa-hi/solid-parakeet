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

    // cached
    public Rank rankView;
    public bool aliveView;
    public Vector2Int posView;

    public bool cheatMode;
    public static event Action<PawnId> OnMoveAnimationCompleted;
    ClientMode currentMode;

    public void TestSetSprite(Rank testRank, Team testTeam)
    {
        team = testTeam;
        rankView = testRank;
        DisplayRankView(testRank);
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
        bool setAnimatorIsSelected = selectedPos.HasValue && selectedPos.Value == posView;
        if (animator.GetBool(animatorIsSelected) != setAnimatorIsSelected)
        {
            animator.SetBool(animatorIsSelected, setAnimatorIsSelected);
        }
    }

    void HandleMovePairsChanged(Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs)
    {
        bool setAnimatorIsSelected = newPairs.ContainsKey(pawnId);
        if (animator.GetBool(animatorIsSelected) != setAnimatorIsSelected)
        {
            animator.SetBool(animatorIsSelected, setAnimatorIsSelected);
        }
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
        rankView = Rank.UNKNOWN;
        aliveView = pawn.alive;
        posView = Vector2Int.zero;
        DisplayPosView(tileView);
        DisplayRankView(Rank.UNKNOWN);

    }
    // Removed: Setup mode event is redundant with client mode event

    void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        currentMode = mode;
        animator.SetBool(animatorIsSelected, false);

		// Helper: re-seed from authoritative network snapshot
		void ReseedFromNet()
		{
			try
			{
				PawnState p = net.GetPawnFromId(pawnId);
				aliveView = p.alive;
				if (posView != p.pos)
				{
					TileView tv2 = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(p.pos) : null;
					if (tv2 != null) { DisplayPosView(tv2); }
					posView = p.pos;
				}
				Rank known2 = p.GetKnownRank(net.userTeam) ?? Rank.UNKNOWN;
				if (known2 != rankView) { DisplayRankView(known2); rankView = known2; }
			}
			catch (Exception) { }
		}

		switch (mode)
		{
			case ClientMode.Resolve:
				
				break;
			case ClientMode.Setup:
				ReseedFromNet();
                {
                    bool shouldBeVisible = rankView != Rank.UNKNOWN && aliveView;
                    model.SetActive(shouldBeVisible);
                }
				break;
			case ClientMode.Move:
			case ClientMode.Finished:
			case ClientMode.Aborted:
                ReseedFromNet();
                model.SetActive(aliveView);
				break;
			default:
                ReseedFromNet();
                model.SetActive(aliveView);
				break;
		}
    }

    void HandleResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        // checkpoint and battleIndex are consumed immediately; no persistent fields needed
        // Apply pawn snapshot based on checkpoint, mirroring TileView's snapshot-first fallback logic
        if (!tr.pawnDeltas.TryGetValue(pawnId, out SnapshotPawnDelta delta))
        {
            return;
        }
		Rank GetRankFromSnapshot(PawnState[] snapshot)
		{
			// Always show our own pawn's rank based on local knowledge, not snapshot
			Team myTeam = pawnId.Decode().Item2;
			if (myTeam == net.userTeam)
			{
				PawnState current = net.GetPawnFromId(pawnId);
				return current.GetKnownRank(net.userTeam) ?? Rank.UNKNOWN;
			}
			// For opponent pawns, use snapshot reveal state
			if (snapshot == null) return Rank.UNKNOWN;
			for (int i = 0; i < snapshot.Length; i++)
			{
				if (snapshot[i].pawn_id == pawnId)
				{
					PawnState p = snapshot[i];
					return (p.zz_revealed && p.rank.HasValue) ? p.rank.Value : Rank.UNKNOWN;
				}
			}
			return Rank.UNKNOWN;
		}

        switch (checkpoint)
        {
            case ResolveCheckpoint.Pre:
                for (int i = 0; i < tr.preSnapshot.Length; i++)
                {
                    if (tr.preSnapshot[i].pawn_id == pawnId)
                    {
                        aliveView = tr.preSnapshot[i].alive;
                        model.SetActive(aliveView);
                        break;
                    }
                }
                posView = delta.prePos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                {
                    
                    Rank displayRank = GetRankFromSnapshot(tr.preSnapshot);
                    DisplayRankView(displayRank);
                    rankView = displayRank;
                }
                break;
            case ResolveCheckpoint.PostMoves:
                for (int i = 0; i < tr.postMovesSnapshot.Length; i++)
                {
                    if (tr.postMovesSnapshot[i].pawn_id == pawnId)
                    {
                        aliveView = tr.postMovesSnapshot[i].alive;
                        model.SetActive(aliveView);
                        break;
                    }
                }
                posView = delta.prePos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                {
                    Rank displayRank = GetRankFromSnapshot(tr.postMovesSnapshot);
                    DisplayRankView(displayRank);
                    rankView = displayRank;
                }
                // Animate arc if this pawn has a move
                if (tr.moves.TryGetValue(pawnId, out MoveEvent mv))
                {
                    TileView from = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(mv.from) : null;
                    TileView to = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(mv.target) : null;
                    if (from != null && to != null) SetArcToTile(from, to);
                }
                break;
            case ResolveCheckpoint.Battle:
                // During battle sequence we may show postPos after certain battles
                posView = delta.postPos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                {
                    PawnState[] snap = tr.battleSnapshots[Mathf.Clamp(battleIndex, 0, tr.battleSnapshots.Length - 1)];
                    for (int i = 0; i < snap.Length; i++)
                    {
                        if (snap[i].pawn_id == pawnId)
                        {
                            aliveView = snap[i].alive;
                            model.SetActive(aliveView);
                            break;
                        }
                    }
                }
                {
                    PawnState[] snap = tr.battleSnapshots[Mathf.Clamp(battleIndex, 0, tr.battleSnapshots.Length - 1)];
                    Rank displayRank = GetRankFromSnapshot(snap);
                    DisplayRankView(displayRank);
                    rankView = displayRank;
                }
                break;
            case ResolveCheckpoint.Final:
                for (int i = 0; i < tr.finalSnapshot.Length; i++)
                {
                    if (tr.finalSnapshot[i].pawn_id == pawnId)
                    {
                        aliveView = tr.finalSnapshot[i].alive;
                        model.SetActive(aliveView);
                        break;
                    }
                }
                posView = delta.postPos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                {
                    Rank displayRank = GetRankFromSnapshot(tr.finalSnapshot);
                    DisplayRankView(displayRank);
                    rankView = displayRank;
                }
                break;
        }
    }

    void HandleSetupPendingChanged(Dictionary<PawnId, Rank?> oldMap, Dictionary<PawnId, Rank?> newMap)
    {
        // Determine committed rank (if any) for this pawn
        Rank? maybeRank;
        bool hasEntry = newMap.TryGetValue(pawnId, out maybeRank);
        Rank displayRank = hasEntry && maybeRank is Rank r ? r : Rank.UNKNOWN;
        if (displayRank != rankView)
        {
            DisplayRankView(displayRank);
            rankView = displayRank;
        }
        // In setup mode, unknowns should not render at all
        if (currentMode == ClientMode.Setup)
        {
            bool shouldBeVisible = displayRank != Rank.UNKNOWN && aliveView;
            model.SetActive(shouldBeVisible);
        }
    }

    void DisplayPosView(TileView tileView = null)
    {
        StopAllCoroutines();
        SetConstraintToTile(tileView);
        if (tileView)
        {
            Transform target = tileView.tileModel.tileOrigin;
            transform.position = target.position;
            transform.rotation = target.rotation;
        }
    }

    void DisplayRankView(Rank rank)
    {
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(rank);
        animator.runtimeAnimatorController = team switch
        {
            Team.RED => pawnDef.redAnimatorOverrideController,
            Team.BLUE => pawnDef.blueAnimatorOverrideController,
            _ => throw new ArgumentOutOfRangeException(),
        };
        float randNormTime = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randNormTime);
        animator.Update(0f);
        
        //Debug.Log($"{gameObject.name} rank badge set to {rank}");
        badge.SetBadge(team, rank);
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


    // Deprecated: use StopAllCoroutines() to restart animations atomically
    // Kept for potential future diagnostics
    // bool isMoving = false;

    public IEnumerator ArcToPosition(Transform target, float duration, float arcHeight)
    {
        parentConstraint.constraintActive = false;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

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

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        // Leave the constraint disabled here; caller will rebind to the destination tile
    }

    void SetArcToTile([CanBeNull] TileView initialTile, TileView targetTile)
    {
        StopAllCoroutines();
        // Ensure we start from the initial tile anchor
        if (initialTile)
        {
            parentConstraint.constraintActive = false;
            transform.position = initialTile.origin.position;
            transform.rotation = initialTile.origin.rotation;
        }
        StartCoroutine(AnimateAndNotify(targetTile));
    }

    IEnumerator AnimateAndNotify(TileView targetTile)
    {
        // Animate toward the tile origin so re-binding won't cause a pop
        yield return ArcToPosition(targetTile.origin, Globals.PawnMoveDuration, 0.5f);
        // Rebind the constraint to the target tile and re-enable it so the pawn stays put
        SetConstraintToTile(targetTile);
        OnMoveAnimationCompleted?.Invoke(pawnId);
    }

    public void PublicSetArcToTile([CanBeNull] TileView initialTile, TileView targetTile)
    {
        SetArcToTile(initialTile, targetTile);
    }
}
