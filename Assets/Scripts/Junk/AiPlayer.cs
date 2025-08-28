using System.Collections.Immutable;
using Contract;
using UnityEngine;

// A single set of pawn moves that represents one instance of one turn of play.
using SimMoveSet = System.Collections.Immutable.ImmutableHashSet<AiPlayer.SimMove>;

public static class AiPlayer
{
    // Minimal information about a pawn.
    public struct SimPawn
    {
        public Team team;
        public Rank rank;

        public Vector2Int pos;
        public bool hasMoved;
        public bool isRevealed;
        public double throneProbability;
    }

    // Describes a pawn moving from one tile to another.
    public struct SimMove
    {
        public Vector2Int lastPos;
        public Vector2Int nextPos;
    };

    // Describes the pawns and their positions before any moves are made.
    public struct SimGameState
    {
        public uint turn;
        public ImmutableDictionary<Vector2Int, SimPawn> pawns;
        public ImmutableList<SimPawn> dead_pawns;
    }

    // Describes the extents and shape of the board, and blitz rules.
    public struct SimGameBoard
    {
        public uint blitzInterval;
        public uint blitzMaxMoves;
        public uint[] maxRanks;
        public bool isHex;
        public Vector2Int size;
        public ImmutableDictionary<Vector2Int, TileState> tiles;
    }

    // Wrap elements in singleton sets.
    // (A B C) -> ((A) (B) (C))
    public static ImmutableHashSet<ImmutableHashSet<T>> MakeSingletonElements<T>(ImmutableHashSet<T> set)
    {
        var superset = ImmutableHashSet.CreateBuilder<ImmutableHashSet<T>>();
        foreach (var item in set)
        {
            superset.Add(ImmutableHashSet<T>.Empty.Add(item));
        }
        return superset.ToImmutable();
    }

    // Makes the product union of two sets of sets. Sort of like the cartesian product of sets.
    // setA = ((A) (B) (C))
    // setB = ((D) (E) (F))
    // result =
    //  ((A D) (A E) (A F)
    //   (B D) (B E) (B F)
    //   (C D) (C E) (C F))
    public static ImmutableHashSet<ImmutableHashSet<T>> MakeProductUnion<T>(
        ImmutableHashSet<ImmutableHashSet<T>> setA,
        ImmutableHashSet<ImmutableHashSet<T>> setB)
    {
        var superset = ImmutableHashSet.CreateBuilder<ImmutableHashSet<T>>();
        foreach (var subsetA in setA)
        {
            foreach (var subsetB in setB)
            {
                superset.Add(subsetA.Union(subsetB));
            }
        }
        return superset.ToImmutable();
    }

    // Get a score of a board's power balance, if it's in favor of one team or the other.
    // Can be something like the sum of all of this team alive ranks minus that of the other team's.
    // Can also factor in the distance between opposing pieces to favor aggression or defense.
    // Infinity can represent a win/lose situation.
    public static double EvaluateBoard()
    {
        return 0.0;
    }

    // Get all moves for a single pawn in isolation.
    // Assumes the rank is known.
    public static SimMoveSet GetAllMovesForPawn(
        SimGameBoard board,
        SimGameState state,
        Vector2Int pawnPos)
    {
        SimPawn pawn = state.pawns[pawnPos];
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return SimMoveSet.Empty;
        }
        var outputMoves = SimMoveSet.Empty.ToBuilder();
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

    public static bool IsBlitzTurn(SimGameBoard board, SimGameState state)
    {
        if (board.blitzInterval == 0)
        {
            return false;
        }
        return state.turn % board.blitzInterval == 0;
    }

    public static uint MaxMovesThisTurn(SimGameBoard board, SimGameState state)
    {
        return IsBlitzTurn(board, state) ? board.blitzMaxMoves : 1;
    }

    // Check if adding a move to a move set is legal:
    // - Two pawns cannot target the same tile in a turn.
    // - A pawn cannot make more than one move in a turn.
    // Assumes the move sets are already filtered for ally chain movement.
    public static bool IsMoveAdditionLegal(
        SimMoveSet moveset,
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
    // extendedMoves = ((D E) (F G))
    // baseMoves = (A B C)
    // output = ((D E A) (D E B) (D E C) (F G A) (F G B) (F G C))
    // Checks for illegal move combinations, assuming all moves are from the same team.
    public static ImmutableHashSet<SimMoveSet> CombinationMoves(
        ImmutableHashSet<SimMoveSet> extendedMoves,
        SimMoveSet baseMoves)
    {
        var newSet = ImmutableHashSet.CreateBuilder<SimMoveSet>();
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

    // Every possible singular move that each pawn can make on this team.
    // If pawn 1 can make moves (A B C)
    // and pawn 2 can make moves (D E F)
    // then the result is the union (A B C D E F).
    public static SimMoveSet GetAllMovesForTeam(
        SimGameBoard board,
        SimGameState state,
        Team team)
    {
        var moves = SimMoveSet.Empty.ToBuilder();
        foreach (var (pos, pawn) in state.pawns)
        {
            if (pawn.team == team)
            {
                moves.UnionWith(GetAllMovesForPawn(board, state, pos));
            }
        }
        return moves.ToImmutable();
    }

    // All combinations of all moves for a single team.
    public static ImmutableHashSet<SimMoveSet> GetAllCombinationMovesForTeam(
        SimGameBoard board,
        SimGameState state,
        Team team)
    {
        var baseMoves = GetAllMovesForTeam(board, state, team);
        var lastSet = MakeSingletonElements(baseMoves);
        var superset = ImmutableHashSet.CreateBuilder<SimMoveSet>();
        superset.UnionWith(lastSet);
        // Repeatedly combine the base moves with each successive level to build up blitz combos.
        for (uint i = 1; i < MaxMovesThisTurn(board, state); i++)
        {
            lastSet = CombinationMoves(lastSet, baseMoves);
            superset.UnionWith(lastSet);
        }
        return superset.ToImmutable();
    }

    // All combinations of all moves for all teams.
    public static ImmutableHashSet<SimMoveSet> GetAllMoves(
        SimGameBoard board,
        SimGameState state)
    {
        var redTeam = GetAllCombinationMovesForTeam(board, state, Team.RED);
        var blueTeam = GetAllCombinationMovesForTeam(board, state, Team.BLUE);
        return MakeProductUnion(redTeam, blueTeam);
    }

    // Create an initial simulation game state from the original game state.
    public static SimGameState MakeSimGameState(GameNetworkState gameNetworkState)
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
        };
    }

    // Create a minimal representation of the board needed from the original board parameters.
    public static SimGameBoard MakeSimeGameBoard(LobbyParameters lobbyParameters)
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
