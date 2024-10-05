using System;
using UnityEngine;

[System.Serializable]
public class Pawn
{
    [SerializeField] public PawnDef def;
    public Player player;
    public Vector2Int pos;
    
    public Pawn(PawnDef inDef, Player inPlayer, Vector2Int inPos)
    {
        if (inPlayer == Player.NONE)
        {
            throw new Exception("Pawn cannot have player == Player.NONE");
        }
        def = inDef;
        player = inPlayer;
        pos = inPos;
    }
}
