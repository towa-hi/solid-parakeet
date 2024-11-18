using System;
using UnityEngine;

[System.Serializable]
public class Pawn
{
    public Guid pawnId;
    [SerializeField] public PawnDef def;
    public Player player;
    public Vector2Int pos;
    public bool isSetup;
    
    public Pawn(PawnDef inDef, Player inPlayer, Vector2Int inPos, bool inIsSetup)
    {
        if (inPlayer == Player.NONE)
        {
            throw new Exception("Pawn cannot have player == Player.NONE");
        }

        pawnId = inIsSetup ? Guid.Empty : Guid.NewGuid();
        def = inDef;
        player = inPlayer;
        pos = inPos;
        isSetup = inIsSetup;
    }
}
