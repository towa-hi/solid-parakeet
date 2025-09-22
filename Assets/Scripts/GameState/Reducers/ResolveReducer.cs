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
                // Capture resolve data upon receiving it and notify views to apply Pre checkpoint immediately
                ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                ViewEventBus.RaiseResolveCheckpointChanged(ResolveCheckpoint.Pre, ui.ResolveData, -1, a.Net);
                return (state with { Ui = ui }, null);
            case ResolvePrev:
            {
                // Always jump straight back to Pre
                ui = ui with { Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                ViewEventBus.RaiseResolveCheckpointChanged(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net);
                return (state with { Ui = ui }, null);
            }
            case ResolveNext:
            {
                // If already at Final, advance to next mode based on ModeDecider (Finished vs Move)
                if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    ClientMode nextMode = ModeDecider.DecideClientMode(state.Net, default, ui);
                    ViewEventBus.RaiseClientModeChanged(nextMode, state.Net, ui);
                    return (state with { Ui = ui }, null);
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
                // If we just entered Final, stay in Resolve until user presses Next/Skip again
                ViewEventBus.RaiseResolveCheckpointChanged(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net);
                return (state with { Ui = ui }, null);
            }
            case ResolveSkip:
                if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    ClientMode nextMode = ModeDecider.DecideClientMode(state.Net, default, ui);
                    ViewEventBus.RaiseClientModeChanged(nextMode, state.Net, ui);
                    return (state with { Ui = ui }, null);
                }
                else
                {
                    ui = ui with { Checkpoint = ResolveCheckpoint.Final };
                    ViewEventBus.RaiseResolveCheckpointChanged(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net);
                    return (state with { Ui = ui }, null);
                }
            default:
                return (state, null);
        }
    }
}


