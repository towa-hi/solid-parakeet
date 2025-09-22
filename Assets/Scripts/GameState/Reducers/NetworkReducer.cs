using System.Collections.Generic;
using Contract;

public sealed class NetworkReducer : IGameReducer
{
    public (GameSnapshot nextState, List<GameEvent> events) Reduce(GameSnapshot state, GameAction action)
    {
        if (action is not NetworkStateChanged a)
        {
            return (state, null);
        }
        GameSnapshot current = state ?? GameSnapshot.Empty;
        ClientMode newMode = ModeDecider.DecideClientMode(a.Net, a.Delta);
        ClientMode oldMode = current.Mode;
        LocalUiState ui = current.Ui ?? LocalUiState.Empty;
        if (newMode != oldMode)
        {
            // Reset local UI state on mode change
            ui = LocalUiState.Empty;
            // Seed resolve data on entering resolve
            if (newMode == ClientMode.Resolve && a.Delta.TurnResolve.HasValue)
            {
                ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
            }
        }
        else if (a.Delta.TurnResolve.HasValue)
        {
            // Attach/refresh resolve data if provided
            ui = ui with { ResolveData = a.Delta.TurnResolve.Value };
        }
        GameSnapshot next = current with { Net = a.Net, Mode = newMode, Ui = ui };
        // For now, emit no events; BoardManager still drives PhaseChangeSet
        return (next, null);
    }
}


