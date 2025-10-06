using System.Collections.Generic;
using Contract;
using UnityEngine;

public sealed class NetworkReducer : IGameReducer
{
    public (GameSnapshot nextState, List<GameEvent> events) Reduce(GameSnapshot state, GameAction action)
    {
        GameSnapshot current = state ?? GameSnapshot.Empty;
        switch (action)
        {
            case NetworkStateChanged a:
            {
                Debug.Log($"[NetworkReducer] NetworkStateChanged: phase={a.Net.lobbyInfo.phase} sub={a.Net.lobbyInfo.subphase} turn={a.Net.gameState.turn} hasResolve={a.Delta.TurnResolve.HasValue}");
                // Use UI-aware mode decision so we can transition from Resolve->Move when UI reaches Final
                ClientMode newMode = ModeDecider.DecideClientMode(a.Net, a.Delta, current.Ui ?? LocalUiState.Empty);
                ClientMode oldMode = current.Mode;
                LocalUiState ui = current.Ui ?? LocalUiState.Empty;
                Debug.Log($"[NetworkReducer] Mode decision: old={oldMode} new={newMode} uiCheckpoint={(ui.Checkpoint)} battleIndex={(ui.BattleIndex)}");
                if (newMode != oldMode)
                {
                    // Reset local UI state on mode change, but preserve last hovered position for immediate cursor update
                    Vector2Int preservedHover = ui.HoveredPos;
                    ui = LocalUiState.Empty with { HoveredPos = preservedHover };
                    // Seed resolve data on entering resolve
                    if (newMode == ClientMode.Resolve && a.Delta.TurnResolve.HasValue)
                    {
                        ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                        Debug.Log($"[NetworkReducer] Enter Resolve: seed checkpoint=Pre battles={(ui.ResolveData.battles?.Length ?? 0)} moves={(ui.ResolveData.moves?.Count ?? 0)}");
                    }
                    GameSnapshot nextTmp = current with { Net = a.Net, Mode = newMode, Ui = ui };
                    Debug.Log($"[NetworkReducer] Emitting ClientModeChangedEvent: mode={newMode}");
                    return (nextTmp, new List<GameEvent> { new ClientModeChangedEvent(nextTmp) });
                }
                else if (a.Delta.TurnResolve.HasValue)
                {
                    // Attach/refresh resolve data if provided
                    ui = ui with { ResolveData = a.Delta.TurnResolve.Value };
                    Debug.Log($"[NetworkReducer] Refresh ResolveData without mode change");
                }
                GameSnapshot next = current with { Net = a.Net, Mode = newMode, Ui = ui };
                return (next, null);
            }
            
            default:
                return (state, null);
        }
    }
}


