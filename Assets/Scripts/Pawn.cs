using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

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
    public bool isVisibleToOpponent;

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

public struct SPawn
{
    public Guid pawnId;
    public SPawnDef? def;
    public int player;
    public SVector2Int pos;
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToOpponent;

    public SPawn(SPawnDef? inDef, int inPlayer, SVector2Int inPos, bool inIsSetup, bool inIsAlive, bool inHasMoved, bool inIsVisibleToOpponent)
    {
        pawnId = Guid.NewGuid();
        def = inDef;
        player = inPlayer;
        pos = inPos;
        isSetup = inIsSetup;
        isAlive = inIsAlive;
        hasMoved = inHasMoved;
        isVisibleToOpponent = inIsVisibleToOpponent;
    }

    public SPawn(Pawn pawn)
    {
        pawnId = pawn.pawnId;
        def = null;
        if (pawn.def != null)
        {
            def = new SPawnDef(pawn.def);
        }
        player = (int)pawn.player;
        pos = new SVector2Int(pawn.pos);
        isSetup = pawn.isSetup;
        isAlive = pawn.isAlive;
        hasMoved = pawn.hasMoved;
        isVisibleToOpponent = pawn.isVisibleToOpponent;
    }

    public SPawn Censor()
    {
        SPawn censoredPawn = new SPawn()
        {
            pawnId = pawnId,
            def = null,
            hasMoved = hasMoved,
            isAlive = isAlive,
            isSetup = isSetup,
            isVisibleToOpponent = isVisibleToOpponent,
            player = player,
            pos = pos,
        };
        return censoredPawn;
    }

    public SPawn Kill()
    {
        Debug.Assert(isAlive);
        Debug.Assert(!isSetup);
        SPawn killedPawn = new SPawn()
        {
            pawnId = pawnId,
            def = def,
            hasMoved = hasMoved,
            isAlive = false,
            isSetup = isSetup,
            isVisibleToOpponent = isVisibleToOpponent,
            player = player,
            pos = new SVector2Int(Globals.pugatory),
        };
        return killedPawn;
    }

    public SPawn Move(SVector2Int inPos)
    {
        Debug.Assert(isAlive);
        Debug.Assert(!isSetup);
        SPawn movedPawn = new SPawn()
        {
            pawnId = pawnId,
            def = def,
            hasMoved = true,
            isAlive = isAlive,
            isSetup = isSetup,
            isVisibleToOpponent = isVisibleToOpponent,
            player = player,
            pos = inPos,
        };
        return movedPawn;
    }

    public Pawn ToUnity()
    {
        Pawn pawn = new Pawn()
        {
            pawnId = pawnId,
            player = (Player)player,
            def = null,
            pos = pos.ToUnity(),
            isSetup = isSetup,
            isAlive = isAlive,
            hasMoved = hasMoved,
            isVisibleToOpponent = isVisibleToOpponent,
        };
        if (def.HasValue)
        {
            pawn.def = def.Value.ToUnity();
        }
        return pawn;
    }
}