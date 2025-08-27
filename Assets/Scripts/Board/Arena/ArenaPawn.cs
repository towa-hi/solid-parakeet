using System;
using UnityEngine;

public class ArenaPawn : MonoBehaviour
{
    public Badge badge;
    public SnapshotPawnDelta pawnDelta;
    public Animator animator;
    public PawnView pawnView;
    public Team team;
    public void Initialize(SnapshotPawnDelta inPawnDelta)
    {
        pawnDelta = inPawnDelta;
        team = pawnDelta.pawnId.GetTeam();
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(pawnDelta.postRank);
        animator.runtimeAnimatorController = team switch
        {
            Team.RED => pawnDef.redAnimatorOverrideController,
            Team.BLUE => pawnDef.blueAnimatorOverrideController,
            _ => throw new ArgumentOutOfRangeException(),
        };
        badge.SetBadge(team, pawnDelta.postRank);
        pawnView.TestSetSprite(pawnDelta.postRank, team);
    }
    
    
}
