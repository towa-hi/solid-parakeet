using System;
using System.Linq;
using System.Collections;
using Contract;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
using Random = UnityEngine.Random;

public class PawnView : MonoBehaviour
{
    static readonly int animatorIsSelected = Animator.StringToHash("IsSelected");
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

    public static event Action<PawnId> OnMoveAnimationCompleted;

    public void TestSetSprite(Rank testRank, Team testTeam)
    {
        team = testTeam;
        rankView = testRank;
        DisplayRankView(testRank);
    }
    public void TestSpriteSelectTransition(bool newAnimationState)
    {
        animator.SetBool(animatorIsSelected, newAnimationState);
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
    void OnDisable()
    {
        // Cancel any ongoing animations to avoid lingering effects across phases/scenes
        StopAllCoroutines();
    }


    void OnSetupPhase()
    {

    }

    void OnMovePhase()
    {

    }

    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        // what to do
        bool? setAliveView = null; // wether to display the pawn dead or alive
        bool? setVisibleView = null; // wether to show the pawn regardless of aliveness
        Rank? setRankView = null; // wether to display rank regardless of revealed rank
        bool? setIsSelected = null;
        bool? setIsMovePairStart = null;
        (Vector2Int, TileView)? setPosView = null;
        // intent for animated move (arc)
        TileView arcFromTile = null;
        TileView arcToTile = null;
        // figure out what to do based on what happened
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            isMyTeam = netStateUpdated.phase.cachedNetState.userTeam == team;
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            PawnState pawn = cachedNetState.GetPawnFromId(pawnId);
            setIsSelected = false;
            setIsMovePairStart = false;
            switch (netStateUpdated.phase)
            {
                case SetupCommitPhase setupCommitPhase:
                    // General non-resolve updates
                    if (pawn.alive != aliveView)
                    {
                        setAliveView = pawn.alive;
                    }
                    setPosView = (pawn.pos, netStateUpdated.phase.tileViews[pawn.pos]);
                    if (cachedNetState.IsMySubphase())
                    {
                        // if have not submitted yet, set rank to whatever it is in pendingcommits or unknown
                        setupCommitPhase.pendingCommits.TryGetValue(pawnId, out Rank? maybeRank);
                        setRankView = maybeRank ?? Rank.UNKNOWN;
                    }
                    else
                    {
                        // since we committed, get it from cache or unknown if its not in cache because its an opponents pawn
                        setRankView = pawn.GetKnownRank(cachedNetState.userTeam) ?? Rank.UNKNOWN;
                    }
                    // hide unknowns
                    setVisibleView = setRankView != Rank.UNKNOWN;
                    break;
                case ResolvePhase resolvePhase:
                    // should only be called once
                    setVisibleView = true;
                    SnapshotPawnDelta pawnDelta = resolvePhase.tr.pawnDeltas[pawnId];
                    setPosView = (pawnDelta.prePos, resolvePhase.tileViews[pawnDelta.prePos]);
                    setAliveView = pawnDelta.preAlive;
                    if (!isMyTeam)
                    {
                        setRankView = pawnDelta.preRevealed ? pawnDelta.preRank : Rank.UNKNOWN;
                    }
                    else
                    {
                        setRankView = pawnDelta.postRank;
                    }
                    setIsMovePairStart = resolvePhase.tr.moves.ContainsKey(pawnId);
                    break;
                case MoveCommitPhase moveCommitPhase:
                    // General non-resolve updates
                    setAliveView = pawn.alive;
                    setRankView = pawn.GetKnownRank(cachedNetState.userTeam) ?? Rank.UNKNOWN;
                    setPosView = (pawn.pos, netStateUpdated.phase.tileViews[pawn.pos]);
                    setVisibleView = true;
                    setIsSelected = moveCommitPhase.selectedPos.HasValue && moveCommitPhase.selectedPos.Value == posView;
                    setIsMovePairStart = moveCommitPhase.movePairs.Any(kv => kv.Value.Item1 == posView);
                    break;
                case MoveProvePhase moveProvePhase:
                    // General non-resolve updates
                    if (pawn.alive != aliveView)
                    {
                        setAliveView = pawn.alive;
                    }
                    setRankView = pawn.GetKnownRank(cachedNetState.userTeam) ?? Rank.UNKNOWN;
                    setPosView = (pawn.pos, netStateUpdated.phase.tileViews[pawn.pos]);
                    setVisibleView = true;
                    setIsMovePairStart = moveProvePhase.turnHiddenMoves.Any(hm => hm.start_pos == posView);
                    break;
                case RankProvePhase rankProvePhase:
                    // General non-resolve updates
                    if (pawn.alive != aliveView)
                    {
                        setAliveView = pawn.alive;
                    }
                    setRankView = pawn.GetKnownRank(cachedNetState.userTeam) ?? Rank.UNKNOWN;
                    setPosView = (pawn.pos, netStateUpdated.phase.tileViews[pawn.pos]);
                    setVisibleView = true;
                    setIsMovePairStart = rankProvePhase.turnHiddenMoves.Any(hm => hm.start_pos == posView);
                    break;
            }
        }
        // for local changes
        foreach (GameOperation operation in changes.operations)
        {
            // intention variables for this loop iteration
            switch (operation)
            {
                case SetupHoverChanged:
                    break;
                case SetupRankCommitted(var oldPendingCommits, var setupCommitPhase):
                    setupCommitPhase.pendingCommits.TryGetValue(pawnId, out Rank? maybeRank);
                    setRankView = maybeRank ?? Rank.UNKNOWN;
                    setVisibleView = setRankView != Rank.UNKNOWN;
                    break;
                case ResolveCheckpointEntered(var checkpoint, var tr, var resolveBattleIndex, var resolvePhase):
                {
                    SnapshotPawnDelta pawnDelta = tr.pawnDeltas[pawnId];
                    Rank preRank = pawnDelta.preRank;
                    Rank postRank = pawnDelta.postRank;
                    setIsMovePairStart = tr.moves.ContainsKey(pawnId);
                    if (!isMyTeam)
                    {
                        preRank = pawnDelta.preRevealed ? pawnDelta.preRank : Rank.UNKNOWN;
                        postRank = pawnDelta.postRevealed ? pawnDelta.postRank : Rank.UNKNOWN;
                    }
                    switch (checkpoint)
                    {
                        case ResolvePhase.Checkpoint.Pre:
                        {
                            setAliveView = pawnDelta.preAlive;
                            setRankView = preRank;
                            setPosView = (pawnDelta.prePos, resolvePhase.tileViews[pawnDelta.prePos]);
                            break;
                        }
                        case ResolvePhase.Checkpoint.PostMoves:
                        {
                            setAliveView = pawnDelta.preAlive;
                            setRankView = preRank;
                            setPosView = (pawnDelta.prePos, resolvePhase.tileViews[pawnDelta.prePos]);
                            if (tr.moves.TryGetValue(pawnId, out MoveEvent move))
                            {
                                arcFromTile = resolvePhase.tileViews[move.from];
                                arcToTile = resolvePhase.tileViews[move.target];
                            }
                            break;
                        }
                        case ResolvePhase.Checkpoint.Battle:
                        {
                            setAliveView = pawnDelta.preAlive;
                            setRankView = preRank;
                            setPosView = (pawnDelta.postPos, resolvePhase.tileViews[pawnDelta.postPos]);
                            for (int i = 0; i < tr.battles.Length; i++)
                            {
                                if (i < resolveBattleIndex)
                                {
                                    setAliveView = pawnDelta.postAlive;
                                    setRankView = postRank;
                                }
                            }
                            break;
                        }
                        case ResolvePhase.Checkpoint.Final:
                        {
                            setAliveView = pawnDelta.postAlive;
                            setRankView = postRank;
                            setPosView = (pawnDelta.postPos, resolvePhase.tileViews[pawnDelta.postPos]);
                            break;
                        }
                    }
                    break;
                }
                case MoveHoverChanged(var moveInputTool, var newHoveredPos, var moveCommitPhase):
                    break;
                case MovePosSelected(var newPos, var targetablePositions, var movePairsSnapshot):
                    {
                        setIsSelected = newPos.HasValue && posView == newPos.Value;
                        setIsMovePairStart = movePairsSnapshot.ContainsKey(pawnId);
                        break;
                    }
                case MovePairUpdated(var movePairsSnapshot2, var changedPawnId, var phaseRef):
                    {
                        setIsMovePairStart = movePairsSnapshot2.ContainsKey(pawnId);
                        break;
                    }
            }
            // execute intentions that require side effects after decision
        }
        // set cached vars and do stuff that requires diffs
        if (setIsSelected is bool inIsSelected)
        {
            isSelected = inIsSelected;
        }
        if (setIsMovePairStart is bool inIsMovePairStart)
        {
            isMovePairStart = inIsMovePairStart;
        }
        if (setAliveView is bool inAliveView)
        {
            aliveView = inAliveView;
        }
        if (setVisibleView is bool inVisibleView)
        {
            visibleView = inVisibleView;
        }

        if (setRankView is Rank inRankView)
        {
            if (inRankView != rankView)
            {
                DisplayRankView(inRankView);
            }
            rankView = inRankView;
        }

        if (setPosView is (Vector2Int pos, TileView tile))
        {
            DisplayPosView(tile);
            posView = pos;
        }

        if (arcToTile)
        {
            SetArcToTile(arcFromTile, arcToTile);
        }
        bool setAnimatorIsSelected = isSelected || isMovePairStart;
        if (animator.GetBool(animatorIsSelected) != setAnimatorIsSelected)
        {
            Debug.Log($"animator selected set to {setAnimatorIsSelected}");
            animator.SetBool(animatorIsSelected, setAnimatorIsSelected);
        }
        if (visibleView && aliveView)
        {
            model.SetActive(true);
        }
        else
        {
            model.SetActive(false);
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
