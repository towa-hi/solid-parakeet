using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;

[System.Serializable]
public class Pawn
{
    public Guid pawnId;
    public PawnDef def;
    public Team team;
    public Vector2Int pos;
    // DEPRECATED: This field is being phased out but kept for compatibility
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToOpponent;

    public bool dirty;
    
    public Pawn()
    {
        
    }

    public Pawn(PawnDef inDef, Team inTeam, bool inIsSetup)
    {
        // spawns pawn in purgatory
        pawnId = Guid.NewGuid();
        def = inDef;
        team = inTeam;
        pos = Globals.Purgatory;
        isSetup = inIsSetup;
        isAlive = false;
        isVisibleToOpponent = false;
    }

    public void MutSetupAdd(Vector2Int inPos)
    {
        isAlive = true;
        pos = inPos;
        dirty = true;
    }

    public void MutSetupRemove()
    {
        isAlive = false;
        pos = Globals.Purgatory;
        dirty = true;
    }

    public void MutMove(Vector2Int inPos)
    {
        pos = inPos;
        dirty = true;
    }

}

[Serializable]
public struct SPawn
{
    public Guid pawnId;
    public SPawnDef def;
    public int team;
    public Vector2Int pos;
    public bool isSetup;
    public bool isAlive;
    public bool hasMoved;
    public bool isVisibleToOpponent;

    public SPawn(SSetupPawn setupPawn)
    {
        pawnId = Guid.NewGuid();
        def = setupPawn.def;
        team = setupPawn.team;
        pos = setupPawn.pos;
        isSetup = false;
        isAlive = setupPawn.deployed;
        hasMoved = false;
        isVisibleToOpponent = false;
    }

    public SPawn(Pawn pawn)
    {
        pawnId = pawn.pawnId;
        def = new SPawnDef(pawn.def);
        team = (int)pawn.team;
        pos = pawn.pos;
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
            def = new SPawnDef()
            {
                id = 99,
                pawnName = "Unknown",
                power = 0,
            },
            team = team,
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
            team = (Team)team,
            pos = pos,
            isSetup = isSetup,
            isAlive = isAlive,
            hasMoved = hasMoved,
            isVisibleToOpponent = isVisibleToOpponent,
        };
        return pawn;
    }

    public override string ToString()
    {
        string newString = $"{(Team)team} {def.pawnName} {Shared.ShortGuid(pawnId)} isAlive: {isAlive}";
        return newString;
    }
}