using System;
using UnityEngine;

public class ArenaPawn : MonoBehaviour
{
    public Badge badge;
    public SnapshotPawn pawn;
    public Animator animator;
    public PawnView pawnView;
    
    public void Initialize(SnapshotPawn inPawn)
    {
        pawn = inPawn;
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(pawn.rank);
        animator.runtimeAnimatorController = pawn.team switch
        {
            Team.RED => pawnDef.redAnimatorOverrideController,
            Team.BLUE => pawnDef.blueAnimatorOverrideController,
            _ => throw new ArgumentOutOfRangeException(),
        };
        badge.SetBadge(pawn.team, pawn.rank);
        if (pawn.rank != null) pawnView.TestSetSprite(pawn.rank.Value, pawn.team);
    }
    
}
