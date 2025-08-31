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
        public ImmutableDictionary<Vector2Int, SimPawn> pawns;
        public ImmutableList<SimPawn> dead_pawns;
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
        var pawns = ImmutableDictionary.CreateBuilder<Vector2Int, SimPawn>();
        var dead_pawns = ImmutableList.CreateBuilder<SimPawn>();
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
                dead_pawns.Add(sim_pawn);
            }
        }
        var state = new SimGameState()
        {
            turn = game_state.turn,
            pawns = pawns.ToImmutable(),
            dead_pawns = dead_pawns.ToImmutable(),
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
        foreach (var pawn in state.dead_pawns)
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
        var updated_pawns = state.pawns.ToBuilder();
        foreach (var index in state.pawns.Keys)
        {
            var pawn = updated_pawns[index];
            if (pawn.team == oppn_team && !pawn.is_revealed)
            {
                pawn.rank = arr_ranks[ix];
                updated_pawns[index] = pawn;
                ix++;
            }
        }
        state.pawns = updated_pawns.ToImmutable();
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
        var final_scores = new Dictionary<SimMoveSet, float>();
        var ally_moves = GetAllSingleMovesForTeam(board, state.pawns, ally_team);
        var oppn_moves = GetAllSingleMovesForTeam(board, state.pawns, oppn_team);
        var oppn_throne = GetThronePos(state.pawns, oppn_team);
        var oppn_dead_pawns = state.dead_pawns.Select(x => x.team == oppn_team).Count();
        foreach (var ally_move in ally_moves)
        {
            float move_score_total = 0;
            foreach (var oppn_move in oppn_moves)
            {
                var move_union = SimMoveSet.Empty.Add(ally_move).Add(oppn_move);
                var substate = GetDerivedStateFromMove(board, state, move_union);
                var new_oppn_dead = substate.dead_pawns.Select(x => x.team == oppn_team).Count();
                move_score_total += substate.value;
            }
            final_scores.Add(SimMoveSet.Empty.Add(ally_move), move_score_total / oppn_moves.Count);
        }
        var arr_scores = final_scores.ToArray();
        System.Array.Sort(arr_scores, (x, y) => y.Value.CompareTo(x.Value));
        return arr_scores.Select(x => x.Key).ToList();
    }

    // Recursion with pruning attempt.

    public static List<SimGameState> NodeScoreEvalSubstate(
        SimGameBoard board,
        SimGameState state,
        int depth)
    {
        var ally_team = board.ally_team;
        var oppn_team = ally_team == Team.RED ? Team.BLUE : Team.RED;
        var ally_scores = new Dictionary<SimMove, SimAverage>();
        var oppn_scores = new Dictionary<SimMove, SimAverage>();
        var ally_moves = GetAllSingleMovesForTeam(board, state.pawns, ally_team);
        var oppn_moves = GetAllSingleMovesForTeam(board, state.pawns, oppn_team);
        var ally_throne = GetThronePos(state.pawns, ally_team);
        var oppn_throne = GetThronePos(state.pawns, oppn_team);
        // Collect all utility scores for each move of each team.
        foreach (var ally_move in ally_moves)
        {
            var ally_throne_nudge = DirTo(ally_move, oppn_throne);
            var ally_avg = ally_scores.GetValueOrDefault(ally_move);
            ally_avg.sum = 0;
            foreach (var oppn_move in oppn_moves)
            {
                var move_union = SimMoveSet.Empty.Add(ally_move).Add(oppn_move);
                var substate = GetDerivedStateFromMove(board, state, move_union);
                var oppn_avg = oppn_scores.GetValueOrDefault(oppn_move);
                ally_avg.sum += substate.value + ally_throne_nudge;
                ally_avg.count += 1;
                oppn_avg.sum += (-substate.value) + DirTo(oppn_move, ally_throne);
                oppn_avg.count += 1;
                oppn_scores[oppn_move] = oppn_avg;
            }
            ally_scores[ally_move] = ally_avg;
        }
        var top_ally_scores = new List<(SimMove, float)>();
        var top_oppn_scores = new List<(SimMove, float)>();
        // Average the found scores.
        foreach (var (scores, top_scores) in new[] { (ally_scores, top_ally_scores), (oppn_scores, top_oppn_scores) })
        {
            foreach (var (move, avg) in scores)
            {
                top_scores.Add((move, avg.sum / avg.count));
            }
            top_scores.Sort((x, y) => y.Item2.CompareTo(x.Item2));
        }
        // Recurse into each top substate, which we think are most likely to occur.
        // Try to get the true utility of each successive substate.
        // Not really sure what is good here, or how to make it more like minimax.
        var top_n_ally = top_ally_scores.Take(board.max_top_moves).ToArray();
        var top_n_oppn = top_oppn_scores.Take(board.max_top_moves).ToArray();
        var substates = new List<SimGameState>();
        foreach (var (ally_move, ally_score) in top_n_ally)
        {
            foreach (var (oppn_move, oppn_score) in top_n_oppn)
            {
                var move_union = SimMoveSet.Empty.Add(ally_move).Add(oppn_move);
                var substate = GetDerivedStateFromMove(board, state, move_union);
                substate.ally_single_move = ally_move;
                if (depth == 0 || substate.terminal)
                {
                    //substate.value = ally_score;
                    substate.value = ally_score + oppn_score;
                }
                else
                {
                    var next_substates = NodeScoreEvalSubstate(board, substate, depth - 1);
                    if (next_substates.Count > 0)
                    {
                        substate.value += next_substates[0].value;
                    }
                }
                substates.Add(substate);
            }
        }
        substates.Sort((x, y) => y.value.CompareTo(x.value));
        return substates;
    }

    public static List<SimMoveSet> NodeScoreStrategy2(
        SimGameBoard board,
        SimGameState state,
        int depth)
    {
        var move = SimMoveSet.Empty.Add(NodeScoreEvalSubstate(board, state, depth)[0].ally_single_move);
        return new List<SimMoveSet>() { move };
        // var substates = NodeScoreEvalSubstate(board, state, depth);
        // var ally_moves = new HashSet<SimMoveSet>();
        // foreach (var substate in substates)
        // {
        //     ally_moves.Add(SimMoveSet.Empty.Add(substate.ally_single_move));
        // }
        // return ally_moves.ToList();
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

    public static void MutMCTSRunForTime(
        SimGameBoard board,
        float seconds)
    {
        var root_state = board.root_state;
        var end_time = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < end_time)
        {
            var state = MutSelectPromisingState(board, root_state);
            state = MutExpandStateChildren(board, state);
            var result = RolloutState(board, state);
            MutBackPropagateState(state, result);
        }
    }

    public static SimMoveSet MCTSGetResult(
        SimGameBoard board)
    {
        //var substate = SelectPromisingChild(board.root_state, 0f);
        // TEST
        var all_substates = board.root_state.substates.ToList();
        all_substates.AddRange(board.root_state.unexplored_states);
        var top = all_substates.OrderBy(x => x.value).Reverse().Take(5).ToArray();
        var substate = top[0];
        Debug.Log($"TOP MOVES {board.ally_team}");
        foreach (var s in top)
        {
            Debug.Log($"STATE --- val {s.value} vis {s.visits}");
            foreach (var move in s.move)
            {
                Debug.Log($"  move {move.last_pos} {move.next_pos}");
            }
        }

        var move_set = substate == null ? SimMoveSet.Empty : substate.move;
        var filtered_set = SimMoveSet.Empty.ToBuilder();
        foreach (var move in move_set)
        {
            if (board.root_state.pawns[move.last_pos].team == board.ally_team)
            {
                filtered_set.Add(move);
            }
        }
        return filtered_set.ToImmutable();
    }

    // Find a promising leaf state to explore or expand.
    public static SimGameState MutSelectPromisingState(
        SimGameBoard board,
        SimGameState state)
    {
        while (!state.terminal && state.unexplored_states != null)
        {
            if (state.unexplored_states.Count > 0)
            {
                return MutGetNextUnexploredState(state);
            }
            else
            {
                state = SelectPromisingChild(state, board.ubc_constant);
            }
        }
        return state;
    }

    // Argmax of the UBC1 function over all instantiated child states.
    public static SimGameState SelectPromisingChild(
        SimGameState state,
        float ucbConstant)
    {
        SimGameState found = null;
        float best = Mathf.NegativeInfinity;
        foreach (var substate in state.substates)
        {
            var value = UCB1(substate, ucbConstant);
            if (value > best)
            {
                best = value;
                found = substate;
            }
        }
        return found;
    }

    // Expand child states and return a random one, or self if terminal.
    public static SimGameState MutExpandStateChildren(
        SimGameBoard board,
        SimGameState state)
    {
        if (!state.terminal && state.unexplored_states == null)
        {
            state.unexplored_states = new();
            foreach (var move in CreateRandomMoveQueue(board, state))
            {
                var substate = GetDerivedStateFromMove(board, state, move);
                MutBackPropagateState(substate, substate.value);
                state.unexplored_states.Enqueue(substate);
            }
            return MutGetNextUnexploredState(state);
        }
        else
        {
            return state;
        }
    }

    // Simulate a game from this state and produce a value representing the final evaluation.
    public static float RolloutState(
        SimGameBoard board,
        SimGameState state)
    {
        var pawns = state.pawns.ToBuilder();
        var dead_pawns = state.dead_pawns.ToBuilder();
        for (uint depth = 0; depth < board.max_sim_depth; depth++)
        {
            if (IsTerminal(board, pawns, dead_pawns))
            {
                break;
            }
            var max_moves = MaxMovesThisTurn(board, depth + state.turn);
            var red_base_moves = GetAllSingleMovesForTeam(board, pawns, Team.RED).ToArray();
            var red_move = MutCreateRandomMoveForTeam(red_base_moves, max_moves);
            var blue_base_moves = GetAllSingleMovesForTeam(board, pawns, Team.BLUE).ToArray();
            var blue_move = MutCreateRandomMoveForTeam(blue_base_moves, max_moves);
            var move = red_move.Union(blue_move);
            MutApplyMove(pawns, dead_pawns, move);
        }
        return EvaluateState(board, pawns, dead_pawns);
    }

    // Updates this state and all parent states to the root with the given value.
    public static void MutBackPropagateState(
        SimGameState state,
        float result)
    {
        while (state != null)
        {
            state.value += result;
            state.visits += 1;
            state = state.parent;
        }
    }

    // Create a queue of random moves with no more than the max moves per state allowed.
    // For 1 move turns, just generate all moves unconditionally.
    public static Queue<SimMoveSet> CreateRandomMoveQueue(
        SimGameBoard board,
        SimGameState state)
    {
        var max_moves = MaxMovesThisTurn(board, state.turn);
        var red_moves = GetAllSingleMovesForTeam(board, state.pawns, Team.RED).ToArray();
        var blue_moves = GetAllSingleMovesForTeam(board, state.pawns, Team.BLUE).ToArray();
        var result = new HashSet<SimMoveSet>();
        if (max_moves == 1)
        {
            foreach (var red in red_moves)
            {
                foreach (var blue in blue_moves)
                {
                    result.Add(SimMoveSet.Empty.Add(red).Add(blue));
                }
            }
        }
        else
        {
            for (int i = 0; i < board.max_moves_per_state; i++)
            {
                var red_move = MutCreateRandomMoveForTeam(red_moves, max_moves);
                var blue_move = MutCreateRandomMoveForTeam(blue_moves, max_moves);
                result.Add(red_move.Union(blue_move));
            }
        }
        var array_result = result.ToArray();
        MutShuffle(array_result);
        return new Queue<SimMoveSet>(array_result);
    }

    // Updates the given state to create a new child state, and returns it.
    // Converts the next unexplored move into an actual state.
    public static SimGameState MutGetNextUnexploredState(
        SimGameState state)
    {
        var derived_state = state.unexplored_states.Dequeue();
        state.substates.Add(derived_state);
        return derived_state;
    }

    // Create a new derived state from the given state and move.
    public static SimGameState GetDerivedStateFromMove(
        SimGameBoard board,
        SimGameState state,
        SimMoveSet move)
    {
        var next_pawns = state.pawns.ToBuilder();
        var next_dead_pawns = state.dead_pawns.ToBuilder();
        MutApplyMove(next_pawns, next_dead_pawns, move);
        var new_state = new SimGameState
        {
            turn = state.turn + 1,
            pawns = next_pawns.ToImmutable(),
            dead_pawns = next_dead_pawns.ToImmutable(),
            parent = state,
            move = move,
        };
        new_state.value = EvaluateState(board, new_state.pawns, new_state.dead_pawns);
        new_state.terminal = IsTerminal(board, new_state.pawns, new_state.dead_pawns);
        return new_state;
    }

    // Modify the pawns and dead pawns with the given move in place.
    // Optimized for this MCTS implementation.
    public static void MutApplyMove(
        IDictionary<Vector2Int, SimPawn> pawns,
        IList<SimPawn> dead_pawns,
        SimMoveSet moveset)
    {
        // Collect all pawns moving now.
        var moving_pawns = new Dictionary<Vector2Int, SimPawn>();
        foreach (var move in moveset)
        {
            moving_pawns[move.last_pos] = pawns[move.last_pos];
        }
        // Collect pawns into target location sets.
        var target_locations = new Dictionary<Vector2Int, HashSet<SimPawn>>();
        foreach (var move in moveset)
        {
            if (!target_locations.TryGetValue(move.next_pos, out var set))
            {
                set = new();
                target_locations[move.next_pos] = set;
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
                target_locations[move.next_pos].Add(pawn);
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
                    target_locations[move.last_pos].Add(moving_pawns[move.last_pos]);
                }
            }
        }
        var died = new Dictionary<PawnId, SimPawn>();
        // Battle or occupy each new position.
        foreach (var (target, pawn_set) in target_locations)
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
                        died[p.id] = p;
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
        foreach (var pawn in died.Values)
        {
            dead_pawns.Add(pawn);
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
        IReadOnlyList<SimPawn> dead_pawns)
    {
        var red_moves = GetAllSingleMovesForTeam(board, pawns, Team.RED);
        var blue_moves = GetAllSingleMovesForTeam(board, pawns, Team.BLUE);
        if (red_moves.IsEmpty || blue_moves.IsEmpty)
        {
            return true;
        }
        foreach (var pawn in dead_pawns)
        {
            if (pawn.rank == Rank.THRONE)
            {
                return true;
            }
        }
        return false;
    }

    // Upper Confidence Bound formula for selecting state to expand or traverse.
    public static float UCB1(SimGameState state, float ubc_constant)
    {
        return state.visits == 0 ?
            Mathf.Infinity :
            (state.value / state.visits) + ubc_constant * Mathf.Sqrt(Mathf.Log(state.parent.visits) / state.visits);
    }

    // Get a score of a state's power balance, if it's in favor of one team or the other.
    // Simple strategy:
    // Win: 1
    // Lose/Draw: 0
    // Else: Normalized material strength difference of players.
    public static float EvaluateState(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        IReadOnlyList<SimPawn> dead_pawns)
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
    public static (bool, Team) WhoWins(IReadOnlyList<SimPawn> dead_pawns)
    {
        var thrones = new List<Team>();
        foreach (var pawn in dead_pawns)
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

    // Get random move set up to the max size from the given available moves.
    // This is only for a single team.
    // Mutates moves by shuffling it.
    public static SimMoveSet MutCreateRandomMoveForTeam(
        SimMove[] moves,
        uint max_size)
    {
        if (max_size == 1)
        {
            return SimMoveSet.Empty.Add(moves[(int)Random.Range(0, moves.Length - 1)]);
        }
        MutShuffle(moves);
        var result = SimMoveSet.Empty.ToBuilder();
        foreach (var move in moves)
        {
            if (result.Count == max_size)
            {
                break;
            }
            if (IsMoveAdditionLegal(result, move))
            {
                result.Add(move);
            }
        }
        return result.ToImmutable();
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
