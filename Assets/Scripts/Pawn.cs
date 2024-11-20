using System;
using JetBrains.Annotations;
using UnityEngine;

[System.Serializable]
public class Pawn
{
    public Guid pawnId;
    [SerializeField] [CanBeNull] public PawnDef def;
    public Player player;
    public Vector2Int pos;
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToPlayer;

    public Pawn()
    {
        
    }
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
        hasMoved = false;
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

public class SPawn
{
    public Guid pawnId;
    [SerializeField] [CanBeNull] public SPawnDef def;
    public int player;
    public SVector2Int pos;
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToPlayer;

    public SPawn()
    {
        
    }

    public SPawn(SPawn copy)
    {
        pawnId = copy.pawnId;
        def = new SPawnDef(copy.def);
        player = copy.player;
        pos = copy.pos;
        isSetup = copy.isSetup;
        isAlive = copy.isAlive;
        hasMoved = copy.hasMoved;
        isVisibleToPlayer = copy.isVisibleToPlayer;
    }
    public SPawn(Pawn pawn)
    {
        pawnId = pawn.pawnId;
        if (pawn.def != null)
        {
            def = new SPawnDef(pawn.def);
        }
        player = (int)pawn.player;
        pos = new SVector2Int(pawn.pos);
        isSetup = pawn.isSetup;
        isAlive = pawn.isAlive;
        hasMoved = pawn.hasMoved;
        isVisibleToPlayer = pawn.isVisibleToPlayer;
    }

    public Pawn ToUnity()
    {
        Pawn pawn = new Pawn()
        {
            pawnId = pawnId,
            // do def later
            pos = pos.ToUnity(),
            isSetup = isSetup,
            isAlive = isAlive,
            hasMoved = hasMoved,
            isVisibleToPlayer = isVisibleToPlayer,
        };
        if (def != null)
        {
            pawn.def = def.ToUnity();
        }
        return pawn;
    }
}