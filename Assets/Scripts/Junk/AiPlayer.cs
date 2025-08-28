using System.Collections.Generic;
using System.Collections.Immutable;
using Contract;
using UnityEngine;

public static class AiPlayer
{
    // Minimal information about a pawn.
    struct SimPawn
    {
        public Team team;
        public Rank rank;

        public Vector2Int pos;
        public bool hasMoved;
        public bool isRevealed;
        public double throneProbability;
    }

    // Describes a pawn moving from one tile to another.
    struct SimMove
    {
        public Vector2Int lastPos;
        public Vector2Int nextPos;
    };

    // Describes the pawns and their positions before any moves are made.
    struct SimGameState
    {
        public uint turn;
        public ImmutableDictionary<Vector2Int, SimPawn> pawns;
        public ImmutableList<SimPawn> dead_pawns;
    }

    // Describes the extents and shape of the board, and blitz rules.
    struct SimGameBoard
    {
        public uint blitzInterval;
        public uint blitzMaxMoves;
        public uint[] maxRanks;
        public bool isHex;
        public Vector2Int size;
        public ImmutableDictionary<Vector2Int, TileState> tiles;
    }

    // Get a score of a board's power balance, if it's in favor of one team or the other.
    // Can be something like the sum of all of this team alive ranks minus that of the other team's.
    // Can also factor in the distance between opposing pieces to favor aggression or defense.
    // Infinity can represent a win/lose situation.
    static double EvaluateBoard()
    {
        return 0.0;
    }

    // Get all moves for a single pawn in isolation.
    // Assumes the rank is known.
    static ImmutableHashSet<SimMove> GetAllMovesForPawn(
        SimGameBoard board,
        SimGameState state,
        Vector2Int pawnPos)
    {
        SimPawn pawn = state.pawns[pawnPos];
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return ImmutableHashSet<SimMove>.Empty;
        }
        var outputMoves = ImmutableHashSet<SimMove>.Empty.ToBuilder();
        int maxSteps = Rules.GetMovementRange(pawn.rank);
        Vector2Int[] initialDirections = Shared.GetDirections(pawnPos, board.isHex);
        for (int directionIndex = 0; directionIndex < initialDirections.Length; directionIndex++)
        {
            Vector2Int currentPos = pawnPos;
            int walkedTiles = 0;
            while (walkedTiles < maxSteps)
            {
                walkedTiles++;
                currentPos = currentPos + Shared.GetDirections(currentPos, board.isHex)[directionIndex];
                if (board.tiles.TryGetValue(currentPos, out TileState tile))
                {
                    if (!tile.passable)
                    {
                        break;
                    }
                    if (state.pawns.TryGetValue(currentPos, out SimPawn otherPawn))
                    {
                        // Enemy team occupied, else ally team occupied.
                        if (pawn.team != otherPawn.team)
                        {
                            outputMoves.Add(new SimMove()
                            {
                                lastPos = pawnPos,
                                nextPos = currentPos,
                            });
                        }
                        break;
                    }
                    // Unoccupied.
                    else
                    {
                        outputMoves.Add(new SimMove()
                        {
                            lastPos = pawnPos,
                            nextPos = currentPos,
                        });
                    }
                }
            }
        }
        return outputMoves.ToImmutable();
    }

    static bool IsBlitzTurn(SimGameBoard board, SimGameState state)
    {
        if (board.blitzInterval == 0)
        {
            return false;
        }
        return state.turn % board.blitzInterval == 0;
    }

    static uint MaxMovesThisTurn(SimGameBoard board, SimGameState state)
    {
        return IsBlitzTurn(board, state) ? board.blitzMaxMoves : 1;
    }

    // Check if adding a move to a move set is legal:
    // - Two pawns cannot target the same tile in a turn.
    // - A pawn cannot make more than one move in a turn.
    // Assumes the move sets are already filtered for ally chain movement.
    static bool IsMoveAdditionLegal(
        ImmutableHashSet<SimMove> moveset,
        SimMove incoming)
    {
        foreach (var move in moveset)
        {
            if (move.nextPos == incoming.nextPos || move.lastPos == incoming.lastPos)
            {
                return false;
            }
        }
        return true;
    }

    // Combines two move sets together to make a product.
    // Example:
    // baseMoves = (A B C)
    // extendedMoves = ((D E) (F G))
    // output = ((D E A) (D E B) (D E C) (F G A) (F G B) (F G C))
    // Checks for illegal move combinations, assuming all moves are from the same team.
    static ImmutableHashSet<ImmutableHashSet<SimMove>> CombinationMoves(
        ImmutableHashSet<SimMove> baseMoves,
        ImmutableHashSet<ImmutableHashSet<SimMove>> extendedMoves)
    {
        var newSet = ImmutableHashSet.CreateBuilder<ImmutableHashSet<SimMove>>();
        foreach (var baseMove in baseMoves)
        {
            foreach (var moveset in extendedMoves)
            {
                if (IsMoveAdditionLegal(moveset, baseMove))
                {
                    newSet.Add(moveset.Add(baseMove));
                }
            }
        }
        return newSet.ToImmutable();
    }

    // All combinations of all moves for a single team.
    static ImmutableHashSet<ImmutableHashSet<SimMove>> GetAllMovesForTeam(
        SimGameBoard board,
        SimGameState state,
        Team team)
    {
        var moves = ImmutableHashSet.CreateBuilder<SimMove>();
        foreach (var (pos, pawn) in state.pawns)
        {
            if (pawn.team == team)
            {
                moves.UnionWith(GetAllMovesForPawn(board, state, pos));
            }
        }
        var baseMoves = moves.ToImmutable();
        var lastSet = ImmutableHashSet<ImmutableHashSet<SimMove>>.Empty.Add(baseMoves);
        var superset = ImmutableHashSet.CreateBuilder<ImmutableHashSet<SimMove>>();
        superset.UnionWith(lastSet);
        // Repeatedly combine the base moves with each successive level to build up blitz combos.
        for (uint i = 1; i < MaxMovesThisTurn(board, state); i++)
        {
            lastSet = CombinationMoves(baseMoves, lastSet);
            superset.UnionWith(lastSet);
        }
        return superset.ToImmutable();
    }

    // All combinations of all moves for all teams.
    static ImmutableHashSet<ImmutableHashSet<SimMove>> GetAllMoves(
        SimGameBoard board,
        SimGameState state)
    {
        var redTeam = GetAllMovesForTeam(board, state, Team.RED);
        var blueTeam = GetAllMovesForTeam(board, state, Team.BLUE);
        var superset = ImmutableHashSet.CreateBuilder<ImmutableHashSet<SimMove>>();
        foreach (var redSet in redTeam)
        {
            foreach (var blueSet in blueTeam)
            {
                superset.Add(redSet.Union(blueSet));
            }
        }
        return superset.ToImmutable();
    }

    // Create an initial simulation game state from the original game state.
    static SimGameState MakeSimGameState(GameNetworkState gameNetworkState)
    {
        var pawns = ImmutableDictionary.CreateBuilder<Vector2Int, SimPawn>();
        var dead_pawns = ImmutableList.CreateBuilder<SimPawn>();
        foreach (var pawn in gameNetworkState.gameState.pawns)
        {
            SimPawn simPawn = new()
            {
                team = pawn.GetTeam(),
                rank = pawn.rank.HasValue ? pawn.rank.Value : Rank.UNKNOWN,
                pos = pawn.pos,
                hasMoved = pawn.moved,
                isRevealed = pawn.zz_revealed,
                throneProbability = 0.0,
            };
            if (pawn.alive)
            {
                pawns[pawn.pos] = simPawn;
            }
            else
            {
                dead_pawns.Add(simPawn);
            }
        }
        return new SimGameState()
        {
            turn = gameNetworkState.gameState.turn,
            pawns = pawns.ToImmutable(),
            dead_pawns = dead_pawns.ToImmutable(),
        }; ;
    }

    // Create a minimal representation of the board needed from the original board parameters.
    static SimGameBoard MakeSimeGameBoard(LobbyParameters lobbyParameters)
    {
        var tiles = ImmutableDictionary.CreateBuilder<Vector2Int, TileState>();
        foreach (var tile in lobbyParameters.board.tiles)
        {
            tiles[tile.pos] = tile;
        }
        return new SimGameBoard()
        {
            blitzInterval = lobbyParameters.blitz_interval,
            blitzMaxMoves = lobbyParameters.blitz_max_simultaneous_moves,
            maxRanks = (uint[])lobbyParameters.max_ranks.Clone(),
            isHex = lobbyParameters.board.hex,
            size = lobbyParameters.board.size,
            tiles = tiles.ToImmutable(),
        };
    }
}
