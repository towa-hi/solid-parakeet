using System.Collections.Generic;
using Contract;
using UnityEngine;

public record LocalUiState
{
    public Vector2Int? SelectedPos { get; init; }
    public Vector2Int HoveredPos { get; init; }
    public SetupInputTool SetupTool { get; init; } = SetupInputTool.NONE;

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


