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
    public uint tempId;
    public Rank rank;
    public Vector2Int pos;
    public Team team;
    public Animator animator;
    public Badge badge;
    
    TestBoardManager bm;
    
    public void Initialize(uint inTempId, Rank inRank, Vector2Int inPos, Team inTeam, TestBoardManager inBm)
    {
        bm = inBm;
        tempId = inTempId;
        rank = inRank;
        team = inTeam;
        pos = inPos;
        PawnDef def = GameManager.instance.orderedPawnDefList.First(def => def.rank == rank);
        gameObject.name = $"Pawn {tempId} {rank}";
        badge.symbolRenderer.sprite = def.icon;

        switch (team)
        {
            case Team.RED:
                if (def.redAnimatorOverrideController)
                {
                    animator.runtimeAnimatorController = def.redAnimatorOverrideController;
                }
                break;
            case Team.BLUE:
                if (def.blueAnimatorOverrideController)
                {
                    animator.runtimeAnimatorController = def.blueAnimatorOverrideController;
                }
                break;
            case Team.NONE:
            default:
                throw new ArgumentOutOfRangeException();
        }
        float randNormTime = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randNormTime);
        animator.Update(0f);
        SetViewPos();
    }
    
    void SetViewPos()
    {
        if (pos == Globals.Purgatory)
        {
            parentConstraint.SetSource(0, new ConstraintSource
            {
                sourceTransform = bm.purgatory,
                weight = 1,
            });
        }
        else
        {
            TestTileView tileView = bm.GetTileViewAtPos(pos);
            parentConstraint.SetSource(0, new ConstraintSource
            {
                sourceTransform = tileView.tileModel.tileOrigin.transform,
                weight = 1,
            });
        }
        parentConstraint.constraintActive = true;
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
        bm.vortex.EndVortex();
    }
}
