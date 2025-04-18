using UnityEngine;
using UnityEngine.Animations;

public class TestPawnView : MonoBehaviour
{
    Billboard billboard;

    public Badge badge;

    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;
    public Pawn pawn;
    
    public void Initialize(Pawn inPawn, TestBoardManager bm)
    {
        pawn = inPawn;
        bm.OnStateChanged += OnStateChanged;
        string objectName = $"{pawn.team} Pawn {pawn.def.pawnName}";
        gameObject.name = objectName;
        badge.Initialize(pawn, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
    }

    void OnStateChanged(TestBoardManager bm)
    {
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
    
}
