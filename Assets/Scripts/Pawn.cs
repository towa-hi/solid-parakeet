using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class Pawn
{
    public Guid pawnId;
    public PawnDef def;
    public Player player;
    public Vector2Int pos;
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToOpponent;

    public Pawn()
    {
        
    }
    
    public Pawn(PawnDef inDef, Player inPlayer, bool inIsSetup)
    {
        // spawns pawn in purgatory
        pawnId = Guid.NewGuid();
        def = inDef;
        player = inPlayer;
        pos = Globals.PURGATORY;
        isSetup = inIsSetup;
        isAlive = false;
        isVisibleToOpponent = false;
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
            pos = Globals.PURGATORY;
        }
    }
}

[Serializable]
public struct SPawn
{
    public Guid pawnId;
    public SPawnDef def;
    public int player;
    public SVector2Int pos;
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToOpponent;

    public SPawn(SSetupPawn setupPawn)
    {
        pawnId = Guid.NewGuid();
        def = setupPawn.def;
        player = setupPawn.player;
        pos = setupPawn.pos;
        isSetup = false;
        isAlive = true;
        hasMoved = false;
        isVisibleToOpponent = false;
    }
    
    public SPawn(Pawn pawn)
    {
        pawnId = pawn.pawnId;
        def = new SPawnDef(pawn.def);
        player = (int)pawn.player;
        pos = (SVector2Int)pawn.pos;
        isSetup = pawn.isSetup;
        isAlive = pawn.isAlive;
        hasMoved = pawn.hasMoved;
        isVisibleToOpponent = pawn.isVisibleToOpponent;
    }

    public readonly SPawn Censor()
    {
        SPawn censoredPawn = new()
        {
            pawnId = pawnId,
            def = new SPawnDef("Unknown", 0),
            player = player,
            pos = pos,
            isSetup = isSetup,
            isAlive = isAlive,
            hasMoved = hasMoved,
            isVisibleToOpponent = isVisibleToOpponent,
        };
        return censoredPawn;
    }
    
    public readonly Pawn ToUnity()
    {
        Pawn pawn = new()
        {
            pawnId = pawnId,
            def = def.ToUnity(),
            player = (Player)player,
            pos = pos.ToUnity(),
            isSetup = isSetup,
            isAlive = isAlive,
            hasMoved = hasMoved,
            isVisibleToOpponent = isVisibleToOpponent,
        };
        return pawn;
    }

    public override string ToString()
    {
        string newString = $"{(Player)player} {def.pawnName} {Globals.ShortGuid(pawnId)} isAlive: {isAlive}";
        return newString;
    }
}