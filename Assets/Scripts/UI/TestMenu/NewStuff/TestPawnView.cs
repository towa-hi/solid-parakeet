using System;
using UnityEngine;
using UnityEngine.Animations;
using Random = UnityEngine.Random;

public class TestPawnView : MonoBehaviour
{
    Billboard billboard;

    public Badge badge;

    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;
    public Pawn pawn;

    public Animator animator;
    public RenderEffect renderEffect;
    
    TestBoardManager bm;
    public void Initialize(Pawn inPawn, TestBoardManager inBoardManager)
    {
        pawn = inPawn;
        bm = inBoardManager;
        bm.OnPhaseChanged += OnPhaseChanged;
        bm.OnStateChanged += OnStateChanged;
        
        gameObject.name = $"{pawn.team} Pawn {pawn.def.pawnName}";
        badge.Initialize(pawn, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        // Pick a random normalized time [0…1)
        float randNormTime = Random.Range(0f, 1f);
        
        // Immediately jump the Idle state to that time
        //   layer 0, and use the normalizedTime offset
        animator.Play("Idle", 0, randNormTime);
        
        // Optionally force an immediate update so you don't see a 1‑frame glitch:
        animator.Update(0f);
    }

    void OnPhaseChanged()
    {
        
    }
    void OnStateChanged()
    {
        switch (bm.currentPhase)
        {
            case MovementTestPhase movementTestPhase:
                bool selected = movementTestPhase.selectedPawnView == this;
                renderEffect.SetEffect(EffectType.SELECTOUTLINE, selected);
                bool queued = movementTestPhase.queuedMove?.pawnId == pawn.pawnId;
                renderEffect.SetEffect(EffectType.FILL, queued);
                break;
            case SetupTestPhase setupTestPhase:
                break;
            default:
                throw new ArgumentOutOfRangeException();

        }
        // Update visual position based on the pawn's current state
        if (pawn.isAlive)
        {
            // Set position directly to the tile's origin
            TestTileView tileView = GameManager.instance.testBoardManager.GetTileViewAtPos(pawn.pos);
            transform.position = tileView.origin.position;
        }
        else
        {
            // If the pawn is in purgatory, set position to purgatory
            transform.position = GameManager.instance.purgatory.position;
        }
    }

    void SetRenderEffect(bool enable, string renderEffect)
    {
        
    }
    
}
