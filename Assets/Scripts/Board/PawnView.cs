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
    public Vector2Int startPos;
    public Team team;

    // cached
    public Rank rankView;
    public bool aliveView;
    public Vector2Int posView;
    public bool isMyTeam;
    public bool visibleView;
    public bool isSelected;
    public bool isMovePairStart;

    public bool cheatMode;
    public static event Action<PawnId> OnMoveAnimationCompleted;
    ClientMode currentMode;
    ResolveCheckpoint currentResolveCheckpoint = ResolveCheckpoint.Pre;
    int currentBattleIndex = -1;

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
        bool newIsSelected = selectedPos.HasValue && selectedPos.Value == posView;
        if (newIsSelected != isSelected)
        {
            isSelected = newIsSelected;
            bool setAnimatorIsSelected = isSelected || isMovePairStart;
            if (animator.GetBool(animatorIsSelected) != setAnimatorIsSelected)
            {
                animator.SetBool(animatorIsSelected, setAnimatorIsSelected);
            }
        }
    }

    void HandleMovePairsChanged(Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs)
    {
        bool newIsStart = newPairs.ContainsKey(pawnId);
        if (newIsStart != isMovePairStart)
        {
            isMovePairStart = newIsStart;
            bool setAnimatorIsSelected = isSelected || isMovePairStart;
            if (animator.GetBool(animatorIsSelected) != setAnimatorIsSelected)
            {
                animator.SetBool(animatorIsSelected, setAnimatorIsSelected);
            }
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
        startPos = pawnId.Decode().Item1;
        team = pawnId.Decode().Item2;
        gameObject.name = $"Pawn {pawnId} team {pawn.GetTeam()} startPos {pawn.GetStartPosition()}";
        rankView = Rank.UNKNOWN;
        aliveView = pawn.alive;
        posView = Vector2Int.zero;
        visibleView = false;
        isSelected = false;
        isMovePairStart = false;
        DisplayPosView(tileView);
        DisplayRankView(Rank.UNKNOWN);

    }
    // Removed: Setup mode event is redundant with client mode event

    void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
		currentMode = mode;
		// Reset resolve checkpoint tracking
		currentResolveCheckpoint = ui.Checkpoint;
		currentBattleIndex = ui.BattleIndex;
		// Reset per-mode visuals
		isSelected = false;
		isMovePairStart = false;
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
				// If resolve payload is present, apply Pre snapshot immediately so pawns are in expected pre state
				if (ui.ResolveData.moves != null && ui.ResolveData.pawnDeltas.TryGetValue(pawnId, out SnapshotPawnDelta delta))
				{
					aliveView = delta.preAlive;
					posView = delta.prePos;
					TileView tv = ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null;
					if (tv != null) { DisplayPosView(tv); }
					Rank known = net.GetPawnFromId(pawnId).GetKnownRank(net.userTeam) ?? Rank.UNKNOWN;
					Rank rv = known == Rank.UNKNOWN ? (delta.preRevealed ? delta.preRank : Rank.UNKNOWN) : known;
					if (rv != rankView) { DisplayRankView(rv); rankView = rv; }
					model.SetActive(aliveView);
					visibleView = aliveView;
					break;
				}
				// Fallback if no payload: re-seed from net
				ReseedFromNet();
				model.SetActive(aliveView);
				visibleView = aliveView;
				break;
			case ClientMode.Setup:
				ReseedFromNet();
				{
					bool shouldBeVisible = rankView != Rank.UNKNOWN && aliveView;
					model.SetActive(shouldBeVisible);
					visibleView = shouldBeVisible;
				}
				break;
			case ClientMode.Move:
			case ClientMode.Finished:
			case ClientMode.Aborted:
				ReseedFromNet();
				model.SetActive(aliveView);
				visibleView = aliveView;
				break;
			default:
				ReseedFromNet();
				model.SetActive(aliveView);
				visibleView = aliveView;
				break;
		}
    }

    void HandleResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        currentResolveCheckpoint = checkpoint;
        currentBattleIndex = battleIndex;
        // Apply pawn snapshot based on checkpoint, mirroring legacy logic
        if (!tr.pawnDeltas.TryGetValue(pawnId, out SnapshotPawnDelta delta))
        {
            return;
        }
        PawnState current = net.GetPawnFromId(pawnId);
        Rank knownRank = current.GetKnownRank(net.userTeam) ?? Rank.UNKNOWN;
        switch (checkpoint)
        {
            case ResolveCheckpoint.Pre:
                aliveView = delta.preAlive;
                posView = delta.prePos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                DisplayRankView(knownRank == Rank.UNKNOWN ? (delta.preRevealed ? delta.preRank : Rank.UNKNOWN) : knownRank);
                break;
            case ResolveCheckpoint.PostMoves:
                aliveView = delta.preAlive;
                posView = delta.prePos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                DisplayRankView(knownRank == Rank.UNKNOWN ? (delta.preRevealed ? delta.preRank : Rank.UNKNOWN) : knownRank);
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
                aliveView = delta.preAlive; // deaths apply after battle completes
                DisplayRankView(knownRank == Rank.UNKNOWN ? (delta.preRevealed ? delta.preRank : Rank.UNKNOWN) : knownRank);
                break;
            case ResolveCheckpoint.Final:
                aliveView = delta.postAlive;
                posView = delta.postPos;
                DisplayPosView(ViewEventBus.TileViewResolver != null ? ViewEventBus.TileViewResolver(posView) : null);
                DisplayRankView(knownRank == Rank.UNKNOWN ? (delta.postRevealed ? delta.postRank : Rank.UNKNOWN) : knownRank);
                break;
        }
        // Visibility: always show pawns in resolve; rank may be unknown per rules
        model.SetActive(aliveView);
        visibleView = aliveView;
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
            if (shouldBeVisible != (visibleView && aliveView))
            {
                visibleView = shouldBeVisible;
                model.SetActive(shouldBeVisible);
            }
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
