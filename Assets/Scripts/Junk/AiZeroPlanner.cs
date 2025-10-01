using System;
using Contract;
using UnityEngine;

public static class AiZeroPlanner
{
    const int MaxBoardWidth = 16;
    const int MaxBoardHeight = 16;
    const int MaxTiles = MaxBoardWidth * MaxBoardHeight;
    const int MaxPawns = 64;
    const int MaxMovesPerPawn = 16;
    const int MaxCandidateMoves = MaxPawns * MaxMovesPerPawn;
    const int MaxPlans = 128;

    static readonly AiPlayer.SimMove[] s_moveStorage = new AiPlayer.SimMove[MaxCandidateMoves + MaxPlans * MaxMovesPerPawn];
    static readonly float[] s_moveScores = new float[MaxCandidateMoves];
    static readonly int[] s_moveOrder = new int[MaxCandidateMoves];
    static readonly AiPlayer.SimMove[] s_pawnMoveScratch = new AiPlayer.SimMove[MaxMovesPerPawn];
    static readonly AiPlayer.SimMove[] s_comboScratch = new AiPlayer.SimMove[MaxMovesPerPawn];
    static readonly bool[] s_usedStarts = new bool[MaxTiles];
    static readonly bool[] s_usedTargets = new bool[MaxTiles];

    static readonly MovePlan[] s_plans = new MovePlan[MaxPlans];
    static int s_candidateCount;
    static int s_planCount;
    static int s_storageCount;

    public readonly ref struct MovePlan
    {
        readonly int offset;
        public readonly int Count;
        public readonly float Score;

        internal MovePlan(int offset, int count, float score)
        {
            this.offset = offset;
            Count = count;
            Score = score;
        }

        public ReadOnlySpan<AiPlayer.SimMove> Moves => new ReadOnlySpan<AiPlayer.SimMove>(s_moveStorage, offset, Count);

        public AiPlayer.SimMove this[int index] => s_moveStorage[offset + index];
    }

    public readonly ref struct MovePlanCollection
    {
        readonly int count;

        internal MovePlanCollection(int count)
        {
            this.count = count;
        }

        public int Count => count;

        public MovePlan this[int index] => s_plans[index];

        public Enumerator GetEnumerator() => new Enumerator(count);

        public ref struct Enumerator
        {
            readonly int count;
            int index;

            internal Enumerator(int count)
            {
                this.count = count;
                index = -1;
            }

            public MovePlan Current => s_plans[index];

            public bool MoveNext()
            {
                int next = index + 1;
                if (next >= count)
                {
                    return false;
                }
                index = next;
                return true;
            }
        }
    }

    static int EncodePosition(Vector2Int size, Vector2Int pos)
    {
        return pos.y * size.x + pos.x;
    }

    public static MovePlanCollection PlanMoves(
        AiPlayer.SimGameBoard board,
        AiPlayer.SimGameState state,
        Team team)
    {
        s_candidateCount = 0;
        s_planCount = 0;
        s_storageCount = 0;
        int boardArea = board.size.x * board.size.y;
        Array.Clear(s_usedStarts, 0, boardArea);
        Array.Clear(s_usedTargets, 0, boardArea);

        CollectCandidateMoves(board, state, team);
        if (s_candidateCount == 0)
        {
            return new MovePlanCollection(0);
        }

        SortCandidates();

        uint topLimit = board.max_top_moves > 0 ? (uint)board.max_top_moves : 1u;
        int singles = Mathf.Min((int)topLimit, s_candidateCount);
        for (int i = 0; i < singles && s_planCount < MaxPlans; i++)
        {
            int idx = s_moveOrder[i];
            var move = s_moveStorage[idx];
            int offset = AppendMoves(new ReadOnlySpan<AiPlayer.SimMove>(s_moveStorage, idx, 1));
            s_plans[s_planCount++] = new MovePlan(offset, 1, s_moveScores[idx]);
        }

        uint blitzCap = AiPlayer.MaxMovesThisTurn(board, state.turn);
        if (blitzCap > 1)
        {
            BuildCombos(board, (int)Mathf.Min(blitzCap, (uint)MaxMovesPerPawn));
        }

        int finalCount = Mathf.Min((int)topLimit, s_planCount);
        SortPlans(finalCount);
        return new MovePlanCollection(finalCount);
    }

    static void CollectCandidateMoves(
        AiPlayer.SimGameBoard board,
        AiPlayer.SimGameState state,
        Team team)
    {
        Team enemy = team == Team.RED ? Team.BLUE : Team.RED;
        bool hasEnemyThrone = TryGetThrone(state, enemy, out var enemyThrone);
        bool hasFriendlyThrone = TryGetThrone(state, team, out var friendlyThrone);

        foreach (var kv in state.pawns)
        {
            var pawn = kv.Value;
            if (!pawn.alive || pawn.team != team)
            {
                continue;
            }

            int generated = GenerateMovesForPawn(board, state, kv.Key, s_pawnMoveScratch);
            for (int i = 0; i < generated && s_candidateCount < MaxCandidateMoves; i++)
            {
                var move = s_pawnMoveScratch[i];
                s_moveStorage[s_candidateCount] = move;
                s_moveScores[s_candidateCount] = ScoreMove(state, pawn, move,
                    hasEnemyThrone ? enemyThrone : (Vector2Int?)null,
                    hasFriendlyThrone ? friendlyThrone : (Vector2Int?)null);
                s_moveOrder[s_candidateCount] = s_candidateCount;
                s_candidateCount++;
            }
        }
    }

    static bool TryGetThrone(AiPlayer.SimGameState state, Team team, out Vector2Int pos)
    {
        foreach (var kv in state.pawns)
        {
            if (kv.Value.team == team && kv.Value.rank == Rank.THRONE)
            {
                pos = kv.Key;
                return true;
            }
        }
        pos = default;
        return false;
    }

    static int GenerateMovesForPawn(
        AiPlayer.SimGameBoard board,
        AiPlayer.SimGameState state,
        Vector2Int pawnPos,
        AiPlayer.SimMove[] buffer)
    {
        if (!state.pawns.TryGetValue(pawnPos, out var pawn))
        {
            return 0;
        }
        if (pawn.rank == Rank.THRONE || pawn.rank == Rank.TRAP)
        {
            return 0;
        }

        int count = 0;
        int maxSteps = Rules.GetMovementRange(pawn.rank);
        var dirs = Shared.GetDirections(pawnPos, board.is_hex);
        for (int dir = 0; dir < dirs.Length; dir++)
        {
            Vector2Int current = pawnPos;
            int walked = 0;
            while (walked < maxSteps)
            {
                walked++;
                var deltaDirs = Shared.GetDirections(current, board.is_hex);
                current += deltaDirs[dir];
                if (!board.tiles.TryGetValue(current, out var tile) || !tile.passable)
                {
                    break;
                }
                if (state.pawns.TryGetValue(current, out var other))
                {
                    if (other.team != pawn.team)
                    {
                        buffer[count++] = new AiPlayer.SimMove { last_pos = pawnPos, next_pos = current };
                    }
                    break;
                }
                buffer[count++] = new AiPlayer.SimMove { last_pos = pawnPos, next_pos = current };
            }
        }
        return count;
    }

    static float ScoreMove(
        AiPlayer.SimGameState state,
        AiPlayer.SimPawn mover,
        AiPlayer.SimMove move,
        Vector2Int? enemyThrone,
        Vector2Int? friendlyThrone)
    {
        float score = 0f;
        if (state.pawns.TryGetValue(move.next_pos, out var target) && target.team != mover.team)
        {
            AiPlayer.BattlePawnsOut(in mover, in target,
                out var hasWinner, out var winner,
                out _, out _,
                out _, out _);
            if (hasWinner)
            {
                if (winner.team == mover.team)
                {
                    score += 8f + (float)winner.rank * 0.5f;
                    if (target.rank == Rank.THRONE)
                    {
                        score += 50f;
                    }
                }
                else
                {
                    score -= 8f;
                }
            }
            else
            {
                score -= 2f;
            }
        }
        else if (enemyThrone.HasValue)
        {
            score += Mathf.Max(0f, AiPlayer.DeltaDist(move, enemyThrone.Value)) * 1.5f;
        }

        if (friendlyThrone.HasValue)
        {
            score -= 0.5f * Mathf.Max(0f, AiPlayer.DeltaDist(move, friendlyThrone.Value));
        }

        if (!mover.is_revealed)
        {
            score += 0.25f;
        }

        return score;
    }

    static void SortCandidates()
    {
        for (int i = 0; i < s_candidateCount; i++)
        {
            int best = i;
            float bestScore = s_moveScores[s_moveOrder[best]];
            for (int j = i + 1; j < s_candidateCount; j++)
            {
                float score = s_moveScores[s_moveOrder[j]];
                if (score > bestScore)
                {
                    best = j;
                    bestScore = score;
                }
            }
            if (best != i)
            {
                (s_moveOrder[i], s_moveOrder[best]) = (s_moveOrder[best], s_moveOrder[i]);
            }
        }
    }

    static void SortPlans(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int best = i;
            float bestScore = s_plans[best].Score;
            for (int j = i + 1; j < count; j++)
            {
                float score = s_plans[j].Score;
                if (score > bestScore)
                {
                    best = j;
                    bestScore = score;
                }
            }
            if (best != i)
            {
                (s_plans[i], s_plans[best]) = (s_plans[best], s_plans[i]);
            }
        }
    }

    static void BuildCombos(
        AiPlayer.SimGameBoard board,
        int maxDepth)
    {
        for (int i = 0; i < s_candidateCount && s_planCount < MaxPlans; i++)
        {
            int idx = s_moveOrder[i];
            var move = s_moveStorage[idx];
            int startIndex = EncodePosition(board.size, move.last_pos);
            int targetIndex = EncodePosition(board.size, move.next_pos);
            if (s_usedStarts[startIndex] || s_usedTargets[targetIndex])
            {
                continue;
            }

            s_comboScratch[0] = move;
            s_usedStarts[startIndex] = true;
            s_usedTargets[targetIndex] = true;
            RecurseCombo(board, maxDepth, 1, s_moveScores[idx]);
            s_usedStarts[startIndex] = false;
            s_usedTargets[targetIndex] = false;
        }
    }

    static void RecurseCombo(
        AiPlayer.SimGameBoard board,
        int maxDepth,
        int depth,
        float baseScore)
    {
        int offset = AppendMoves(new ReadOnlySpan<AiPlayer.SimMove>(s_comboScratch, 0, depth));
        if (s_planCount < MaxPlans)
        {
            s_plans[s_planCount++] = new MovePlan(offset, depth, baseScore + depth);
        }

        if (depth >= maxDepth)
        {
            return;
        }

        for (int i = 0; i < s_candidateCount && s_planCount < MaxPlans; i++)
        {
            int idx = s_moveOrder[i];
            var move = s_moveStorage[idx];
            int startIndex = EncodePosition(board.size, move.last_pos);
            int targetIndex = EncodePosition(board.size, move.next_pos);
            if (s_usedStarts[startIndex] || s_usedTargets[targetIndex])
            {
                continue;
            }

            if (!IsMoveAdditionLegal(depth, move))
            {
                continue;
            }

            s_comboScratch[depth] = move;
            s_usedStarts[startIndex] = true;
            s_usedTargets[targetIndex] = true;
            RecurseCombo(board, maxDepth, depth + 1, baseScore + s_moveScores[idx]);
            s_usedStarts[startIndex] = false;
            s_usedTargets[targetIndex] = false;
        }
    }

    static int AppendMoves(ReadOnlySpan<AiPlayer.SimMove> moves)
    {
        int offset = s_candidateCount + s_storageCount;
        for (int i = 0; i < moves.Length; i++)
        {
            s_moveStorage[offset + i] = moves[i];
        }
        s_storageCount += moves.Length;
        return offset;
    }

    static bool IsMoveAdditionLegal(int depth, AiPlayer.SimMove move)
    {
        for (int i = 0; i < depth; i++)
        {
            if (s_comboScratch[i].next_pos == move.next_pos || s_comboScratch[i].last_pos == move.last_pos)
            {
                return false;
            }
        }
        return true;
    }
}
