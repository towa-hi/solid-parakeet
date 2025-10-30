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
    public Animator animator;
    public Badge badge;
    public GameObject model;
    // immutable
    public PawnId pawnId;
    public Vector2Int startPos;
    public Team team;
    // mutable
    public Rank? rank;
    
    void OnEnable()
    {
        SettingsManager.OnSettingChanged += HandleSettingChanged;
        UpdateBadgeVisibility();
    }

    void OnDisable()
    {
        SettingsManager.OnSettingChanged -= HandleSettingChanged;
    }

    void HandleSettingChanged(SettingsKey key, int val)
    {
        if (key == SettingsKey.DISPLAYBADGES)
        {
            UpdateBadgeVisibility();
        }
    }
    
    public void Initialize(PawnId inPawnId, Transform target)
    {
        rank = null;
        pawnId = inPawnId;
        startPos = pawnId.Decode().Item1;
        team = pawnId.Decode().Item2;
        gameObject.name = $"SetupPawn {pawnId} team {team} at {startPos}";
        //model.SetActive(false);
        parentConstraint.SetSource(0, new ConstraintSource()
        {
            sourceTransform = target,
            weight = 1,
        });
        parentConstraint.constraintActive = true;
        badge.SetBadge(team, null);
        UpdateBadgeVisibility();
    }

    // void OnClientGameStateChanged(IPhase phase, bool phaseChanged)
    // {
        // switch (phase)
        // {
        //     case MovementPhase movementPhase:
        //         break;
        //     case SetupPhase setupPhase:
        //         if (setupPhase.clientState.committed)
        //         {
        //             foreach (SetupCommit setupCommit in setupPhase.clientState.lockedCommits)
        //             {
        //                 if (Globals.DecodeStartingPosAndTeamFromPawnId(setupCommit.pawn_id).Item1 == pos)
        //                 {
        //                     Rank newRank = CacheManager.LoadHiddenRank(setupCommit.hidden_rank_hash).rank;
        //                     if (rank != newRank)
        //                     {
        //                         SetPendingCommit(newRank);
        //                     }
        //                     break;
        //                 }
        //             }
        //         }
        //         else
        //         {
        //             Rank? newRank = setupPhase.clientState.pendingCommits[pos];
        //             if (rank != newRank)
        //             {
        //                 SetPendingCommit(newRank);
        //             }
        //         }
        //         break;
        //     default:
        //         throw new ArgumentOutOfRangeException(nameof(phase));
        //
        // }
    //}
    //
    // void SetPendingCommit(Rank? inRank)
    // {
    //     rank = inRank;
    //     if (rank == null)
    //     {
    //         model.SetActive(false);
    //     }
    //     else
    //     {
    //         badge.SetBadge(team, inRank);
    //         model.SetActive(true);
    //         switch (team)
    //         {
    //             case Team.RED:
    //                 if (pawnDef.redAnimatorOverrideController)
    //                 {
    //                     animator.runtimeAnimatorController = pawnDef.redAnimatorOverrideController;
    //                 }
    //                 break;
    //             case Team.BLUE:
    //                 if (pawnDef.blueAnimatorOverrideController)
    //                 {
    //                     animator.runtimeAnimatorController = pawnDef.blueAnimatorOverrideController;
    //                 }
    //                 break;
    //             case Team.NONE:
    //             default:
    //                 throw new ArgumentOutOfRangeException();
    //         }
    //         float randNormTime = Random.Range(0f, 1f);
    //         animator.Play("Idle", 0, randNormTime);
    //         animator.Update(0f);
    //     }
    // }

    
    void UpdateBadgeVisibility()
    {
        if (badge == null) return;
        bool shouldShow = SettingsManager.Load().displayBadges;
        if (badge.gameObject.activeSelf != shouldShow)
        {
            badge.gameObject.SetActive(shouldShow);
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