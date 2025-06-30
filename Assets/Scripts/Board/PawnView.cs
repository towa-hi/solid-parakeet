using System;
using System.Collections;
using Contract;
using UnityEngine;
using UnityEngine.Animations;
using Random = UnityEngine.Random;

public class PawnView : MonoBehaviour
{
    Billboard billboard;

    public Badge badge;
    public GameObject model;
    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;
    
    public Animator animator;
    public RenderEffect renderEffect;
    BoardManager bm;
    // immutable
    public PawnId pawnId;
    public Vector2Int startPos;
    public Team team;
    
    // cached
    bool aliveView;
    Rank rankView;
    public Vector2Int posView;

    bool firstTime = false;
    
    public void Initialize(PawnState pawn)
    {
        pawnId = pawn.pawn_id;
        gameObject.name = $"Pawn {pawnId} team {pawn.GetTeam()} startPos {pawn.GetStartPosition()}";
        startPos = pawnId.Decode().Item1;
        team = pawnId.Decode().Item2;
        rankView = Rank.UNKNOWN;
        aliveView = false;
        posView = Vector2Int.zero;
    }

    public void PhaseStateChanged(PhaseBase phase, PhaseChanges changes)
    {
        firstTime = true;
        PawnState pawn = phase.cachedNetworkState.gameState.GetPawnStateFromId(pawnId);
        Team userTeam = phase.cachedNetworkState.userTeam;
        if (changes.networkUpdated)
        {
            
        }
        switch (phase)
        {
            case SetupCommitPhase setupCommitPhase:
                Rank? pendingCommitRank = setupCommitPhase.pendingCommits.TryGetValue(pawn.pos, out Rank rank) ? rank : null;
                SetRankView(pawn, userTeam, pendingCommitRank);
                // send pawnView to purgatory to hide it if rank is not known
                SetPosView(rankView == Rank.UNKNOWN ? null : setupCommitPhase.tileViews[pawn.pos]);
                break;
            case MoveCommitPhase moveCommitPhase:
                SetRankView(pawn, userTeam);
                SetPosView(moveCommitPhase.tileViews[pawn.pos]);
                break;
            case MoveProvePhase moveProvePhase:
                SetRankView(pawn, userTeam);
                break;
            case RankProvePhase rankProvePhase:
                SetRankView(pawn, userTeam);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }
    }

    void SetPosView(TileView tileView = null)
    {
        posView = tileView?.tile.pos ?? Globals.Purgatory;
        Transform target = tileView ? tileView.origin : GameManager.instance.purgatory;
        parentConstraint.SetSource(0, new ConstraintSource()
        {
            sourceTransform = target,
            weight = 1,
        });
        parentConstraint.constraintActive = true;
    }
    
    void SetRankView(PawnState pawn, Team userTeam, Rank? mOverrideRank = null)
    {
        Rank oldRankView = rankView;
        rankView = Rank.UNKNOWN;
        if (mOverrideRank is Rank overrideRank)
        {
            rankView = overrideRank;
        }
        else if (pawn.rank is Rank rank)
        {
            rankView = rank;
        }
        else if (pawn.GetTeam() == userTeam && CacheManager.GetHiddenRank(pawn.hidden_rank_hash) is HiddenRank hiddenRank)
        {
            rankView = hiddenRank.rank;
        }
        
        if (!firstTime && rankView == oldRankView)
        {
            return;
        }
        PawnDef pawnDef = GameManager.instance.GetPawnDefFromRankTemp(rankView);
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
            default:
                throw new ArgumentOutOfRangeException();
        }
        Debug.Log($"animation set for {gameObject.name}");
        float randNormTime = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randNormTime);
        animator.Update(0f);
        badge.SetBadge(team, rankView);
    }
    
    // void Revealed(PawnState p)
    // {
    //     Rank rank = CacheManager.LoadHiddenRank(p.hidden_rank_hash).rank;
    //     PawnDef pawnDef = GameManager.instance.GetPawnDefFromRankTemp(rank);
    //     switch (team)
    //     {
    //         case Team.RED:
    //             if (pawnDef.redAnimatorOverrideController)
    //             {
    //                 animator.runtimeAnimatorController = pawnDef.redAnimatorOverrideController;
    //             }
    //
    //             break;
    //         case Team.BLUE:
    //             if (pawnDef.blueAnimatorOverrideController)
    //             {
    //                 animator.runtimeAnimatorController = pawnDef.blueAnimatorOverrideController;
    //             }
    //
    //             break;
    //         default:
    //             throw new ArgumentOutOfRangeException();
    //     }
    //     float randNormTime = Random.Range(0f, 1f);
    //     animator.Play("Idle", 0, randNormTime);
    //     animator.Update(0f);
    // }
    
    // void Revealed(Contract.Pawn p)
    // {
    //     bool changed = false;
    //     
    //     if (isMyTeam || p.is_revealed || PlayerPrefs.GetInt("CHEATMODE") == 1)
    //     {
    //         PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
    //         switch (team)
    //         {
    //             case Team.RED:
    //                 if (animator.runtimeAnimatorController != def.redAnimatorOverrideController)
    //                 {
    //                     animator.runtimeAnimatorController = def.redAnimatorOverrideController;
    //                     changed = true;
    //                 }
    //                 break;
    //             case Team.BLUE:
    //                 if (animator.runtimeAnimatorController != def.blueAnimatorOverrideController)
    //                 {
    //                     animator.runtimeAnimatorController = def.blueAnimatorOverrideController;
    //                     changed = true;
    //                 }
    //                 break;
    //             default:
    //                 throw new ArgumentOutOfRangeException();
    //         }
    //     }
    //     else
    //     {
    //         PawnDef def = Globals.FakeHashToPawnDef("Unknown");
    //         if (animator.runtimeAnimatorController != def.redAnimatorOverrideController)
    //         {
    //             animator.runtimeAnimatorController = def.redAnimatorOverrideController;
    //             changed = true;
    //         }
    //     }
    //     if (changed)
    //     {
    //         float randNormTime = Random.Range(0f, 1f);
    //         animator.Play("Idle", 0, randNormTime);
    //         animator.Update(0f);
    //     }
    // }
    
    // void OnClientGameStateChanged(IPhase phase, bool phaseChanged)
    // {
        // bool phaseChanged = (uint)networkState.lobbyInfo.phase != oldPhase;
        // switch (phase)
        // {
        //     case MovementTestPhase movementTestPhase:
        //         Contract.Pawn currentPawn = lobby.GetPawnById(pawnId);
        //         if (phaseChanged)
        //         {
        //             //Debug.Log("going to movement phase for the first time");
        //             SetPawn(currentPawn);
        //         }
        //         if (displayedPos != currentPawn.pos.ToVector2Int())
        //         {
        //             Debug.Log("Moving pawn to pos normally...");
        //             Transform target = !currentPawn.is_alive ? bm.purgatory : bm.GetTileViewAtPos(currentPawn.pos.ToVector2Int()).tileModel.tileOrigin;
        //             SetViewPos(currentPawn.pos.ToVector2Int());
        //             StopAllCoroutines();
        //             bm.vortex.StartVortex();
        //             StartCoroutine(ArcToPosition(target, Globals.PawnMoveDuration, 0.2f));
        //         }
        //         bool showBadge = isMyTeam || currentPawn.is_revealed || PlayerPrefs.GetInt("CHEATMODE") == 1;
        //         if (PlayerPrefs.GetInt("DISPLAYBADGE") == 0)
        //         {
        //             showBadge = false;
        //         }
        //         badge.Initialize(currentPawn, showBadge);
        //         Revealed(currentPawn);
        //         break;
        //     case SetupTestPhase setupTestPhase:
        //         if (phaseChanged)
        //         {
        //             //Debug.Log("going to setup phase for the first time");
        //             if (!isMyTeam)
        //             {
        //                 SetViewPos(lobby.GetPawnById(pawnId).pos.ToVector2Int());
        //             }
        //         }
        //         if (isMyTeam)
        //         {
        //             PawnCommit c = setupTestPhase.clientState.commitments[pawnId.ToString()];
        //             SetCommitment(c);
        //             Revealed(c);
        //             SetViewPos(setupPos);
        //             badge.Initialize(c, team,true);
        //         }
        //         break;
        //     default:
        //         throw new ArgumentOutOfRangeException();
        // }
        // oldPhase = lobby.phase;
    //}
    
    void SetPawn(PawnState p)
    {
        // pawnId = p.pawn_id;
        // team = (Team)p.team;
        // pawnDefHash = p.pawn_def_hash;
        // if (!string.IsNullOrEmpty(pawnDefHash))
        // {
        //     PawnDef def = Globals.FakeHashToPawnDef(pawnDefHash);
        //     badge.symbolRenderer.sprite = def.icon;
        // }
        // gameObject.name = $"Pawn {p.team} {p.pawn_id}";
        // SetViewPos(p.pos.ToVector2Int());
    }
    
    void SetViewPos(Vector2Int pos)
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
            TileView tileView = bm.GetTileViewAtPos(pos);
            parentConstraint.SetSource(0, new ConstraintSource
            {
                sourceTransform = tileView.tileModel.tileOrigin.transform,
                weight = 1,
            });
        }
        parentConstraint.constraintActive = true;
        posView = pos;
    }
    
    void SetRenderEffect(bool enable, string renderEffect)
    {
        
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
        bm.vortex.EndVortex();
    }
}
