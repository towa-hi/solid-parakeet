using UnityEngine;

public class Pawn
{
    public readonly PawnDef def;
    public Player player;

    public Pawn(PawnDef inDef)
    {
        def = inDef;
    }
}
