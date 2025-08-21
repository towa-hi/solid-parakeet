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
    public bool visibleView;
    public bool isSelected;
    public bool isMovePairStart;


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
        SetConstraintToTile(tileView);
        DisplayRankView(Rank.UNKNOWN);

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
        // figure out what to do based on what happened
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            PawnState pawn = cachedNetState.GetPawnFromId(pawnId);
            if (pawn.alive != aliveView)
            {
                setAliveView = pawn.alive;
            }
            setIsSelected = false;
            setIsMovePairStart = false;
            setRankView = pawn.GetKnownRank(cachedNetState.userTeam) ?? Rank.UNKNOWN;
            setPosView = (pawn.pos, netStateUpdated.phase.tileViews[pawn.pos]);
            switch (netStateUpdated.phase)
            {
                case SetupCommitPhase setupCommitPhase:
                    if (cachedNetState.IsMySubphase())
                    {
                        setVisibleView = setupCommitPhase.pendingCommits.ContainsKey(pawnId);
                        if (setVisibleView == false)
                        {
                            Debug.Log("hiding pawn because not in pending commits");
                        }
                        else
                        {
                            Debug.Log($"showing pawn because it exists in pending with count {setupCommitPhase.pendingCommits.Keys.Count}");
                            
                        }
                    }
                    else
                    {
                        setVisibleView = true;
                    }
                    // TODO: fix opponents pawns showing up as unknown after submitting
                    break;
                case MoveCommitPhase moveCommitPhase:
                    setVisibleView = true;
                    setIsSelected = moveCommitPhase.selectedPos.HasValue && moveCommitPhase.selectedPos.Value == posView;
                    setIsMovePairStart = moveCommitPhase.movePairs.Any(kv => kv.Value.Item1 == posView);
                    break;
                case MoveProvePhase moveProvePhase:
                    setVisibleView = true;
                    setIsMovePairStart = moveProvePhase.turnHiddenMoves.Any(hm => hm.start_pos == posView); 
                    break;
                case RankProvePhase rankProvePhase:
                    setVisibleView = true;
                    setIsMovePairStart = rankProvePhase.turnHiddenMoves.Any(hm => hm.start_pos == posView); 
                    break;
            }
        }
        // for local changes
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupHoverChanged setupHoverChanged:
                    break;
                case SetupRankCommitted(var oldPendingCommits, var setupCommitPhase):
                    setVisibleView = setupCommitPhase.pendingCommits.ContainsKey(pawnId);
                    if (setupCommitPhase.pendingCommits.TryGetValue(pawnId, out Rank? maybeRank))
                    {
                        setRankView = maybeRank ?? Rank.UNKNOWN;
                    }
                    break;
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
            if (pos != posView)
            {
                DisplayPosView(tile);
            }
            posView = pos;
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
        SetConstraintToTile(tileView);
    }
    
    void DisplayRankView(Rank rank)
    {
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(rank);
        Debug.Log(pawnDef);
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
            source = tileView.tileModel.tileOrigin.transform;
        }
        parentConstraint.SetSource(0, new()
        {
            sourceTransform = source,
            weight = 1,
        });
        parentConstraint.constraintActive = true;
    }
    

    bool isMoving = false;
    
    public IEnumerator ArcToPosition(Transform target, float duration, float arcHeight)
    {
        parentConstraint.constraintActive = false;
        isMoving = true;
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
        // Ensure the final position is set
        isMoving = false;
        parentConstraint.constraintActive = true;
    }
}
