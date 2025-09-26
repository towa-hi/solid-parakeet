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
                // Use UI-aware mode decision so we can transition from Resolve->Move when UI reaches Final
                ClientMode newMode = ModeDecider.DecideClientMode(a.Net, a.Delta, current.Ui ?? LocalUiState.Empty);
                ClientMode oldMode = current.Mode;
                LocalUiState ui = current.Ui ?? LocalUiState.Empty;
                if (newMode != oldMode)
                {
                    // Reset local UI state on mode change, but preserve last hovered position for immediate cursor update
                    Vector2Int preservedHover = ui.HoveredPos;
                    ui = LocalUiState.Empty with { HoveredPos = preservedHover };
                    // Seed resolve data on entering resolve
                    if (newMode == ClientMode.Resolve && a.Delta.TurnResolve.HasValue)
                    {
                        ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                    }
                    GameSnapshot nextTmp = current with { Net = a.Net, Mode = newMode, Ui = ui };
                    return (nextTmp, new List<GameEvent> { new ClientModeChangedEvent(newMode, a.Net, ui) });
                }
                else if (a.Delta.TurnResolve.HasValue)
                {
                    // Attach/refresh resolve data if provided
                    ui = ui with { ResolveData = a.Delta.TurnResolve.Value };
                }
                GameSnapshot next = current with { Net = a.Net, Mode = newMode, Ui = ui };
                return (next, null);
            }
            
            default:
                return (state, null);
        }
    }
}


