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
    
    public void Initialize(Contract.Pawn p, TestBoardManager inBoardManager)
    {
        //pawn = inPawn;
        bm = inBoardManager;
        bm.OnNetworkGameStateChanged += OnNetworkGameStateChanged;
        bm.OnClientGameStateChanged += OnClientGameStateChanged;
        pawnId = Guid.Parse(p.pawn_id);
        team = (Team)p.team;
        pawnDefHash = p.pawn_def_hash;
        isMyTeam = team == inBoardManager.userTeam;
        displayedPos = Globals.Purgatory;
        gameObject.name = $"Pawn {p.team} {p.pawn_id}";
        badge.Initialize(p, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        // Pick a random normalized time [0…1)
        float randNormTime = Random.Range(0f, 1f);
        
        // Immediately jump the Idle state to that time
        //   layer 0, and use the normalizedTime offset
        animator.Play("Idle", 0, randNormTime);
        
        // Optionally force an immediate update so you don't see a 1‑frame glitch:
        animator.Update(0f);
    }

    uint oldPhase = 999;
    void OnClientGameStateChanged(Lobby lobby)
    {
        bool phaseChanged = lobby.phase != oldPhase;
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                if (phaseChanged)
                {
                    SetViewPos(lobby.GetPawnById(pawnId).pos.ToVector2Int());
                    if (isMyTeam)
                    {
                        PawnDef def = Globals.FakeHashToPawnDef(pawnDefHash);
                        badge.symbolRenderer.sprite = def.icon;
                    }
                    
                }
                bool selected = movementTestPhase.selectedPawnView == this;
                renderEffect.SetEffect(EffectType.SELECTOUTLINE, selected);
                bool queued = movementTestPhase.queuedMove?.pawnId == pawnId;
                renderEffect.SetEffect(EffectType.FILL, queued);
                break;
            case SetupTestPhase setupTestPhase:
                if (phaseChanged)
                {
                    if (!isMyTeam)
                    {
                        SetViewPos(lobby.GetPawnById(pawnId).pos.ToVector2Int());
                    }
                }
                if (isMyTeam)
                {
                    SetCommitment(setupTestPhase.commitments[pawnId.ToString()]);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        oldPhase = lobby.phase;
    }

    void OnNetworkGameStateChanged(Lobby lobby)
    {
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                //SetViewPos(lobby.GetPawnById(pawnId).pos.ToVector2Int());
                break;
            case SetupTestPhase setupTestPhase:
                //SetViewPos(lobby.GetPawnById(pawnId).pos.ToVector2Int());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    void SetCommitment(PawnCommitment commitment)
    {
        if (commitment.pawn_id != pawnId.ToString()) throw new InvalidOperationException();
        setupPos = commitment.starting_pos.ToVector2Int();
        badge.InitializeSetup(commitment, team, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        pawnDefHash = commitment.pawn_def_hash;
        SetViewPos(setupPos);
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
