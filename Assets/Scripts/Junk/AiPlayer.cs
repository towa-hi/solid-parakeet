using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Linq;
using Contract;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

// A single set of pawn moves that represents one instance of one turn of play.
using SimMoveSet = System.Collections.Immutable.ImmutableHashSet<AiPlayer.SimMove>;

public static class AiPlayer
{
    static readonly ProfilerMarker AI_NodeScore = new ProfilerMarker("AI.NodeScoreStrategy");
    static readonly ProfilerMarker AI_GetAllSingleMoves = new ProfilerMarker("AI.GetAllSingleMovesForTeam");
    static readonly ProfilerMarker AI_GetAllMovesForPawn = new ProfilerMarker("AI.GetAllMovesForPawn");
    static readonly ProfilerMarker AI_ApplyMove = new ProfilerMarker("AI.MutApplyMove");
    static readonly ProfilerMarker AI_UndoMove = new ProfilerMarker("AI.MutUndoApplyMove");
    static readonly ProfilerMarker AI_Evaluate = new ProfilerMarker("AI.EvaluateState");
    static readonly ProfilerMarker AI_GetDerivedStateFromMove = new ProfilerMarker("AI.GetDerivedStateFromMove");


    public class RankEstimateData
    {
        public SimPawn pawn;
        public Dictionary<Rank, bool> is_possible;
        public Dictionary<Rank, float> probability;
        public float throne_score;

        public RankEstimateData(SimPawn inPawn)
        {
            pawn = inPawn;
            is_possible = new Dictionary<Rank, bool>();
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                if (rank == Rank.UNKNOWN)
                {
                    continue;
                }
                is_possible[rank] = true;
            }
            probability = new Dictionary<Rank, float>();
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                if (rank == Rank.UNKNOWN)
                {
                    continue;
                }
                probability[rank] = 0.0f;
            }
            throne_score = 0.0f;
        }
    }
    // Minimal information about a pawn.
    public struct SimPawn
    {
        public PawnId id;
        public Team team;
        public Rank rank;

        public Vector2Int pos;
        public bool has_moved;
        public bool has_moved_scout;
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

    static Dictionary<PawnId, Rank> EstimateRanks(SimGameBoard board, SimGameState state, Team team)
    {
        var final_estimates = new Dictionary<PawnId, Rank>();
        // Consider only pawns of the target team that are alive (unknown to us if throne or not).
        var estimates = new Dictionary<PawnId, RankEstimateData>();
        var teamPawns = state.pawns
            .Where(kv => kv.Value.team == team && kv.Value.alive)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        estimates = teamPawns.Select(kv => new RankEstimateData(kv.Value)).ToDictionary(kv => kv.pawn.id, kv => kv);
        if (teamPawns.Count == 0)
        {
            return final_estimates;
        }
        foreach (var estimate in estimates.Values)
        {
            if (estimate.pawn.has_moved_scout) {
                foreach (var key in estimate.is_possible.Keys)
                {
                    estimate.is_possible[key] = key == Rank.SCOUT;
                }
                continue;
            }
            if (estimate.pawn.has_moved) {
                foreach (var key in estimate.is_possible.Keys)
                {
                    if (key == Rank.TRAP || key == Rank.THRONE)
                    {
                        estimate.is_possible[key] = false;
                    }
                    else
                    {
                        estimate.is_possible[key] = true;
                    }
                }
            }
        }
        foreach (var estimate in estimates.Values)
        {
            if (estimate.is_possible[Rank.THRONE])
            {
                // foreach adjacent tile, if inaccessible or board edge, add 1 to throne score, if occupied by a revealed trap, add 1, if occupied by unrevealed pawn that hasnt moved, add 0.5 to throne score
                var dirs = Shared.GetDirections(estimate.pawn.pos, board.is_hex);
                foreach (var dir in dirs)
                {
                    var pos = estimate.pawn.pos + dir;
                    if (board.tiles.TryGetValue(pos, out var tile))
                    {
                        if (!tile.passable)
                        {
                            estimate.throne_score += 1;
                        }
                        else
                        {
                            if (state.pawns.TryGetValue(pos, out var adjPawn))
                            {
                                if (adjPawn.is_revealed && adjPawn.rank == Rank.TRAP)
                                {
                                    estimate.throne_score += 1;
                                }
                                else if (!adjPawn.is_revealed && !adjPawn.has_moved)
                                {
                                    estimate.throne_score += 0.5f;
                                }
                            }
                        }
                    }
                    else
                    {
                        // off board (edge)
                        estimate.throne_score += 1;
                    }
                }
            }
            
        }
        // pick the estimate with the highest throne score and set final_estimates to throne
        var throne_estimate = estimates.Values.OrderByDescending(e => e.throne_score).First();
        final_estimates[throne_estimate.pawn.id] = Rank.THRONE;
        foreach (var key in throne_estimate.is_possible.Keys)
        {
            throne_estimate.probability[key] = key == Rank.THRONE ? 1.0f : 0.0f;
        }
        // now estimate all traps that havent been revealed yet
        // if throne_estimate had adjacents, if they are unrevealed and unmoved, assume they are traps
        // if there are still traps left that we have to estimate, assign them from the highest throne estimate down until we dont have any left
        {
            int total_traps = (int)board.max_ranks[(int)Rank.TRAP];
            int revealed_traps = state.pawns.Values.Count(p => p.team == team && p.is_revealed && p.rank == Rank.TRAP);
            int dead_traps = state.dead_pawns.Values.Count(p => p.team == team && p.rank == Rank.TRAP);
            int traps_remaining = Mathf.Max(0, total_traps - revealed_traps - dead_traps);
            var assigned_traps = new HashSet<PawnId>();

            bool TryAssignTrap(in SimPawn pawn)
            {
                if (traps_remaining <= 0) return false;
                if (pawn.team != team) return false;
                if (pawn.is_revealed) return false;
                if (pawn.has_moved) return false;
                if (!pawn.alive) return false;
                if (pawn.id.Equals(throne_estimate.pawn.id)) return false;
                if (assigned_traps.Contains(pawn.id)) return false;
                if (!estimates.ContainsKey(pawn.id)) return false;

                final_estimates[pawn.id] = Rank.TRAP;
                var est = estimates[pawn.id];
                foreach (var key in est.is_possible.Keys)
                {
                    est.probability[key] = key == Rank.TRAP ? 1.0f : 0.0f;
                }
                assigned_traps.Add(pawn.id);
                traps_remaining--;
                return true;
            }

            // First, assign traps adjacent to the chosen throne
            if (traps_remaining > 0)
            {
                var dirs = Shared.GetDirections(throne_estimate.pawn.pos, board.is_hex);
                foreach (var dir in dirs)
                {
                    var pos = throne_estimate.pawn.pos + dir;
                    if (state.pawns.TryGetValue(pos, out var adjPawn))
                    {
                        TryAssignTrap(in adjPawn);
                        if (traps_remaining <= 0) break;
                    }
                }
            }

            // If still remaining, walk other high throne-score pawns and assign their adjacent unmoved, unrevealed pawns
            if (traps_remaining > 0)
            {
                foreach (var est in estimates.Values.OrderByDescending(e => e.throne_score))
                {
                    if (est.pawn.id.Equals(throne_estimate.pawn.id)) continue;
                    if (traps_remaining <= 0) break;
                    var dirs = Shared.GetDirections(est.pawn.pos, board.is_hex);
                    foreach (var dir in dirs)
                    {
                        var pos = est.pawn.pos + dir;
                        if (state.pawns.TryGetValue(pos, out var adjPawn))
                        {
                            if (TryAssignTrap(in adjPawn) && traps_remaining <= 0) break;
                        }
                    }
                }
            }
        }
        
        return final_estimates;
    }
    static List<SimMove> ally_moves = new();
    static List<SimMove> oppn_moves = new();
    static List<SimMove> ally_moves_2 = new();
    static List<SimMove> oppn_moves_2 = new();
    static List<KeyValuePair<SimMove, float>> node_scores = new();
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
        node_scores.Clear();
        ally_moves.Clear();
        GetAllSingleMovesForTeamList(board, state.pawns, ally_team, ally_moves);
        oppn_moves.Clear();
        GetAllSingleMovesForTeamList(board, state.pawns, oppn_team, oppn_moves);
        int first_turn_evals = 0;
        int first_turn_all_possibilities = 0;
        int second_turn_evals = 0;
        int second_turn_all_possibilities = 0;
        var changed_pawns = new Dictionary<PawnId, SimPawn>();
        var changed_pawns_2 = new Dictionary<PawnId, SimPawn>();
        var changed_pawns_3 = new Dictionary<PawnId, SimPawn>();
        float second_ply_sum = 0f;
        int second_ply_count = 0;
        int paths_pruned_ally_suicide = 0;
        int paths_pruned_ally_suicide_2 = 0;
        int paths_pruned_oppn_suicide = 0;
        int paths_pruned_oppn_suicide_2 = 0;
        int paths_pruned_ally_boring_2 = 0;
        int paths_pruned_oppn_boring_2 = 0;
        SimPawn ally_pawn = default;
        SimPawn oppn_pawn = default;
        AI_NodeScore.Begin();
        // 2 ply search for simultaenous moves
        foreach (var ally_move in ally_moves)
        {
            float move_score_total = 0;
            bool is_scout = state.pawns[ally_move.last_pos].rank == Rank.SCOUT;
            // check if this branch is worth evaluating at all
            if (state.pawns.ContainsKey(ally_move.next_pos))
            {
                ally_pawn = state.pawns[ally_move.last_pos];
                oppn_pawn = state.pawns[ally_move.next_pos];
                BattlePawnsOut(in ally_pawn, in oppn_pawn,
                    out var _hasW_sw, out var _W_sw,
                    out var _hasLA_sw, out var _LA_sw,
                    out var _hasLB_sw, out var _LB_sw);
                // Ally suicides only if the loser is the ally (non-tie)
                if (_hasLA_sw && !_hasLB_sw && _LA_sw.id == ally_pawn.id)
                {
                    paths_pruned_ally_suicide++;
                    continue;
                }
            }
            int oppn_moves_skipped = 0;
            // Baseline after applying only ally_move (used if all opponent replies are skipped)
            float __baseline_after_ally;
            {
                MutApplyMove(state.pawns, state.dead_pawns, changed_pawns_2, ally_move);
                __baseline_after_ally = EvaluateState(board, state.pawns, state.dead_pawns);
                MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns_2);
            }
            foreach (var oppn_move in oppn_moves)
            {
                // check to see if opponents response is intentionally suiciding without allocating
                if (state.pawns.ContainsKey(oppn_move.next_pos))
                {
                    oppn_pawn = state.pawns[oppn_move.last_pos];
                    ally_pawn = state.pawns[oppn_move.next_pos];
                    BattlePawnsOut(in ally_pawn, in oppn_pawn,
                        out var _hasW_sw, out var _W_sw,
                        out var _hasLA_sw, out var _LA_sw,
                        out var _hasLB_sw, out var _LB_sw);
                    // Opponent suicides only if the loser is the opponent (non-tie)
                    if (_hasLA_sw && !_hasLB_sw && _LA_sw.id == oppn_pawn.id)
                    {
                        paths_pruned_oppn_suicide ++;
                        oppn_moves_skipped++;
                        continue;
                    }
                }
                MutApplyMove(state.pawns, state.dead_pawns, changed_pawns, ally_move, oppn_move);
                var move_value = EvaluateState(board, state.pawns, state.dead_pawns);
                first_turn_all_possibilities++;
                // check if terminal
                if (IsTerminal(board, state.pawns, state.dead_pawns) || is_scout)
                {
                    move_score_total += move_value;
                }
                else
                {
                    second_ply_sum = 0f;
                    second_ply_count = 0;
                    //move_score_total += substate.value;
                    ally_moves_2.Clear();
                    GetAllSingleMovesForTeamList(board, state.pawns, ally_team, ally_moves_2);
                    oppn_moves_2.Clear();
                    GetAllSingleMovesForTeamList(board, state.pawns, oppn_team, oppn_moves_2);
                    int ally_moves_2_skipped = 0;
                    foreach (var ally_move_2 in ally_moves_2)
                    {
                        second_turn_all_possibilities++;
                        if (state.pawns.ContainsKey(ally_move_2.next_pos))
                        {
                            ally_pawn = state.pawns[ally_move_2.last_pos];
                            oppn_pawn = state.pawns[ally_move_2.next_pos];
                            BattlePawnsOut(in ally_pawn, in oppn_pawn,
                                out var _hasW_sw, out var _W_sw,
                                out var _hasLA_sw, out var _LA_sw,
                                out var _hasLB_sw, out var _LB_sw);
                            if (_hasLA_sw && !_hasLB_sw && _LA_sw.id == ally_pawn.id)
                            {
                                paths_pruned_ally_suicide_2++;
                                ally_moves_2_skipped++;
                                continue;
                            }
                        }
                        else
                        {
                            // paths_pruned_ally_boring_2++;
                            // continue;
                        }
                        // Baseline after applying only ally_move_2 (used if all opp replies are skipped)
                        float move_value_2;
                        MutApplyMove(state.pawns, state.dead_pawns, changed_pawns_2, ally_move_2);
                        move_value_2 = EvaluateState(board, state.pawns, state.dead_pawns);
                        MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns_2);
                        // Skip ally second moves that are strictly worse than the first-ply outcome.
                        if (move_value_2 < move_value)
                        {
                            // paths_pruned_ally_no_effect++;
                            continue;
                        }
                        float move_score_total_2 = 0f;
                        int oppn_moves_2_skipped = 0;
                        foreach (var oppn_move_2 in oppn_moves_2)
                        {
                            if (state.pawns.ContainsKey(oppn_move_2.next_pos))
                            {
                                oppn_pawn = state.pawns[oppn_move_2.last_pos];
                                ally_pawn = state.pawns[oppn_move_2.next_pos];
                                BattlePawnsOut(in ally_pawn, in oppn_pawn,
                                    out var _hasW_sw, out var _W_sw,
                                    out var _hasLA_sw, out var _LA_sw,
                                    out var _hasLB_sw, out var _LB_sw);
                                if (_hasLA_sw && !_hasLB_sw && _LA_sw.id == oppn_pawn.id)
                                {
                                    paths_pruned_oppn_suicide_2++;
                                    oppn_moves_2_skipped++;
                                    continue;
                                }
                            }
                            else
                            {
                                // paths_pruned_oppn_boring_2++;
                                // continue;
                            }
                            MutApplyMove(state.pawns, state.dead_pawns, changed_pawns_3, ally_move_2, oppn_move_2);
                            move_score_total_2 += EvaluateState(board, state.pawns, state.dead_pawns);
                            MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns_3);
                        }
                        {
                            int __considered2 = oppn_moves_2.Count - oppn_moves_2_skipped;
                            second_ply_sum += __considered2 > 0 ? (move_score_total_2 / __considered2) : move_value_2;
                        }
                        second_ply_count++;
                        second_turn_evals++;
                    }
                    move_score_total += second_ply_count > 0 ? (second_ply_sum / second_ply_count) : EvaluateState(board, state.pawns, state.dead_pawns);
                    first_turn_evals++;
                }
                MutUndoApplyMove(state.pawns, state.dead_pawns, changed_pawns);
            }
            {
                int __considered1 = oppn_moves.Count - oppn_moves_skipped;
                float __avg = __considered1 > 0 ? (move_score_total / __considered1) : __baseline_after_ally;
                node_scores.Add(new KeyValuePair<SimMove, float>(ally_move, __avg));
            }
        }

        node_scores.Sort((x, y) => y.Value.CompareTo(x.Value));
        var _nss_elapsed = Time.realtimeSinceStartupAsDouble - _nss_start_time;
        Debug.Log($"NodeScoreStrategy elapsed: {_nss_elapsed:F4}s, time per eval {_nss_elapsed / (first_turn_evals + second_turn_evals)}s, First turn evals: {first_turn_all_possibilities}/{first_turn_evals}, Second turn evals: {second_turn_all_possibilities}/{second_turn_evals}");
        Debug.Log($"NodeScoreStrategy Paths pruned ally suicide: {paths_pruned_ally_suicide}, Paths pruned oppn suicide: {paths_pruned_oppn_suicide}, Paths pruned ally suicide 2: {paths_pruned_ally_suicide_2}, Paths pruned oppn suicide 2: {paths_pruned_oppn_suicide_2}");
        Debug.Log($"NodeScoreStrategy Paths pruned ally boring 2: {paths_pruned_ally_boring_2}, Paths pruned oppn boring 2: {paths_pruned_oppn_boring_2}");
        // Build list of keys without LINQ
        var __result = new List<SimMoveSet>(node_scores.Count);
        foreach (var __kv in node_scores)
        {
            __result.Add(SimMoveSet.Empty.Add(__kv.Key));
        }
        node_scores.Clear();
        ally_moves.Clear();
        oppn_moves.Clear();
        ally_moves_2.Clear();
        oppn_moves_2.Clear();
        changed_pawns.Clear();
        changed_pawns_2.Clear();
        AI_NodeScore.End();
        return __result;
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
        AI_GetDerivedStateFromMove.Begin();
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
        AI_GetDerivedStateFromMove.End();
        return new_state;
    }

    public static void MutUndoApplyMove(
        Dictionary<Vector2Int, SimPawn> pawns,
        Dictionary<PawnId, SimPawn> dead_pawns,
        Dictionary<PawnId, SimPawn> changed_pawns)
    {
        AI_UndoMove.Begin();
        __undo_positions_len = 0;
        if (__undo_positions_arr == null || __undo_positions_arr.Length < pawns.Count)
        {
            __undo_positions_arr = new Vector2Int[pawns.Count];
        }
        foreach (var kv in pawns)
        {
            var p = kv.Value;
            if (changed_pawns.ContainsKey(p.id))
            {
                __undo_positions_arr[__undo_positions_len++] = kv.Key;
            }
        }
        for (int i = 0; i < __undo_positions_len; i++)
        {
            pawns.Remove(__undo_positions_arr[i]);
        }
        foreach (var kv in changed_pawns)
        {
            dead_pawns.Remove(kv.Key);
            pawns[kv.Value.pos] = kv.Value;
        }
        AI_UndoMove.End();
    }

    public static bool IsScoutMove(Vector2Int last_pos, Vector2Int next_pos)
    {
        // Grid-agnostic: a scout move is any move that spans more than one adjacent step.
        // Works for both square and hex offset coordinates, without needing board shape.
        var delta = next_pos - last_pos;
        return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > 1;
    }

    private static HashSet<(SimPawn, SimPawn)> mut_swaps = new();
    private static Vector2Int[] __undo_positions_arr = new Vector2Int[0];
    private static int __undo_positions_len = 0;
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
        AI_ApplyMove.Begin();
        changed_pawns.Clear();
        foreach (var move in moveset)
        {
            var pawn = pawns[move.last_pos];
            if (IsScoutMove(move.last_pos, move.next_pos))
            {
                pawn.has_moved_scout = true;
            }
            changed_pawns[pawn.id] = pawn;
            if (pawns.TryGetValue(move.next_pos, out var next_pawn))
            {
                changed_pawns[next_pawn.id] = next_pawn;
            }
            pawns[move.last_pos] = pawn;
        }
        // Collect swaps.
        mut_swaps.Clear();
        var temp_moveset = moveset;
        foreach (var move_a in moveset)
        {
            foreach (var move_b in moveset)
            {
                if (move_a.last_pos == move_b.next_pos && move_a.next_pos == move_b.last_pos)
                {
                    var pawn_a = pawns[move_a.last_pos];
                    var pawn_b = pawns[move_a.next_pos];
                    if (pawn_a.id < pawn_b.id)
                        mut_swaps.Add((pawn_a, pawn_b));
                    else
                        mut_swaps.Add((pawn_b, pawn_a));
                    temp_moveset = moveset.Remove(move_a).Remove(move_b);
                }
            }
        }
        moveset = temp_moveset;
        // Remove the swappy boys.
        foreach (var (swap_a, swap_b) in mut_swaps)
        {
            pawns.Remove(swap_a.pos);
            pawns.Remove(swap_b.pos);
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
        foreach (var (pawn_a, pawn_b) in mut_swaps)
        {
            var pawn_aa = pawn_a;
            var pawn_bb = pawn_b;
            var temp = pawn_aa.pos;
            pawn_aa.pos = pawn_bb.pos;
            pawn_bb.pos = temp;

            BattlePawnsOut(in pawn_a, in pawn_b, out var _hasW_sw, out var _W_sw, out var _hasLA_sw, out var _LA_sw, out var _hasLB_sw, out var _LB_sw);
            if (_hasW_sw)
            {
                var p = _W_sw;
                p.has_moved = true;
                p.is_revealed = true;
                pawns[p.pos] = p;
            }
            if (_hasLA_sw)
            {
                var p = _LA_sw;
                p.has_moved = true;
                p.alive = false;
                p.is_revealed = true;
                dead_pawns[p.id] = p;
            }
            if (_hasLB_sw)
            {
                var p = _LB_sw;
                p.has_moved = true;
                p.alive = false;
                p.is_revealed = true;
                dead_pawns[p.id] = p;
            }
        }
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
                BattlePawnsOut(in pawn_a, in pawn_b, out var _hasW_t, out var _W_t, out var _hasLA_t, out var _LA_t, out var _hasLB_t, out var _LB_t);
                if (_hasW_t)
                {
                    var p = _W_t;
                    if (p.pos != target) { p.has_moved = true; }
                    p.is_revealed = true;
                    p.pos = target;
                    pawns[target] = p;
                }
                if (_hasLA_t)
                {
                    var p = _LA_t;
                    if (p.pos != target) { p.has_moved = true; }
                    p.pos = target;
                    p.alive = false;
                    p.is_revealed = true;
                    dead_pawns[p.id] = p;
                }
                if (_hasLB_t)
                {
                    var p = _LB_t;
                    if (p.pos != target) { p.has_moved = true; }
                    p.pos = target;
                    p.alive = false;
                    p.is_revealed = true;
                    dead_pawns[p.id] = p;
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
                // Assert.IsFalse(true, "Expected size 1 or 2 list.");
            }
        }
        AI_ApplyMove.End();
    }

    // Optimized 2-move overload to avoid ImmutableHashSet allocations in hot paths.
    public static void MutApplyMove(
        IDictionary<Vector2Int, SimPawn> pawns,
        IDictionary<PawnId, SimPawn> dead_pawns,
        IDictionary<PawnId, SimPawn> changed_pawns,
        SimMove move_a,
        SimMove move_b)
    {
        AI_ApplyMove.Begin();
        try
        {
            if (IsScoutMove(move_a.last_pos, move_a.next_pos))
            {
                var pawn_a = pawns[move_a.last_pos];
                pawn_a.has_moved_scout = true;
                pawns[move_a.last_pos] = pawn_a;
            }
            if (IsScoutMove(move_b.last_pos, move_b.next_pos))
            {
                var pawn_b = pawns[move_b.last_pos];
                pawn_b.has_moved_scout = true;
                pawns[move_b.last_pos] = pawn_b;
            }
            changed_pawns.Clear();
            bool a_has_mover = pawns.TryGetValue(move_a.last_pos, out var a_pawn);
            bool b_has_mover = pawns.TryGetValue(move_b.last_pos, out var b_pawn);
            bool a_has_defender = pawns.TryGetValue(move_a.next_pos, out var a_defender);
            bool b_has_defender = pawns.TryGetValue(move_b.next_pos, out var b_defender);

            if (a_has_mover) changed_pawns[a_pawn.id] = a_pawn;
            if (b_has_mover) changed_pawns[b_pawn.id] = b_pawn;
            if (a_has_defender) changed_pawns[a_defender.id] = a_defender;
            if (b_has_defender) changed_pawns[b_defender.id] = b_defender;

                // Special-case: both movers target the same tile (no defender expected).
                if (move_a.next_pos == move_b.next_pos)
                {
                    // If a defender was also present, this would be a 3-way which original logic
                    // does not support (would assert). Keep behavior consistent.
                    // Assert.IsFalse(a_has_defender || b_has_defender, "3-way conflict not supported in 2-move MutApplyMove");

                    // Remove moving pawns from their last positions before resolving.
                    if (a_has_mover) pawns.Remove(move_a.last_pos);
                    if (b_has_mover) pawns.Remove(move_b.last_pos);

                    BattlePawnsOut(in a_pawn, in b_pawn,
                        out var _hasW, out var _W,
                        out var _hasLA, out var _LA,
                        out var _hasLB, out var _LB);
                    if (_hasW)
                    {
                        var p = _W;
                        if (p.pos != move_a.next_pos) { p.has_moved = true; }
                        p.is_revealed = true;
                        p.pos = move_a.next_pos; // same as move_b.next_pos
                        pawns[move_a.next_pos] = p;
                    }
                    if (_hasLA)
                    {
                        var p = _LA;
                        if (p.pos != move_a.next_pos) { p.has_moved = true; }
                        p.pos = move_a.next_pos;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                    if (_hasLB)
                    {
                        var p = _LB;
                        if (p.pos != move_a.next_pos) { p.has_moved = true; }
                        p.pos = move_a.next_pos;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                    return;
                }

            bool is_swap = a_has_mover && b_has_mover &&
                (move_a.last_pos == move_b.next_pos && move_a.next_pos == move_b.last_pos);

            // Remove moving pawns from their last positions.
            if (a_has_mover) pawns.Remove(move_a.last_pos);
            if (b_has_mover) pawns.Remove(move_b.last_pos);

            // Remove unmoving defenders from the board when applicable (not part of a swap).
            if (a_has_defender && (!is_swap || (b_has_mover && a_defender.id != b_pawn.id)))
            {
                pawns.Remove(move_a.next_pos);
            }
            if (b_has_defender && (!is_swap || (a_has_mover && b_defender.id != a_pawn.id)))
            {
                pawns.Remove(move_b.next_pos);
            }

                if (is_swap)
                {
                    BattlePawnsOut(in a_pawn, in b_pawn,
                        out var _hasW_sw, out var _W_sw,
                        out var _hasLA_sw, out var _LA_sw,
                        out var _hasLB_sw, out var _LB_sw);
                    if (_hasW_sw)
                    {
                        var p = _W_sw;
                        p.has_moved = true;
                        p.is_revealed = true;
                        // Mirror existing MutApplyMove behavior for swaps: keep winner at original position.
                        pawns[p.pos] = p;
                    }
                    if (_hasLA_sw)
                    {
                        var p = _LA_sw;
                        p.has_moved = true;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                    if (_hasLB_sw)
                    {
                        var p = _LB_sw;
                        p.has_moved = true;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                    return;
                }

            // Resolve move A.
            if (a_has_mover)
            {
                if (a_has_defender && (!b_has_mover || a_defender.id != b_pawn.id))
                {
                    BattlePawnsOut(in a_pawn, in a_defender,
                        out var _hasW_a, out var _W_a,
                        out var _hasLA_a, out var _LA_a,
                        out var _hasLB_a, out var _LB_a);
                    if (_hasW_a)
                    {
                        var p = _W_a;
                        if (p.pos != move_a.next_pos) { p.has_moved = true; }
                        p.is_revealed = true;
                        p.pos = move_a.next_pos;
                        pawns[move_a.next_pos] = p;
                    }
                    if (_hasLA_a)
                    {
                        var p = _LA_a;
                        if (p.pos != move_a.next_pos) { p.has_moved = true; }
                        p.pos = move_a.next_pos;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                    if (_hasLB_a)
                    {
                        var p = _LB_a;
                        if (p.pos != move_a.next_pos) { p.has_moved = true; }
                        p.pos = move_a.next_pos;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                }
                else
                {
                    var p = a_pawn;
                    p.pos = move_a.next_pos;
                    p.has_moved = true;
                    pawns[move_a.next_pos] = p;
                }
            }

            // Resolve move B.
            if (b_has_mover)
            {
                if (b_has_defender && (!a_has_mover || b_defender.id != a_pawn.id))
                {
                    BattlePawnsOut(in b_pawn, in b_defender,
                        out var _hasW_b, out var _W_b,
                        out var _hasLA_b, out var _LA_b,
                        out var _hasLB_b, out var _LB_b);
                    if (_hasW_b)
                    {
                        var p = _W_b;
                        if (p.pos != move_b.next_pos) { p.has_moved = true; }
                        p.is_revealed = true;
                        p.pos = move_b.next_pos;
                        pawns[move_b.next_pos] = p;
                    }
                    if (_hasLA_b)
                    {
                        var p = _LA_b;
                        if (p.pos != move_b.next_pos) { p.has_moved = true; }
                        p.pos = move_b.next_pos;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                    if (_hasLB_b)
                    {
                        var p = _LB_b;
                        if (p.pos != move_b.next_pos) { p.has_moved = true; }
                        p.pos = move_b.next_pos;
                        p.alive = false;
                        p.is_revealed = true;
                        dead_pawns[p.id] = p;
                    }
                }
                else
                {
                    var p = b_pawn;
                    p.pos = move_b.next_pos;
                    p.has_moved = true;
                    pawns[move_b.next_pos] = p;
                }
            }

        }
        finally
        {
            AI_ApplyMove.End();
        }
    }

    // Optimized 1-move overload to avoid ImmutableHashSet allocations for single pawn moves.
    public static void MutApplyMove(
        IDictionary<Vector2Int, SimPawn> pawns,
        IDictionary<PawnId, SimPawn> dead_pawns,
        IDictionary<PawnId, SimPawn> changed_pawns,
        SimMove move)
    {
        AI_ApplyMove.Begin();
        try
        {
            if (IsScoutMove(move.last_pos, move.next_pos))
            {
                var pawn = pawns[move.last_pos];
                pawn.has_moved_scout = true;
                pawns[move.last_pos] = pawn;
            }
            changed_pawns.Clear();

            if (!pawns.TryGetValue(move.last_pos, out var mover))
            {
                Debug.Log("MutApplyMove(1): missing pawn at last_pos");
                return;
            }
            changed_pawns[mover.id] = mover;

            bool has_defender = pawns.TryGetValue(move.next_pos, out var defender);
            if (has_defender)
            {
                changed_pawns[defender.id] = defender;
            }

            // Remove mover from current tile
            pawns.Remove(move.last_pos);
            // Remove defender from target tile (if any) before resolving
            if (has_defender)
            {
                pawns.Remove(move.next_pos);
            }

            if (has_defender)
            {
                BattlePawnsOut(in mover, in defender,
                    out var _hasW1, out var _W1,
                    out var _hasLA1, out var _LA1,
                    out var _hasLB1, out var _LB1);
                if (_hasW1)
                {
                    var p = _W1;
                    if (p.pos != move.next_pos) { p.has_moved = true; }
                    p.is_revealed = true;
                    p.pos = move.next_pos;
                    pawns[move.next_pos] = p;
                }
                if (_hasLA1)
                {
                    var p = _LA1;
                    if (p.pos != move.next_pos) { p.has_moved = true; }
                    p.pos = move.next_pos;
                    p.alive = false;
                    p.is_revealed = true;
                    dead_pawns[p.id] = p;
                }
                if (_hasLB1)
                {
                    var p = _LB1;
                    if (p.pos != move.next_pos) { p.has_moved = true; }
                    p.pos = move.next_pos;
                    p.alive = false;
                    p.is_revealed = true;
                    dead_pawns[p.id] = p;
                }
            }
            else
            {
                var p = mover;
                p.pos = move.next_pos;
                p.has_moved = true;
                pawns[move.next_pos] = p;
            }

        }
        finally
        {
            AI_ApplyMove.End();
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

    // Allocation-free out-parameter variant for hot paths
    public static void BattlePawnsOut(
        in SimPawn a,
        in SimPawn b,
        out bool hasWinner,
        out SimPawn winner,
        out bool hasLoserA,
        out SimPawn loserA,
        out bool hasLoserB,
        out SimPawn loserB)
    {
        hasWinner = false;
        hasLoserA = false;
        hasLoserB = false;
        winner = default;
        loserA = default;
        loserB = default;

        if (a.rank == Rank.TRAP && b.rank == Rank.SEER)
        {
            hasWinner = true; winner = b; hasLoserA = true; loserA = a; return;
        }
        if (a.rank == Rank.SEER && b.rank == Rank.TRAP)
        {
            hasWinner = true; winner = a; hasLoserA = true; loserA = b; return;
        }
        if (a.rank == Rank.WARLORD && b.rank == Rank.ASSASSIN)
        {
            hasWinner = true; winner = b; hasLoserA = true; loserA = a; return;
        }
        if (a.rank == Rank.ASSASSIN && b.rank == Rank.WARLORD)
        {
            hasWinner = true; winner = a; hasLoserA = true; loserA = b; return;
        }
        if (a.rank < b.rank)
        {
            hasWinner = true; winner = b; hasLoserA = true; loserA = a; return;
        }
        if (a.rank > b.rank)
        {
            hasWinner = true; winner = a; hasLoserA = true; loserA = b; return;
        }
        // Tie
        hasLoserA = true; loserA = a;
        hasLoserB = true; loserB = b;
    }

    // Reusable buffers to avoid allocations in IsTerminal
    static List<SimMove> __terminal_red_moves = new();
    static List<SimMove> __terminal_blue_moves = new();

    // Check if a state is terminal.
    public static bool IsTerminal(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        IReadOnlyDictionary<PawnId, SimPawn> dead_pawns)
    {
        __terminal_red_moves.Clear();
        __terminal_blue_moves.Clear();
        GetAllSingleMovesForTeamList(board, pawns, Team.RED, __terminal_red_moves);
        GetAllSingleMovesForTeamList(board, pawns, Team.BLUE, __terminal_blue_moves);
        if (__terminal_red_moves.Count == 0 || __terminal_blue_moves.Count == 0)
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
    const float material_weight = 0.5f;
    const float revealed_weight = 0.1f;
    // Get a score of a state's power balance, if it's in favor of one team or the other.
    // Simple strategy:
    // Win: 1
    // Loss: -1
    // Draw: 0
    // Else: Normalized material difference from ally perspective: (ally - oppn) / (ally + oppn).
    public static float EvaluateState(
        SimGameBoard board,
        Dictionary<Vector2Int, SimPawn> pawns,
        Dictionary<PawnId, SimPawn> dead_pawns)
    {
        AI_Evaluate.Begin();
        try
        {
            bool redThroneDead = false;
            bool blueThroneDead = false;
            if (dead_pawns is Dictionary<PawnId, SimPawn> __deadDict)
            {
                var __valEnum = __deadDict.Values.GetEnumerator();
                while (__valEnum.MoveNext())
                {
                    var pawn = __valEnum.Current;
                    if (pawn.rank != Rank.THRONE)
                    {
                        continue;
                    }
                    if (pawn.team == Team.RED)
                    {
                        redThroneDead = true;
                    }
                    else if (pawn.team == Team.BLUE)
                    {
                        blueThroneDead = true;
                    }
                    if (redThroneDead && blueThroneDead)
                    {
                        // Draw
                        return 0f;
                    }
                }
            }
            if (redThroneDead)
            {
                return board.ally_team == Team.BLUE ? 1f : -1f;
            }
            if (blueThroneDead)
            {
                return board.ally_team == Team.RED ? 1f : -1f;
            }

            float red_base = 0f;
            float blue_base = 0f;
            int red_scouts = 0;
            int blue_scouts = 0;
            int red_seers = 0;
            int blue_seers = 0;

            int red_revealed = 0;
            int blue_revealed = 0;
            int red_count = 0;
            int blue_count = 0;

            if (pawns is Dictionary<Vector2Int, SimPawn> __pawnsDict)
            {
                var __valEnum = __pawnsDict.Values.GetEnumerator();
                while (__valEnum.MoveNext())
                {
                    var pawn = __valEnum.Current;
                    if (pawn.team == Team.RED)
                    {
                        red_base += (int)pawn.rank;
                        if (pawn.rank == Rank.SCOUT) { red_scouts++; }
                        else if (pawn.rank == Rank.SEER) { red_seers++; }
                        red_count++;
                        if (pawn.is_revealed)
                        {
                            red_revealed++;
                        }
                    }
                    else
                    {
                        blue_base += (int)pawn.rank;
                        if (pawn.rank == Rank.SCOUT) { blue_scouts++; }
                        else if (pawn.rank == Rank.SEER) { blue_seers++; }
                        blue_count++;
                        if (pawn.is_revealed)
                        {
                            blue_revealed++;
                        }
                    }
                }
            }
            float red_one_scout_alive_bonus = red_scouts > 0 ? 4f : 0f;
            float red_one_seer_alive_bonus = red_seers > 0 ? 4f : 0f;
            float blue_one_scout_alive_bonus = blue_scouts > 0 ? 4f : 0f;
            float blue_one_seer_alive_bonus = blue_seers > 0 ? 4f : 0f;
            float red = red_base + red_one_scout_alive_bonus + red_one_seer_alive_bonus;
            float blue = blue_base + blue_one_scout_alive_bonus + blue_one_seer_alive_bonus;
            float ally = board.ally_team == Team.RED ? red : blue;
            float oppn = board.ally_team == Team.RED ? blue : red;
            float denom = ally + oppn;
            if (denom <= 0f)
            {
                return 0f;
            }
            float final_score = 0f;
            float material_score = (ally - oppn) / denom;
            material_score *= material_weight;
            final_score += material_score;
            int oppn_revealed = board.ally_team == Team.RED ? blue_revealed : red_revealed;
            int oppn_total = board.ally_team == Team.RED ? blue_count : red_count;
            float revealed_score = oppn_total > 0 ? (float)oppn_revealed / oppn_total : 0f;
            final_score += revealed_score * revealed_weight;
            return final_score;
        }
        finally
        {
            AI_Evaluate.End();
        }
    }

    // WhoWins inlined into EvaluateState

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
        AI_GetAllSingleMoves.Begin();
        var moves = SimMoveSet.Empty.ToBuilder();
        foreach (var (pos, pawn) in pawns)
        {
            if (pawn.team == team)
            {
                CollectMovesForTeamBuilder(board, pawns, pos, moves);
            }
        }
        AI_GetAllSingleMoves.End();
        return moves.ToImmutable();
    }

    // Allocation-lean list variant for hot call sites.
    public static List<SimMove> GetAllSingleMovesForTeamList(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Team team)
    {
        AI_GetAllSingleMoves.Begin();
        try
        {
            // Capacity hint: average of ~6 moves per pawn (tunable)
            var moves = new List<SimMove>(pawns.Count * 4);
            foreach (var kv in pawns)
            {
                var pos = kv.Key;
                var pawn = kv.Value;
                if (pawn.team == team)
                {
                    CollectMovesForPawnToList(board, pawns, pos, moves);
                }
            }
            return moves;
        }
        finally
        {
            AI_GetAllSingleMoves.End();
        }
    }

    // Non-allocating overload that fills a provided list
    public static void GetAllSingleMovesForTeamList(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Team team,
        List<SimMove> output)
    {
        AI_GetAllSingleMoves.Begin();
        try
        {
            output.Clear();
            foreach (var kv in pawns)
            {
                var pos = kv.Key;
                var pawn = kv.Value;
                if (pawn.team == team)
                {
                    CollectMovesForPawnToList(board, pawns, pos, output);
                }
            }
        }
        finally
        {
            AI_GetAllSingleMoves.End();
        }
    }

    private static void CollectMovesForTeamBuilder(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Vector2Int pawn_pos,
        System.Collections.Immutable.ImmutableHashSet<SimMove>.Builder output)
    {
        SimPawn pawn = pawns[pawn_pos];
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return;
        }
        int max_steps = Rules.GetMovementRange(pawn.rank);
        var initial_directions = Shared.GetDirections(pawn_pos, board.is_hex);
        for (int direction_index = 0; direction_index < initial_directions.Length; direction_index++)
        {
            Vector2Int current_pos = pawn_pos;
            int walked_tiles = 0;
            while (walked_tiles < max_steps)
            {
                walked_tiles++;
                var current_dirs = Shared.GetDirections(current_pos, board.is_hex);
                current_pos = current_pos + current_dirs[direction_index];
                if (board.tiles.TryGetValue(current_pos, out TileState tile))
                {
                    if (!tile.passable)
                    {
                        break;
                    }
                    if (pawns.TryGetValue(current_pos, out SimPawn other_pawn))
                    {
                        if (pawn.team != other_pawn.team)
                        {
                            output.Add(new SimMove()
                            {
                                last_pos = pawn_pos,
                                next_pos = current_pos,
                            });
                        }
                        break;
                    }
                    else
                    {
                        output.Add(new SimMove()
                        {
                            last_pos = pawn_pos,
                            next_pos = current_pos,
                        });
                    }
                }
            }
        }
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
        AI_GetAllMovesForPawn.Begin();
        var output_moves = SimMoveSet.Empty.ToBuilder();
        int max_steps = Rules.GetMovementRange(pawn.rank);
        var initial_directions = Shared.GetDirections(pawn_pos, board.is_hex);
        for (int direction_index = 0; direction_index < initial_directions.Length; direction_index++)
        {
            Vector2Int current_pos = pawn_pos;
            int walked_tiles = 0;
            while (walked_tiles < max_steps)
            {
                walked_tiles++;
                var current_dirs = Shared.GetDirections(current_pos, board.is_hex);
                current_pos = current_pos + current_dirs[direction_index];
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
        AI_GetAllMovesForPawn.End();
        return output_moves.ToImmutable();
    }

    // List variant for per-pawn moves
    public static List<SimMove> GetAllMovesForPawnList(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Vector2Int pawn_pos)
    {
        SimPawn pawn = pawns[pawn_pos];
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return new List<SimMove>(0);
        }
        AI_GetAllMovesForPawn.Begin();
        var output_moves = new List<SimMove>(8);
        int max_steps = Rules.GetMovementRange(pawn.rank);
        Vector2Int current_pos;
        var initial_directions = Shared.GetDirections(pawn_pos, board.is_hex);
        for (int direction_index = 0; direction_index < initial_directions.Length; direction_index++)
        {
            current_pos = pawn_pos;
            int walked_tiles = 0;
            while (walked_tiles < max_steps)
            {
                walked_tiles++;
                var current_dirs = Shared.GetDirections(current_pos, board.is_hex);
                current_pos = current_pos + current_dirs[direction_index];
                if (board.tiles.TryGetValue(current_pos, out TileState tile))
                {
                    if (!tile.passable)
                    {
                        break;
                    }
                    if (pawns.TryGetValue(current_pos, out SimPawn other_pawn))
                    {
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
        AI_GetAllMovesForPawn.End();
        return output_moves;
    }

    private static void CollectMovesForPawnToList(
        SimGameBoard board,
        IReadOnlyDictionary<Vector2Int, SimPawn> pawns,
        Vector2Int pawn_pos,
        List<SimMove> output)
    {
        SimPawn pawn = pawns[pawn_pos];
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return;
        }
        int max_steps = Rules.GetMovementRange(pawn.rank);
        Vector2Int current_pos;
        var initial_directions = Shared.GetDirections(pawn_pos, board.is_hex);
        for (int direction_index = 0; direction_index < initial_directions.Length; direction_index++)
        {
            current_pos = pawn_pos;
            int walked_tiles = 0;
            while (walked_tiles < max_steps)
            {
                walked_tiles++;
                var current_dirs = Shared.GetDirections(current_pos, board.is_hex);
                current_pos = current_pos + current_dirs[direction_index];
                if (board.tiles.TryGetValue(current_pos, out TileState tile))
                {
                    if (!tile.passable)
                    {
                        break;
                    }
                    if (pawns.TryGetValue(current_pos, out SimPawn other_pawn))
                    {
                        if (pawn.team != other_pawn.team)
                        {
                            output.Add(new SimMove()
                            {
                                last_pos = pawn_pos,
                                next_pos = current_pos,
                            });
                        }
                        break;
                    }
                    else
                    {
                        output.Add(new SimMove()
                        {
                            last_pos = pawn_pos,
                            next_pos = current_pos,
                        });
                    }
                }
            }
        }
    }

    // Removed cached direction helper; using Shared.GetDirections which returns static arrays.

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
