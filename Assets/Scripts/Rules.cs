using System;

public static class Rules
{

    public static int GetPawnBackRows(int pawnId)
    {
        return pawnId switch
        {
            0 => 1,      // Throne goes in the back row
            1 => 2,      // Assassin goes somewhere in the two furthest back rows
            2 => 0,
            3 => 0,
            4 => 0,
            5 => 0,
            6 => 0,
            7 => 0,
            8 => 0,
            9 => 3,     // Champion in three furthest back rows
            10 => 3,    // Warlord in three furthest back rows
            11 => 3,    // Trap in three furthest back rows
            99 => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(pawnId)),
        };
    }

    public static int GetMaxPawns(int pawnId)
    {
        return pawnId switch
        {
            0 => 1,
            1 => 1,
            2 => 8,
            3 => 5,
            4 => 4,
            5 => 4,
            6 => 4,
            7 => 3,
            8 => 2,
            9 => 1,
            10 => 1,
            11 => 6,
            99 => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(pawnId)),
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
        if (redPawn.def.pawnName == "Trap" && bluePawn.def.pawnName == "Seer")
        {
            redDies = true;
            blueDies = false;
        }
        else if (bluePawn.def.pawnName == "Trap" && redPawn.def.pawnName == "Seer")
        {
            redDies = false;
            blueDies = true;
        }
        else if (redPawn.def.pawnName == "Warlord" && bluePawn.def.pawnName == "Assassin")
        {
            redDies = true;
            blueDies = false;
        }
        else if (bluePawn.def.pawnName == "Warlord" && redPawn.def.pawnName == "Assassin")
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
