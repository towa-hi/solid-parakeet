using System.Collections.Generic;
using Contract;

public sealed class ResolveReducer : IGameReducer
{
    public (GameSnapshot nextState, List<GameEvent> events) Reduce(GameSnapshot state, GameAction action)
    {
        if (state == null) state = GameSnapshot.Empty;
        LocalUiState ui = state.Ui ?? LocalUiState.Empty;
        switch (action)
        {
            case NetworkStateChanged a when a.Delta.TurnResolve.HasValue:
                ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ResolveCheckpoint.Pre, ui.ResolveData, -1, a.Net)});
            case ResolvePrev:
            {
                ui = ui with { Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net)});
            }
            case ResolveNext:
            {
                // If already at Final, advance to next mode and notify views
                if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    ClientMode nextMode = ModeDecider.DecideClientMode(state.Net, default, ui);
                    LocalUiState ui2 = LocalUiState.Empty with { HoveredPos = ui.HoveredPos };
                    GameSnapshot s2 = state with { Ui = ui2, Mode = nextMode };
                    return (s2, new List<GameEvent>{ new ClientModeChangedEvent(nextMode, s2.Net, ui2)});
                }
                if (ui.Checkpoint == ResolveCheckpoint.Pre)
                {
                    ui = ui with { Checkpoint = ResolveCheckpoint.PostMoves };
                }
                else if (ui.Checkpoint == ResolveCheckpoint.PostMoves)
                {
                    bool hasBattles = (ui.ResolveData.battles?.Length ?? 0) > 0;
                    ui = hasBattles
                        ? ui with { Checkpoint = ResolveCheckpoint.Battle, BattleIndex = 0 }
                        : ui with { Checkpoint = ResolveCheckpoint.Final };
                }
                else if (ui.Checkpoint == ResolveCheckpoint.Battle)
                {
                    int next = ui.BattleIndex + 1;
                    int total = ui.ResolveData.battles?.Length ?? 0;
                    ui = next < total
                        ? ui with { BattleIndex = next }
                        : ui with { Checkpoint = ResolveCheckpoint.Final };
                }
                return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net)});
            }
            case ResolveSkip:
                if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    ClientMode nextMode = ModeDecider.DecideClientMode(state.Net, default, ui);
                    LocalUiState ui2 = LocalUiState.Empty with { HoveredPos = ui.HoveredPos };
                    GameSnapshot s2 = state with { Ui = ui2, Mode = nextMode };
                    return (s2, new List<GameEvent>{ new ClientModeChangedEvent(nextMode, s2.Net, ui2)});
                }
                else
                {
                    ui = ui with { Checkpoint = ResolveCheckpoint.Final };
                    return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net)});
                }
            default:
                return (state, null);
        }
    }
}


