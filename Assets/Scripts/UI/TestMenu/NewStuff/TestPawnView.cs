using UnityEngine;
using UnityEngine.Animations;

public class TestPawnView : MonoBehaviour
{
    Billboard billboard;

    public Badge badge;

    public ParentConstraint parentConstraint;
    ConstraintSource parentSource;
    public Pawn pawn;
    
    public void Initialize(Pawn inPawn)
    {
        pawn = inPawn;
        string objectName = $"{pawn.team} Pawn {pawn.def.pawnName}";
        if (!pawn.isAlive)
        {
            objectName += $" {Shared.ShortGuid(pawn.pawnId)}";
        }
        gameObject.name = objectName;
        badge.Initialize(pawn, PlayerPrefs.GetInt("DISPLAYBADGE") == 1);
        if (!pawn.isAlive)
        {
            transform.position = GameManager.instance.purgatory.position;
        }
    }

    public void Apply()
    {
        // Update visual position based on the pawn's current state
        if (GameManager.instance.testBoardManager.tileViews.TryGetValue(pawn.pos, out TestTileView tileView))
        {
            // Set position directly to the tile's origin
            transform.position = tileView.origin.position;
        }
        else if (pawn.pos == Globals.Purgatory)
        {
            // If the pawn is in purgatory, set position to purgatory
            transform.position = GameManager.instance.purgatory.position;
        }
    }
}
