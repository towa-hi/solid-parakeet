using UnityEngine;

public class PawnView : MonoBehaviour
{
    public GameObject model;
    public Pawn pawn;

    public void Initialize(PawnDef inPawn)
    {
        pawn = new Pawn(inPawn);
        gameObject.name = $"Pawn {pawn.def.pawnName}";
        GetComponent<DebugText>()?.SetText(pawn.def.pawnName);
    }
}
