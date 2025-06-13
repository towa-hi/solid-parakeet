using System;
using System.Collections;
using Contract;
using UnityEngine;
using UnityEngine.Animations;
using Random = UnityEngine.Random;

public class TestPawnView : MonoBehaviour
{
    Billboard billboard;

    public Badge badge;

    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;
    //public Pawn pawn;
    public uint pawnId;
    public Team team;
    public bool isMyTeam;
    public Vector2Int displayedPos;
    public string pawnDefHash;
    
    public Animator animator;
    public RenderEffect renderEffect;
    TestBoardManager bm;

    public Vector2Int setupPos;
    
    uint oldPhase = 999;
    
    public void Initialize(Contract.Pawn p, TestBoardManager inBoardManager)
    {
        oldPhase = 999;
        //pawn = inPawn;
        bm = inBoardManager;
        //bm.OnNetworkGameStateChanged += OnNetworkGameStateChanged;
        bm.OnClientGameStateChanged += OnClientGameStateChanged;
        //badge.Initialize(p, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        SetPawn(p);
        //isMyTeam = team == inBoardManager.userTeam;
    }
    
    void Revealed(PawnCommit pawnCommit)
    {
        Rank rank = CacheManager.LoadHiddenRank(pawnCommit.hidden_rank_hash).rank;
        PawnDef pawnDef = GameManager.instance.GetPawnDefFromRankTemp(rank);
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
        float randNormTime = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randNormTime);
        animator.Update(0f);
    }
    
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
    
    void OnClientGameStateChanged(GameNetworkState networkState, ITestPhase phase)
    {
        bool phaseChanged = (uint)networkState.gameState.phase != oldPhase;
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
    }
    
    void SetPawn(Contract.Pawn p)
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
            TestTileView tileView = bm.GetTileViewAtPos(pos);
            parentConstraint.SetSource(0, new ConstraintSource
            {
                sourceTransform = tileView.tileModel.tileOrigin.transform,
                weight = 1,
            });
        }
        parentConstraint.constraintActive = true;
        displayedPos = pos;
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
