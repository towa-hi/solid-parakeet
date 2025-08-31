using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Contract;
using UnityEngine;
using UnityEngine.Assertions;


// A single set of pawn moves that represents one instance of one turn of play.
using SimMoveSet = System.Collections.Immutable.ImmutableHashSet<AiPlayer.SimMove>;

public static class AiPlayer
{
    // Minimal information about a pawn.
    public struct SimPawn
    {
        public PawnId id;
        public Team team;
        public Rank rank;

        public Vector2Int pos;
        public bool has_moved;
        public bool is_revealed;
        public double throne_probability;
        public bool alive;
    }

    // Describes a pawn moving from one tile to another.
    public struct SimMove
    {
        public Vector2Int last_pos;
        public Vector2Int next_pos;
    };

    // Describes the pawns and their positions before any moves are made.
    public class SimGameState
    {
        public uint turn;
        public Dictionary<Vector2Int, SimPawn> pawns;
        public Dictionary<PawnId, SimPawn> dead_pawns;
        // TODO look into separate values for each team.
        // See paper: Monte Carlo Tree Search in Simultaneous Move Games with Applications to Goofspiel
        public float value;
        public uint visits;
        public List<SimGameState> substates = new();
        public Queue<SimGameState> unexplored_states;
        public SimGameState parent;
        public bool terminal;
        // The move that produced this state.
        public SimMoveSet move;
        public SimMove ally_single_move;
    }

    // Describes the extents and shape of the board, and blitz rules.
    public class SimGameBoard
    {
        public uint blitz_interval;
        public uint blitz_max_moves;
        public uint[] max_ranks;
        public uint total_material;
        public bool is_hex;
        public Vector2Int size;
        public ImmutableDictionary<Vector2Int, TileState> tiles;
        // Adjust to control exploitation vs exploration.
        public float ubc_constant = 1.4f;
        // How many sim iterations to go before giving up finding a terminal state and returning
        // an evaluation heuristic.
        public uint max_sim_depth = 20;
        // Amount of substates to expand (because it can get out of control fast).
        public uint max_moves_per_state = 400;
        // Amount of time to search for before stopping.
        public double timeout = 10.0;
        public int max_top_moves = 3;
        public Team ally_team;
        public SimGameState root_state;
    }

    public struct SimAverage
    {
        public float sum;
        public float count;
    }

    // Create an initial simulation game state from the original game state.
    public static SimGameState MakeSimGameState(
        SimGameBoard board,
        GameState game_state)
    {
        var pawns = new Dictionary<Vector2Int, SimPawn>();
        var dead_pawns = new Dictionary<PawnId, SimPawn>();
        foreach (var pawn in game_state.pawns)
        {
            var sim_pawn = new SimPawn()
            {
                id = pawn.pawn_id,
                team = pawn.GetTeam(),
                rank = pawn.rank.HasValue ? pawn.rank.Value : Rank.UNKNOWN, // Cheating!
                pos = pawn.pos,
                has_moved = pawn.moved,
                is_revealed = pawn.zz_revealed,
                throne_probability = 0.0,
                alive = pawn.alive,
            };
            if (pawn.alive)
            {
                pawns[pawn.pos] = sim_pawn;
            }
            else
            {
                dead_pawns[sim_pawn.id] = sim_pawn;
            }
        }
        var state = new SimGameState()
        {
            turn = game_state.turn,
            pawns = pawns,
            dead_pawns = dead_pawns,
        };
        state.terminal = IsTerminal(board, state.pawns, state.dead_pawns);
        return state;
    }

    public static void MutGuessOpponentRanks(
        SimGameBoard board,
        SimGameState state)
    {
        var available_ranks = (uint[])board.max_ranks.Clone();
        var oppn_team = board.ally_team == Team.RED ? Team.BLUE : Team.RED;
        foreach (var pawn in state.dead_pawns.Values)
        {
            if (pawn.team == oppn_team)
            {
                available_ranks[(uint)pawn.rank]--;
            }
        }
        foreach (var pawn in state.pawns.Values)
        {
            if (pawn.team == oppn_team && pawn.is_revealed)
            {
                available_ranks[(uint)pawn.rank]--;
            }
        }
        List<Rank> ranks = new();
        for (int i = 0; i < (int)Rank.UNKNOWN; i++)
        {
            var count = available_ranks[i];
            for (int j = 0; j < count; j++)
            {
                ranks.Add((Rank)i);
            }
        }
        var arr_ranks = ranks.ToArray();
        MutShuffle(arr_ranks);
        int ix = 0;
        foreach (var index in state.pawns.Keys.ToList())
        {
            var pawn = state.pawns[index];
            if (pawn.team == oppn_team && !pawn.is_revealed)
            {
                pawn.rank = arr_ranks[ix];
                state.pawns[index] = pawn;
                ix++;
            }
        }
    }

    // Create a minimal representation of the board needed from the original board parameters.
    public static SimGameBoard MakeSimGameBoard(
        LobbyParameters lobby_parameters,
        GameState game_state)
    {
        var tiles = ImmutableDictionary.CreateBuilder<Vector2Int, TileState>();
        foreach (var tile in lobby_parameters.board.tiles)
        {
            tiles[tile.pos] = tile;
        }
        var board = new SimGameBoard()
        {
            blitz_interval = lobby_parameters.blitz_interval,
            blitz_max_moves = lobby_parameters.blitz_max_simultaneous_moves,
            max_ranks = (uint[])lobby_parameters.max_ranks.Clone(),
            is_hex = lobby_parameters.board.hex,
            size = lobby_parameters.board.size,
            tiles = tiles.ToImmutable(),
        };
        for (Rank i = 0; i < Rank.UNKNOWN; i++)
        {
            board.total_material += (uint)(board.max_ranks[(int)i] * (int)i);
        }
        board.root_state = MakeSimGameState(board, game_state);
        return board;
    }

    // Scores the utility of each ally move against the average outcome of each
    // opponent's possible simultaneous moves. Reduces the utility of probabilistically bad moves
    // like a pawn suiciding on another.
    // Also nudges the pawns toward the enemy throne (which can also just make it b-line the throne, for now).
    public static List<SimMoveSet> NodeScoreStrategy(
        SimGameBoard board,
        SimGameState state)
    {
        var ally_team = board.ally_team;
        var oppn_team = ally_team == Team.RED ? Team.BLUE : Team.RED;
        var _nss_start_time = Time.realtimeSinceStartupAsDouble;
        var final_scores = new Dictionary<SimMoveSet, float>();
        var ally_moves = GetAllSingleMovesForTeam(board, state.pawns, ally_team);
        var oppn_moves = GetAllSingleMovesForTeam(board, state.pawns, oppn_team);
        int first_turn_evals = 0;
        int first_turn_all_possibilities = 0;
        int second_turn_evals = 0;
        int second_turn_all_possibilities = 0;
        var changed_pawns = new Dictionary<PawnId, SimPawn>();
        var changed_pawns_2 = new Dictionary<PawnId, SimPawn>();
        var changed_pawns_3 = new Dictionary<PawnId, SimPawn>();
        var final_scores_2 = new Dictionary<SimMoveSet, float>();

        // 2 ply search for simultaenous moves
        foreach (var ally_move in ally_moves)
        {
            float move_score_total = 0;
            foreach (var oppn_move in oppn_moves)
            {
                var move_union = SimMoveSet.Empty.Add(ally_move).Add(oppn_move);
                MutApplyMove(state.pawns, state.dead_pawns, changed_pawns, move_union);
                var move_value = EvaluateState(board, state.pawns, state.dead_pawns);
                first_turn_all_possibilities++;
                // check if terminal
                if (IsTerminal(board, state.pawns, state.dead_pawns))
                {
                    move_score_total += move_value;
                }
                else
                {
                    final_scores_2.Clear();
                    //move_score_total += substate.value;
                    var ally_moves_2 = GetAllSingleMovesForTeam(board, state.pawns, ally_team);
                    var oppn_moves_2 = GetAllSingleMovesForTeam(board, state.pawns, oppn_team);
                    foreach (var ally_move_2 in ally_moves_2)
                    {
                        second_turn_all_possibilities++;
                        MutApplyMove(state.pawns, state.dead_pawns, changed_pawns_2, SimMoveSet.Empty.Add(ally_move_2));
                        var move_value_2 = EvaluateState(board, state.pawns, state.dead_pawns);
                        MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns_2);
                        if (move_value_2 >= move_value)
                        {
                            float move_score_total_2 = 0;
                            foreach (var oppn_move_2 in oppn_moves_2)
                            {
                                MutApplyMove(state.pawns, state.dead_pawns, changed_pawns_3, SimMoveSet.Empty.Add(ally_move_2).Add(oppn_move_2));
                                move_score_total_2 += EvaluateState(board, state.pawns, state.dead_pawns);
                                MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns_3);
                            }
                            final_scores_2.Add(SimMoveSet.Empty.Add(ally_move_2), move_score_total_2 / oppn_moves_2.Count);
                            second_turn_evals++;
                        }
                    }
                    move_score_total += final_scores_2.Values.Average();
                    first_turn_evals++;
                }
                MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns);
            }
            final_scores.Add(SimMoveSet.Empty.Add(ally_move), move_score_total / oppn_moves.Count);
        }

        var arr_scores = final_scores.ToArray();
        System.Array.Sort(arr_scores, (x, y) => y.Value.CompareTo(x.Value));
        var _nss_elapsed = Time.realtimeSinceStartupAsDouble - _nss_start_time;
        Debug.Log($"NodeScoreStrategy elapsed: {_nss_elapsed:F4}s, First turn evals: {first_turn_all_possibilities}/{first_turn_evals}, Second turn evals: {second_turn_all_possibilities}/{second_turn_evals}");
        return arr_scores.Select(x => x.Key).ToList();
    }

    // Greedy join moves together
    public static List<SimMoveSet> CombineMoves(
        IReadOnlyCollection<SimMoveSet> moves,
        uint max_submoves,
        uint max_moves)
    {
        var combined = new HashSet<SimMoveSet>();
        var current_subset = SimMoveSet.Empty.ToBuilder();
        for (uint i = 0; i < max_submoves; i++)
        {
            foreach (var move in moves)
            {
                if (IsMoveAdditionLegal(current_subset, move.First()))
                {
                    current_subset.Add(move.First());
                }
                if (current_subset.Count == max_submoves)
                {
                    combined.Add(current_subset.ToImmutable());
                    if (combined.Count == max_moves)
                    {
                        return combined.ToList();
                    }
                    current_subset = SimMoveSet.Empty.ToBuilder();
                }
            }
        }
        if (combined.Count == 0)
        {
            combined.Add(current_subset.ToImmutable());
        }
        return combined.ToList();
    }

    public static Vector2 V2(Vector2Int a)
    {
        return new Vector2(a.x, a.y);
    }

    public static float DirTo(SimMove move, Vector2Int pos)
    {
        var a = V2(pos - move.last_pos).normalized;
        var b = V2(pos - move.next_pos).normalized;
        return Vector2.Dot(a, b);
    }

    public static float DeltaDist(SimMove move, Vector2Int target)
    {
        return Distance(move.last_pos, target) - Distance(move.next_pos, target);
    }

    public static float Distance(Vector2Int a, Vector2Int b)
    {
        return (a - b).magnitude;
    }

    public static Vector2Int GetThronePos(
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Team team)
    {
        foreach (var (pos, pawn) in pawns)
        {
            if (pawn.team == team && pawn.rank == Rank.THRONE)
            {
                return pos;
            }
        }
        return Vector2Int.zero;
    }

    // Create a new derived state from the given state and move.
    public static SimGameState GetDerivedStateFromMove(
        SimGameBoard board,
        SimGameState state,
        SimMoveSet move)
    {
        var next_pawns = new Dictionary<Vector2Int, SimPawn>(state.pawns);
        var next_dead_pawns = new Dictionary<PawnId, SimPawn>(state.dead_pawns);
        MutApplyMove(next_pawns, next_dead_pawns, new Dictionary<PawnId, SimPawn>(), move);
        var new_state = new SimGameState
        {
            turn = state.turn + 1,
            pawns = next_pawns,
            dead_pawns = next_dead_pawns,
            parent = state,
            move = move,
        };
        new_state.value = EvaluateState(board, new_state.pawns, new_state.dead_pawns);
        new_state.terminal = IsTerminal(board, new_state.pawns, new_state.dead_pawns);
        return new_state;
    }

    public static void MutUndoApplyMove(
        IDictionary<Vector2Int, SimPawn> pawns,
        IDictionary<PawnId, SimPawn> dead_pawns,
        IDictionary<PawnId, SimPawn> changed_pawns)
    {
        foreach (var pawn in pawns.Values.ToList())
        {
            if (changed_pawns.ContainsKey(pawn.id))
            {
                pawns.Remove(pawn.pos);
            }
        }
        foreach (var (id, pawn) in changed_pawns)
        {
            dead_pawns.Remove(id);
            pawns[pawn.pos] = pawn;
        }
    }

    private static Dictionary<Vector2Int, SimPawn> mut_moving_pawns = new();
    private static Dictionary<Vector2Int, HashSet<SimPawn>> mut_target_locations = new();

    // Modify the pawns and dead pawns with the given move in place.
    // Optimized for this MCTS implementation.
    public static void MutApplyMove(
        IDictionary<Vector2Int, SimPawn> pawns,
        IDictionary<PawnId, SimPawn> dead_pawns,
        IDictionary<PawnId, SimPawn> changed_pawns,
        SimMoveSet moveset)
    {
        changed_pawns.Clear();
        foreach (var move in moveset)
        {
            var pawn = pawns[move.last_pos];
            changed_pawns[pawn.id] = pawn;
            if (pawns.TryGetValue(move.next_pos, out var next_pawn))
            {
                changed_pawns[next_pawn.id] = next_pawn;
            }
        }
        // Collect all pawns moving now.
        mut_moving_pawns.Clear();
        foreach (var move in moveset)
        {
            var pawn = pawns[move.last_pos];
            mut_moving_pawns[move.last_pos] = pawn;
        }
        // Collect pawns into target location sets.
        mut_target_locations.Clear();
        foreach (var move in moveset)
        {
            if (!mut_target_locations.TryGetValue(move.next_pos, out var set))
            {
                set = new();
                mut_target_locations[move.next_pos] = set;
            }
            set.Add(pawns[move.last_pos]);
        }
        // Remove moving pawns from board.
        foreach (var move in moveset)
        {
            pawns.Remove(move.last_pos);
        }
        // Put unmoving pawns (defenders) in the target location set.
        foreach (var move in moveset)
        {
            if (pawns.TryGetValue(move.next_pos, out var pawn))
            {
                mut_target_locations[move.next_pos].Add(pawn);
            }
        }
        // Remove unmoving targets from the board.
        foreach (var move in moveset)
        {
            pawns.Remove(move.next_pos);
        }
        // Put moving pawns at a target location that constitutes a swap.
        foreach (var move in moveset)
        {
            foreach (var othermove in moveset)
            {
                if (move.last_pos == othermove.next_pos && move.next_pos == othermove.last_pos)
                {
                    mut_target_locations[move.last_pos].Add(mut_moving_pawns[move.last_pos]);
                }
            }
        }
        // Battle or occupy each new position.
        foreach (var (target, pawn_set) in mut_target_locations)
        {
            var pawn_list = pawn_set.ToArray();
            if (pawn_list.Length == 1)
            {
                var p = pawn_list[0];
                p.pos = target;
                p.has_moved = true;
                pawns[target] = p;
            }
            else if (pawn_list.Length == 2)
            {
                var pawn_a = pawn_list[0];
                var pawn_b = pawn_list[1];
                var (winner, loser_a, loser_b) = BattlePawns(pawn_a, pawn_b);
                if (winner.HasValue)
                {
                    var p = winner.Value;
                    if (p.pos != target) { p.has_moved = true; }
                    p.is_revealed = true;
                    p.pos = target;
                    pawns[target] = p;
                }
                foreach (var pawn in new[] { loser_a, loser_b })
                {
                    if (pawn.HasValue)
                    {
                        var p = pawn.Value;
                        if (p.pos != target) { p.has_moved = true; }
                        p.pos = target;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"list {pawn_list.Length}");
                foreach (var pawn in pawn_list)
                {
                    Debug.LogWarning($"pawn {pawn.pos}");
                }
                foreach (var move in moveset)
                {
                    Debug.LogWarning($"move {move.last_pos} {move.next_pos}");
                }
                Assert.IsFalse(true, "Expected size 1 or 2 list.");
            }
        }
    }

    // Return (winner, loser A, loser B)
    public static (SimPawn?, SimPawn?, SimPawn?) BattlePawns(SimPawn a, SimPawn b)
    {
        if (a.rank == Rank.TRAP && b.rank == Rank.SEER)
        {
            return (b, a, null);
        }
        if (a.rank == Rank.SEER && b.rank == Rank.TRAP)
        {
            return (a, b, null);
        }
        if (a.rank == Rank.WARLORD && b.rank == Rank.ASSASSIN)
        {
            return (b, a, null);
        }
        if (a.rank == Rank.ASSASSIN && b.rank == Rank.WARLORD)
        {
            return (a, b, null);
        }
        if (a.rank < b.rank)
        {
            return (b, a, null);
        }
        if (a.rank > b.rank)
        {
            return (a, b, null);
        }
        return (null, a, b);
    }

    // Check if a state is terminal.
    public static bool IsTerminal(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        IReadOnlyDictionary<PawnId, SimPawn> dead_pawns)
    {
        var red_moves = GetAllSingleMovesForTeam(board, pawns, Team.RED);
        var blue_moves = GetAllSingleMovesForTeam(board, pawns, Team.BLUE);
        if (red_moves.IsEmpty || blue_moves.IsEmpty)
        {
            return true;
        }
        foreach (var pawn in dead_pawns.Values)
        {
            if (pawn.rank == Rank.THRONE)
            {
                return true;
            }
        }
        return false;
    }

    // Get a score of a state's power balance, if it's in favor of one team or the other.
    // Simple strategy:
    // Win: 1
    // Lose/Draw: 0
    // Else: Normalized material strength difference of players.
    public static float EvaluateState(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        IReadOnlyDictionary<PawnId, SimPawn> dead_pawns)
    {
        var (terminal, winner) = WhoWins(dead_pawns);
        if (terminal)
        {
            return winner == board.ally_team ? board.total_material : -board.total_material;
        }
        var (red, blue) = CountMaterial(pawns);
        var redf = (float)red;
        var bluef = (float)blue;
        var score = redf - bluef;
        if (board.ally_team == Team.BLUE)
        {
            score *= -1;
        }
        return score;
    }

    // Returns (terminal, winning team)
    public static (bool, Team) WhoWins(IReadOnlyDictionary<PawnId, SimPawn> dead_pawns)
    {
        var thrones = new List<Team>();
        foreach (var pawn in dead_pawns.Values)
        {
            if (pawn.rank == Rank.THRONE)
            {
                thrones.Add(pawn.team);
            }
        }
        if (thrones.Count == 2)
        {
            return (true, Team.NONE);
        }
        else if (thrones.Count == 1)
        {
            return (true, thrones[0] == Team.RED ? Team.BLUE : Team.RED);
        }
        return (false, Team.NONE);
    }

    // Material of (red, blue)
    public static (uint, uint) CountMaterial(IReadOnlyDictionary<Vector2Int, SimPawn> pawns)
    {
        uint red = 0;
        uint blue = 0;
        foreach (var pawn in pawns.Values)
        {
            if (pawn.team == Team.RED)
            {
                red += (uint)pawn.rank;
            }
            else
            {
                blue += (uint)pawn.rank;
            }
        }
        return (red, blue);
    }

    // Shuffle in-place.
    public static void MutShuffle<T>(T[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            int j = Random.Range(0, i + 1);
            var temp = data[i];
            data[i] = data[j];
            data[j] = temp;
        }
    }

    // Every possible singular move that each pawn can make on this team.
    // If pawn 1 can make moves (A B C)
    // and pawn 2 can make moves (D E F)
    // then the result is the union (A B C D E F).
    public static SimMoveSet GetAllSingleMovesForTeam(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Team team)
    {
        var moves = SimMoveSet.Empty.ToBuilder();
        foreach (var (pos, pawn) in pawns)
        {
            if (pawn.team == team)
            {
                moves.UnionWith(GetAllMovesForPawn(board, pawns, pos));
            }
        }
        return moves.ToImmutable();
    }

    // Get all moves for a single pawn in isolation.
    // Assumes the rank is known.
    public static SimMoveSet GetAllMovesForPawn(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Vector2Int pawn_pos)
    {
        SimPawn pawn = pawns[pawn_pos];
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return SimMoveSet.Empty;
        }
        var output_moves = SimMoveSet.Empty.ToBuilder();
        int max_steps = Rules.GetMovementRange(pawn.rank);
        Vector2Int[] initial_directions = Shared.GetDirections(pawn_pos, board.is_hex);
        for (int direction_index = 0; direction_index < initial_directions.Length; direction_index++)
        {
            Vector2Int current_pos = pawn_pos;
            int walked_tiles = 0;
            while (walked_tiles < max_steps)
            {
                walked_tiles++;
                current_pos = current_pos + Shared.GetDirections(current_pos, board.is_hex)[direction_index];
                if (board.tiles.TryGetValue(current_pos, out TileState tile))
                {
                    if (!tile.passable)
                    {
                        break;
                    }
                    if (pawns.TryGetValue(current_pos, out SimPawn other_pawn))
                    {
                        // Enemy team occupied, else ally team occupied.
                        if (pawn.team != other_pawn.team)
                        {
                            output_moves.Add(new SimMove()
                            {
                                last_pos = pawn_pos,
                                next_pos = current_pos,
                            });
                        }
                        break;
                    }
                    // Unoccupied.
                    else
                    {
                        output_moves.Add(new SimMove()
                        {
                            last_pos = pawn_pos,
                            next_pos = current_pos,
                        });
                    }
                }
            }
        }
        return output_moves.ToImmutable();
    }

    public static bool IsBlitzTurn(SimGameBoard board, uint turn)
    {
        if (board.blitz_interval == 0)
        {
            return false;
        }
        return turn % board.blitz_interval == 0;
    }

    public static uint MaxMovesThisTurn(SimGameBoard board, uint turn)
    {
        return IsBlitzTurn(board, turn) ? board.blitz_max_moves : 1;
    }

    // Check if adding a move to a move set is legal:
    // - Two pawns cannot target the same tile in a turn.
    // - A pawn cannot make more than one move in a turn.
    // Assumes the move sets are already filtered for ally chain movement.
    public static bool IsMoveAdditionLegal(
        IReadOnlyCollection<SimMove> moveset,
        SimMove incoming)
    {
        foreach (var move in moveset)
        {
            if (move.next_pos == incoming.next_pos || move.last_pos == incoming.last_pos)
            {
                return false;
            }
        }
        return true;
    }
}
