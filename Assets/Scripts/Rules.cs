using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public static class Rules
{
    public static int GetSetupZone(Rank rank)
    {
        return rank switch
        {
            Rank.THRONE => 3,
            Rank.ASSASSIN => 2,
            Rank.SCOUT => 0,
            Rank.SEER => 0,
            Rank.GRUNT => 0,
            Rank.KNIGHT => 0,
            Rank.WRAITH => 0,
            Rank.REAVER => 0,
            Rank.HERALD => 0,
            Rank.CHAMPION => 2,
            Rank.WARLORD => 2,
            Rank.TRAP => 1,
            Rank.UNKNOWN => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(rank)),
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
    //
    // public static bool IsSetupValid(int targetPlayer, SLobbyParameters lobbyParameters, SSetupPawn[] setupPawns)
    // {
    //     bool isValid = true;
    //     List<string> errorMessages = new();
    //     if (setupPawns.Length == 0)
    //     {
    //         errorMessages.Add("IsSetupValid(): lobbyParameters setupPawns is empty.");
    //         isValid = false;
    //     }
    //     if (lobbyParameters.mustFillAllTiles)
    //     {
    //         foreach (STile tile in lobbyParameters.board.tiles)
    //         {
    //             if (tile.setupTeam != targetPlayer)
    //             {
    //                 continue;
    //             }
    //             bool tileIsOccupied = setupPawns.Any(setupPawn => setupPawn.pos == tile.pos);
    //             if (!tileIsOccupied)
    //             {
    //                 errorMessages.Add($"IsSetupValid(): mustFillAllTiles but tile is not filled at {tile.pos}");
    //                 isValid = false;
    //             }
    //         }
    //     }
    //     foreach (SSetupPawn pawn in setupPawns.Where(p => p.deployed))
    //     {
    //         if (!lobbyParameters.board.IsPosValid(pawn.pos))
    //         {
    //             errorMessages.Add($"IsSetupValid(): Pawn '{pawn.def.pawnName}' is on an invalid position {pawn.pos}.");
    //             isValid = false;
    //         }
    //     }
    //     if (!setupPawns.Any(pawn => pawn.deployed && pawn.def.Rank == Rank.THRONE))
    //     {
    //         errorMessages.Add("IsSetupValid(): Throne is not deployed.");
    //         isValid = false;
    //     }
    //     bool hasMovablePawns = false;
    //     foreach (SSetupPawn pawn in setupPawns.Where(p => p.deployed && p.def.movementRange > 0))
    //     {
    //         IEnumerable<(Vector2Int pos, bool isValid)> neighbors = Shared.GetNeighbors(pawn.pos, lobbyParameters.board.isHex)
    //             .Select(pos => (pos, isValid: lobbyParameters.board.IsPosValid(pos) &&
    //                                     lobbyParameters.board.GetTileByPos(pos).isPassable && setupPawns.All(other => other.pos != pos)));
    //         if (neighbors.Any(neighbor => neighbor.isValid))
    //         {
    //             hasMovablePawns = true;
    //         }
    //     }
    //     if (!hasMovablePawns)
    //     {
    //         errorMessages.Add("IsSetupValid(): No pawns with valid moves found.");
    //         isValid = false;
    //     }
    //     if (!isValid)
    //     {
    //         foreach (var error in errorMessages)
    //         {
    //             Debug.LogError(error);
    //         }
    //     }
    //     Debug.Log(isValid ? "IsSetupValid(): Setup is valid." : "IsSetupValid(): Setup is invalid.");
    //     return isValid;
    // }
}

public enum Team
{
    RED,
    BLUE,
    NONE,
}

public enum Player
{
    HOST,
    GUEST,
    NONE,
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
    UNKNOWN = 12,
}