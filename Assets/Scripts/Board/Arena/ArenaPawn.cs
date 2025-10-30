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
        pawnView.ResetHurtAnimation();
        team = pawnDelta.pawnId.GetTeam();
        PawnDef pawnDef = ResourceRoot.GetPawnDefFromRank(pawnDelta.postRank);
        animator.runtimeAnimatorController = team switch
        {
            Team.RED => pawnDef.redAnimatorOverrideController,
            Team.BLUE => pawnDef.blueAnimatorOverrideController,
            _ => throw new ArgumentOutOfRangeException(),
        };
        // Arena: badges should always be hidden regardless of global setting
        if (pawnView != null)
        {
            pawnView.overrideBadgeHidden = true;
            if (pawnView.badge != null && pawnView.badge.gameObject.activeSelf)
            {
                pawnView.badge.gameObject.SetActive(false);
            }
        }
        if (badge != null && badge.gameObject.activeSelf)
        {
            badge.gameObject.SetActive(false);
        }
    }
    
    
}
