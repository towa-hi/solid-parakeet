using System.Collections.Generic;
using Contract;
using UnityEngine;

public record LocalUiState
{
    public Vector2Int? SelectedPos { get; init; }
    public Vector2Int HoveredPos { get; init; }
    // Info while a non-polling network request is inflight; null when idle
    public UiWaitingForResponseData WaitingForResponse { get; init; }

    // Move planning
    public Dictionary<PawnId, (Vector2Int start, Vector2Int target)> MovePairs { get; init; } = new();

    // Setup commitments
    public Dictionary<PawnId, Rank?> PendingCommits { get; init; } = new();
    public Rank? SelectedRank { get; init; }

    // Resolve stepping
    public ResolveCheckpoint Checkpoint { get; init; } = ResolveCheckpoint.Pre;
    public int BattleIndex { get; init; } = -1;
    public TurnResolveDelta ResolveData { get; init; }

    public static LocalUiState Empty => new LocalUiState();
}

public enum ResolveCheckpoint { Pre, PostMoves, Battle, Final }

public record UiWaitingForResponseData
{
    public GameAction Action { get; init; }
    public long TimestampMs { get; init; }
}


