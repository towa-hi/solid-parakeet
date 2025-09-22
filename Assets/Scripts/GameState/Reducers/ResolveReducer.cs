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
                // Capture resolve data upon receiving it
                ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                return (state with { Ui = ui }, null);
            case ResolvePrev:
            {
                if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    int last = (ui.ResolveData.battles?.Length ?? 0) - 1;
                    ui = last >= 0
                        ? ui with { Checkpoint = ResolveCheckpoint.Battle, BattleIndex = last }
                        : ui with { Checkpoint = ResolveCheckpoint.PostMoves };
                }
                else if (ui.Checkpoint == ResolveCheckpoint.Battle)
                {
                    ui = ui.BattleIndex > 0
                        ? ui with { BattleIndex = ui.BattleIndex - 1 }
                        : ui with { Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                }
                else if (ui.Checkpoint == ResolveCheckpoint.PostMoves)
                {
                    ui = ui with { Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                }
                return (state with { Ui = ui }, null);
            }
            case ResolveNext:
            {
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
                else if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    // signal done by staying in Final; Phase will interpret it as ResolveDone if needed
                }
                return (state with { Ui = ui }, null);
            }
            case ResolveSkip:
                ui = ui with { Checkpoint = ResolveCheckpoint.Final };
                return (state with { Ui = ui }, null);
            default:
                return (state, null);
        }
    }
}


