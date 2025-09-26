using System;
using Contract;

public abstract record GameEvent;

// Setup visuals events (store-driven)
public record SetupRankSelectedEvent(Rank? OldRank, Rank? NewRank) : GameEvent;
public record SetupPendingChangedEvent(System.Collections.Generic.Dictionary<PawnId, Rank?> OldMap, System.Collections.Generic.Dictionary<PawnId, Rank?> NewMap) : GameEvent;

// Setup hover event (cursor/tool updates)
public record SetupHoverChangedEvent(UnityEngine.Vector2Int Pos, bool IsMyTurn, SetupInputTool Tool) : GameEvent;

// Movement UI events
public record MoveHoverChangedEvent(UnityEngine.Vector2Int Pos, bool IsMyTurn, MoveInputTool Tool, System.Collections.Generic.HashSet<UnityEngine.Vector2Int> Targets) : GameEvent;
public record MoveSelectionChangedEvent(UnityEngine.Vector2Int? SelectedPos, System.Collections.Generic.HashSet<UnityEngine.Vector2Int> Targets) : GameEvent;
public record MovePairsChangedEvent(System.Collections.Generic.Dictionary<PawnId, (UnityEngine.Vector2Int start, UnityEngine.Vector2Int target)> OldPairs, System.Collections.Generic.Dictionary<PawnId, (UnityEngine.Vector2Int start, UnityEngine.Vector2Int target)> NewPairs) : GameEvent;

// Resolve flow events
public record ResolveCheckpointChangedEvent(ResolveCheckpoint Checkpoint, TurnResolveDelta ResolveData, int BattleIndex, GameNetworkState Net) : GameEvent;

// Client mode changed (single authority: NetworkReducer)
public record ClientModeChangedEvent(ClientMode Mode, GameNetworkState Net, LocalUiState Ui) : GameEvent;


