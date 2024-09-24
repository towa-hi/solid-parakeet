using UnityEngine;

public class PawnView : MonoBehaviour
{
    public GameObject model;
    public PawnDef pawn;

    public void Initialize(PawnDef inPawn)
    {
        pawn = inPawn;
    }
}
