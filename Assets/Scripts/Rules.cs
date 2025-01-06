using System;

public static class Rules
{

    public static int GetPawnBackRows(Rank pawnRank)
    {
        return pawnRank switch
        {
            Rank.THRONE => 1,
            Rank.ASSASSIN => 2,
            Rank.SCOUT => 0,
            Rank.SEER => 0,
            Rank.GRUNT => 0,
            Rank.KNIGHT => 0,
            Rank.WRAITH => 0,
            Rank.REAVER => 0,
            Rank.HERALD => 0,
            Rank.CHAMPION => 3,
            Rank.WARLORD => 3,
            Rank.TRAP => 3,
            Rank.UNKNOWN => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(pawnRank)),
        };
    }

    public static int GetMaxPawns(Rank pawnRank)
    {
        return pawnRank switch
        {
            Rank.THRONE => 1,
            Rank.ASSASSIN => 1,
            Rank.SCOUT => 8,
            Rank.SEER => 5,
            Rank.GRUNT => 4,
            Rank.KNIGHT => 4,
            Rank.WRAITH => 4,
            Rank.REAVER => 3,
            Rank.HERALD => 2,
            Rank.CHAMPION => 1,
            Rank.WARLORD => 1,
            Rank.TRAP => 6,
            Rank.UNKNOWN => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(pawnRank)),
        };
    }
    
    public static int GetPawnMovementRange(Rank pawnRank)
    {
        return pawnRank switch
        {
            Rank.THRONE => 0,
            Rank.ASSASSIN => 1,
            Rank.SCOUT => 11,
            Rank.SEER => 1,
            Rank.GRUNT => 1,
            Rank.KNIGHT => 1,
            Rank.WRAITH => 1,
            Rank.REAVER => 1,
            Rank.HERALD => 1,
            Rank.CHAMPION => 3,
            Rank.WARLORD => 3,
            Rank.TRAP => 0,
            Rank.UNKNOWN => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(pawnRank)),
        };
    }
    
    // ResolveConflict determines the outcome of a conflict. Special cases are defined here
    public static SConflictReceipt ResolveConflict(in SPawn redPawn, in SPawn bluePawn)
    {
        int redRank = redPawn.def.power;
        int blueRank = bluePawn.def.power;
        bool redDies;
        bool blueDies;
        // Handle special cases (e.g., Trap, Seer, Warlord, Assassin)
        if (redPawn.def.Rank == Rank.TRAP && bluePawn.def.Rank == Rank.SEER)
        {
            redDies = true;
            blueDies = false;
        }
        else if (bluePawn.def.Rank == Rank.TRAP && redPawn.def.Rank == Rank.SEER)
        {
            redDies = false;
            blueDies = true;
        }
        else if (redPawn.def.Rank == Rank.WARLORD && bluePawn.def.Rank == Rank.ASSASSIN)
        {
            redDies = true;
            blueDies = false;
        }
        else if (bluePawn.def.Rank == Rank.WARLORD && redPawn.def.Rank == Rank.ASSASSIN)
        {
            redDies = false;
            blueDies = true;
        }
        else if (redRank > blueRank)
        {
            redDies = false;
            blueDies = true;
        }
        else if (blueRank > redRank)
        {
            redDies = true;
            blueDies = false;
        }
        else
        {
            redDies = true;
            blueDies = true;
        }
        return new SConflictReceipt()
        {
            redPawnId = redPawn.pawnId,
            bluePawnId = bluePawn.pawnId,
            redDies = redDies,
            blueDies = blueDies,
        };
    }

    
}

public enum Player
{
    NONE,
    RED,
    BLUE,
}

public enum Rank
{
    THRONE = 0,
    ASSASSIN = 1,
    SCOUT = 2,
    SEER = 3,
    GRUNT = 4,
    KNIGHT = 5,
    WRAITH = 6,
    REAVER = 7,
    HERALD = 8,
    CHAMPION = 9,
    WARLORD = 10,
    TRAP = 11,
    UNKNOWN = 99,
}