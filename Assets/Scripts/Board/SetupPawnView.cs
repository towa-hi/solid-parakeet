using System;
using System.Collections;
using System.Linq;
using Contract;
using UnityEngine;
using UnityEngine.Animations;
using Random = UnityEngine.Random;

public class SetupPawnView: MonoBehaviour
{
    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;
    public Rank? rank;
    public Vector2Int pos;
    public Team team;
    public Animator animator;
    public Badge badge;
    public GameObject model;
    
    Phase oldPhase = Phase.Completed;
    TestBoardManager bm;
    
    public void Initialize(TestTileView tileView, Team inTeam, TestBoardManager inBm)
    {
        bm = inBm;
        bm.OnClientGameStateChanged += OnClientGameStateChanged;
        rank = null;
        team = inTeam;
        pos = tileView.tile.pos;
        gameObject.name = $"SetupPawn {pos}";
        model.SetActive(false);
        parentConstraint.SetSource(0, new ConstraintSource()
        {
            sourceTransform = tileView.tileModel.tileOrigin.transform,
            weight = 1,
        });
        parentConstraint.constraintActive = true;
    }

    void OnClientGameStateChanged(GameNetworkState networkState, ITestPhase phase)
    {
        switch (phase)
        {
            case MovementTestPhase movementTestPhase:
                break;
            case SetupTestPhase setupTestPhase:
                if (setupTestPhase.clientState.committed)
                {
                    foreach (PawnCommit pawnCommit in setupTestPhase.clientState.lockedCommits)
                    {
                        if (Globals.DecodeStartingPos(pawnCommit.pawn_id) == pos)
                        {
                            Rank newRank = CacheManager.LoadHiddenRank(pawnCommit.hidden_rank_hash).rank;
                            if (rank != newRank)
                            {
                                SetPendingCommit(newRank);
                            }
                            break;
                        }
                    }
                }
                else
                {
                    Rank? newRank = setupTestPhase.clientState.pendingCommits[pos];
                    if (rank != newRank)
                    {
                        SetPendingCommit(newRank);
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));

        }
    }

    void SetPendingCommit(Rank? inRank)
    {
        rank = inRank;
        if (rank == null)
        {
            model.SetActive(false);
        }
        else
        {
            PawnDef pawnDef = GameManager.instance.orderedPawnDefList.First(def => def.rank == rank);
            badge.SetBadge(team, pawnDef);
            model.SetActive(true);
            switch (team)
            {
                case Team.RED:
                    if (pawnDef.redAnimatorOverrideController)
                    {
                        animator.runtimeAnimatorController = pawnDef.redAnimatorOverrideController;
                    }
                    break;
                case Team.BLUE:
                    if (pawnDef.blueAnimatorOverrideController)
                    {
                        animator.runtimeAnimatorController = pawnDef.blueAnimatorOverrideController;
                    }
                    break;
                case Team.NONE:
                default:
                    throw new ArgumentOutOfRangeException();
            }
            float randNormTime = Random.Range(0f, 1f);
            animator.Play("Idle", 0, randNormTime);
            animator.Update(0f);
        }
    }

    
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
        // Ensure the final position is set
        parentConstraint.constraintActive = true;
    }
}

public struct SetupPawn
{
    public uint pawnViewId;
    public Rank rank;
    public Vector2Int pos;
}