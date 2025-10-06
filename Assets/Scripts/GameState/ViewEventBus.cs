using System;
using System.Collections.Generic;
using Contract;
using UnityEngine;

public static class ViewEventBus
{
    public static event Action<ClientMode, GameNetworkState, LocalUiState> OnClientModeChanged;
    public static event Action<GameSnapshot> OnStateUpdated;
    public static event Action<Vector2Int, bool> OnSetupHoverChanged;
    public static event Action<Dictionary<PawnId, Rank?>, Dictionary<PawnId, Rank?>> OnSetupPendingChanged;
    public static event Action<Rank?, Rank?> OnSetupRankSelected;
    public static event Action<UiWaitingForResponseData> OnUiWaitingForResponse;
    // Movement events (store-driven)
    public static event Action<Vector2Int, bool, HashSet<Vector2Int>> OnMoveHoverChanged;
    
    public static event Action<Vector2Int?, HashSet<Vector2Int>> OnMoveSelectionChanged;
    public static event Action<Dictionary<PawnId, (Vector2Int start, Vector2Int target)>, Dictionary<PawnId, (Vector2Int start, Vector2Int target)>> OnMovePairsChanged;
    // Resolve events (store-driven)
    public static event Action<ResolveCheckpoint, TurnResolveDelta, int, GameNetworkState> OnResolveCheckpointChanged;
    
    // Utility resolver for views needing TileView from board space
    public static Func<Vector2Int, TileView> TileViewResolver;

    public static void RaiseSetupHoverChanged(Vector2Int pos, bool isMyTurn) => OnSetupHoverChanged?.Invoke(pos, isMyTurn);
    public static void RaiseSetupPendingChanged(Dictionary<PawnId, Rank?> oldMap, Dictionary<PawnId, Rank?> newMap) => OnSetupPendingChanged?.Invoke(oldMap, newMap);
    public static void RaiseSetupRankSelected(Rank? oldRank, Rank? newRank) => OnSetupRankSelected?.Invoke(oldRank, newRank);
    
    public static void RaiseClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        UnityEngine.Debug.Log($"[ViewEventBus] Begin ClientModeChanged mode={mode}");
        OnClientModeChanged?.Invoke(mode, net, ui);
        UnityEngine.Debug.Log($"[ViewEventBus] End ClientModeChanged mode={mode}");
    }
    public static void RaiseStateUpdated(GameSnapshot snapshot)
    {
        UnityEngine.Debug.Log($"[ViewEventBus] Begin StateUpdated mode={snapshot.Mode} checkpoint={(snapshot.Ui?.Checkpoint)}");
        OnStateUpdated?.Invoke(snapshot);
        UnityEngine.Debug.Log("[ViewEventBus] End StateUpdated");
    }
    // Movement raisers
    public static void RaiseMoveHoverChanged(Vector2Int pos, bool isMyTurn, HashSet<Vector2Int> targets) => OnMoveHoverChanged?.Invoke(pos, isMyTurn, targets ?? new HashSet<Vector2Int>());
    
    public static void RaiseMoveSelectionChanged(Vector2Int? selectedPos, HashSet<Vector2Int> validTargets) => OnMoveSelectionChanged?.Invoke(selectedPos, validTargets);
    public static void RaiseMovePairsChanged(Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs) => OnMovePairsChanged?.Invoke(oldPairs, newPairs);

    public static void RaiseResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        UnityEngine.Debug.Log($"[ViewEventBus] Begin ResolveCheckpointChanged checkpoint={checkpoint} index={battleIndex}");
        OnResolveCheckpointChanged?.Invoke(checkpoint, tr, battleIndex, net);
        UnityEngine.Debug.Log($"[ViewEventBus] End ResolveCheckpointChanged checkpoint={checkpoint} index={battleIndex}");
    }
    public static void RaiseUiWaitingForResponse(UiWaitingForResponseData data) => OnUiWaitingForResponse?.Invoke(data);
}


