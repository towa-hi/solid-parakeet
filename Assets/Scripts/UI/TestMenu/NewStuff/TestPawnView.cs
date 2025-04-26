using System;
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
    public Guid pawnId;
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
        badge.Initialize(p, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        SetPawn(p);
        isMyTeam = team == inBoardManager.userTeam;
    }

    void Revealed(Contract.PawnCommitment c)
    {
        if (!string.IsNullOrEmpty(c.pawn_def_hash))
        {
            PawnDef def = Globals.FakeHashToPawnDef(c.pawn_def_hash);
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
            float randNormTime = Random.Range(0f, 1f);
            animator.Play("Idle", 0, randNormTime);
            animator.Update(0f);
        }
        
    }
    
    void Revealed(Contract.Pawn p)
    {
        bool changed = false;
        if (!p.is_revealed && !isMyTeam)
        {
            PawnDef def = Globals.FakeHashToPawnDef("Unknown");
            if (animator.runtimeAnimatorController != def.redAnimatorOverrideController)
            {
                animator.runtimeAnimatorController = def.redAnimatorOverrideController;
                changed = true;
            }
        }
        else
        {
            PawnDef def = Globals.FakeHashToPawnDef(p.pawn_def_hash);
            switch (team)
            {
                case Team.RED:
                    if (animator.runtimeAnimatorController != def.redAnimatorOverrideController)
                    {
                        animator.runtimeAnimatorController = def.redAnimatorOverrideController;
                        changed = true;
                    }
                    break;
                case Team.BLUE:
                    if (animator.runtimeAnimatorController != def.blueAnimatorOverrideController)
                    {
                        animator.runtimeAnimatorController = def.blueAnimatorOverrideController;
                        changed = true;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        if (changed)
        {
            float randNormTime = Random.Range(0f, 1f);
            animator.Play("Idle", 0, randNormTime);
            animator.Update(0f);
        }
    }
    
    void OnClientGameStateChanged(Lobby lobby, ITestPhase phase)
    {
        bool phaseChanged = lobby.phase != oldPhase;
        switch (phase)
        {
            case MovementTestPhase movementTestPhase:
                Contract.Pawn currentPawn = lobby.GetPawnById(pawnId);
                if (phaseChanged)
                {
                    //Debug.Log("going to movement phase for the first time");
                    SetPawn(currentPawn);
                }
                if (displayedPos != currentPawn.pos.ToVector2Int())
                {
                    Debug.Log("Moving pawn to pos normally...");
                    SetViewPos(currentPawn.pos.ToVector2Int());
                }

                Revealed(currentPawn);
                break;
            case SetupTestPhase setupTestPhase:
                if (phaseChanged)
                {
                    Debug.Log("going to setup phase for the first time");
                    if (!isMyTeam)
                    {
                        SetViewPos(lobby.GetPawnById(pawnId).pos.ToVector2Int());
                    }
                }
                if (isMyTeam)
                {
                    SetCommitment(setupTestPhase.clientState.commitments[pawnId.ToString()]);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        oldPhase = lobby.phase;
    }
    
    void SetCommitment(PawnCommitment commitment)
    {
        if (commitment.pawn_id != pawnId.ToString()) throw new InvalidOperationException();
        setupPos = commitment.starting_pos.ToVector2Int();
        badge.InitializeSetup(commitment, team, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        pawnDefHash = commitment.pawn_def_hash;
        Revealed(commitment);
        SetViewPos(setupPos);
    }

    void SetPawn(Contract.Pawn p)
    {
        pawnId = Guid.Parse(p.pawn_id);
        team = (Team)p.team;
        pawnDefHash = p.pawn_def_hash;
        if (!string.IsNullOrEmpty(pawnDefHash))
        {
            PawnDef def = Globals.FakeHashToPawnDef(pawnDefHash);
            badge.symbolRenderer.sprite = def.icon;
        }
        gameObject.name = $"Pawn {p.team} {p.pawn_id}";
        SetViewPos(p.pos.ToVector2Int());
    }
    
    void SetViewPos(Vector2Int pos)
    {
        transform.position = pos == Globals.Purgatory ? GameManager.instance.purgatory.position : bm.GetTileViewAtPos(pos).origin.position;
        displayedPos = pos;
    }
    
    void SetRenderEffect(bool enable, string renderEffect)
    {
        
    }
    
}
