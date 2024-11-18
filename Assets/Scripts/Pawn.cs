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
    public bool isAlive;
    
    public Pawn(PawnDef inDef, Player inPlayer, Vector2Int inPos, bool inIsSetup)
    {
        if (inPlayer == Player.NONE)
        {
            throw new Exception("Pawn cannot have player == Player.NONE");
        }

        pawnId = Guid.NewGuid();
        def = inDef;
        player = inPlayer;
        pos = inPos;
        isSetup = inIsSetup;
        isAlive = false;
    }

    public Pawn(PawnDef inDef, Player inPlayer, bool inIsSetup)
    {
        // spawns pawn in purgatory
        pawnId = Guid.NewGuid();
        player = inPlayer;
        def = inDef;
        isSetup = inIsSetup;
        SetAlive(false, null);
    }

    public void SetAlive(bool inIsAlive, Vector2Int? inPos)
    {
        isAlive = inIsAlive;
        if (inPos != null)
        {
            pos = (Vector2Int)inPos;
        }
        else
        {
            pos = Globals.pugatory;
        }
    }
}
