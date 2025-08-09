using System;
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
    Rank rankView;
    bool aliveView;
    public Vector2Int posView;
    bool visible;

    // debug
    [SerializeField] SerializablePawnState debugPawnState;

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
        posView = Vector2Int.zero;
        SetConstraintToTile(tileView);

        debugPawnState = new SerializablePawnState()
        {
            alive = pawn.alive,
            moved = pawn.moved,
            movedScout = pawn.moved_scout,
            pawnID = pawn.pawn_id.Value,
            pos = new SerializablePos { x = pawn.pos.x, y = pawn.pos.y },
            rank = pawn.rank ?? Rank.UNKNOWN,
            rankHasValue = pawn.rank.HasValue,
            zz_revealed = pawn.zz_revealed,
        };
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
        bool? setAlive = null; // wether to display the pawn dead or alive 
        bool? setVisible = null; // wether to show the pawn regardless of aliveness
        Rank? setRankView = null; // wether to display rank regardless of revealed rank
        bool? setAnimatorIsSelected = null;
        (Vector2Int, TileView)? setPosView = null;
        // figure out what to do based on what happened
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            PawnState pawn = cachedNetState.GetPawnFromId(pawnId);
            setRankView = pawn.GetKnownRank(cachedNetState.userTeam) ?? Rank.UNKNOWN;
            setAlive = pawn.alive;
            if (setAlive.Value)
            {
                setPosView = (pawn.pos, netStateUpdated.phase.tileViews[pawn.pos]);
            }
            else
            {
                setPosView = (Globals.Purgatory, null);
            }
            switch (cachedNetState.lobbyInfo.phase)
            {
                case Phase.SetupCommit:
                    if (cachedNetState.IsMySubphase())
                    {
                        setVisible = false;
                    }
                    else
                    {
                        // only show your pawns when waiting
                        setVisible = team == cachedNetState.userTeam;
                    }
                    break;
                case Phase.MoveCommit:
                    setVisible = true;
                    break;
                case Phase.MoveProve:
                    setVisible = true;
                    break;
                case Phase.RankProve:
                    setVisible = true;
                    break;
                case Phase.Finished:
                    break;
                case Phase.Aborted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            debugPawnState = new SerializablePawnState()
            {
                alive = pawn.alive,
                moved = pawn.moved,
                movedScout = pawn.moved_scout,
                pawnID = pawn.pawn_id.Value,
                pos = new SerializablePos { x = pawn.pos.x, y = pawn.pos.y },
                rank = pawn.rank ?? Rank.UNKNOWN,
                rankHasValue = pawn.rank.HasValue,
                zz_revealed = pawn.zz_revealed,
            };
            setAnimatorIsSelected = false;
        }
        // for local changes
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupHoverChanged setupHoverChanged:
                    break;
                case SetupRankCommitted(var oldPendingCommits, var setupCommitPhase):
                    if (pawnId.GetTeam() == setupCommitPhase.cachedNetState.userTeam)
                    {
                        if (oldPendingCommits[pawnId] != setupCommitPhase.pendingCommits[pawnId])
                        {
                            setRankView = setupCommitPhase.pendingCommits[pawnId] ?? Rank.UNKNOWN;
                            setVisible = setRankView.Value != Rank.UNKNOWN;
                        }
                    }
                    else
                    {
                        visible = false;
                    }
                    break;
                case MoveHoverChanged moveHoverChanged:
                    break;
                case MovePosSelected movePosSelected:
                {
                    bool isCurrentlySelectedPawn = movePosSelected.newPos.HasValue && movePosSelected.newPos.Value == posView;
                    setAnimatorIsSelected = isCurrentlySelectedPawn;
                    break;
                }
                case MovePairUpdated movePairUpdated:
                {
                    // Keep pawns in selected animation if they are part of movePairs (as a start)
                    bool isPlannedStart = movePairUpdated.movePairsSnapshot.ContainsKey(pawnId);
                    setAnimatorIsSelected = isPlannedStart;
                    break;
                }
            }
        }
        // now do the stuff
        if (setAlive.HasValue)
        {
            aliveView = setAlive.Value;
            model.SetActive(aliveView);
        }
        if (setVisible != null)
        {
            visible = setVisible.Value;
            model.SetActive(visible);
        }
        if (setRankView != null)
        {
            rankView = setRankView.Value;
            DisplayRankView(rankView);
        }

        if (setPosView != null)
        {
            (Vector2Int pos, TileView tile) = setPosView.Value;
            posView = pos;
            DisplayPosView(tile);
        }

        if (setAnimatorIsSelected != null)
        {
            animator.SetBool(animatorIsSelected, setAnimatorIsSelected.Value);
        }
    }
    
    void DisplayPosView(TileView tileView = null)
    {
        Transform target = tileView ? tileView.origin : GameManager.instance.purgatory;
        parentConstraint.SetSource(0, new ConstraintSource()
        {
            sourceTransform = target,
            weight = 1,
        });
        parentConstraint.constraintActive = true;
    }
    
    void DisplayRankView(Rank rank)
    {
        PawnDef pawnDef = GameManager.instance.GetPawnDefFromRankTemp(rank);
        switch (team)
        {
            case Team.RED:
                animator.runtimeAnimatorController = pawnDef.redAnimatorOverrideController;
                break;
            case Team.BLUE:
                animator.runtimeAnimatorController = pawnDef.blueAnimatorOverrideController;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        float randNormTime = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randNormTime);
        animator.Update(0f);
        badge.SetBadge(team, rank);
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
    }
}
