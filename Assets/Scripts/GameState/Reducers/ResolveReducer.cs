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
                UnityEngine.Debug.Log($"[ResolveReducer] NetworkStateChanged with TurnResolve: entering Pre. battles={(a.Delta.TurnResolve.Value.battles?.Length ?? 0)} moves={(a.Delta.TurnResolve.Value.moves?.Count ?? 0)}");
                ui = ui with { ResolveData = a.Delta.TurnResolve.Value, Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                UnityEngine.Debug.Log($"[ResolveReducer] Emit ResolveCheckpointChangedEvent: checkpoint=Pre index=-1");
                return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ResolveCheckpoint.Pre, ui.ResolveData, -1, a.Net)});
            case ResolvePrev:
            {
                UnityEngine.Debug.Log($"[ResolveReducer] ResolvePrev -> Pre");
                ui = ui with { Checkpoint = ResolveCheckpoint.Pre, BattleIndex = -1 };
                return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net)});
            }
            case ResolveNext:
            {
                // If already at Final, advance to next mode and notify views
                if (ui.Checkpoint == ResolveCheckpoint.Final)
                {
                    UnityEngine.Debug.Log($"[ResolveReducer] ResolveNext at Final -> decide next mode");
                    ClientMode nextMode = ModeDecider.DecideClientMode(state.Net, default, ui);
                    LocalUiState ui2 = LocalUiState.Empty with { HoveredPos = ui.HoveredPos };
                    GameSnapshot s2 = state with { Ui = ui2, Mode = nextMode };
                    UnityEngine.Debug.Log($"[ResolveReducer] Emitting ClientModeChangedEvent: nextMode={nextMode}");
                    return (s2, new List<GameEvent>{ new ClientModeChangedEvent(s2)});
                }
                if (ui.Checkpoint == ResolveCheckpoint.Pre)
                {
                    UnityEngine.Debug.Log($"[ResolveReducer] ResolveNext: Pre -> PostMoves");
                    ui = ui with { Checkpoint = ResolveCheckpoint.PostMoves };
                }
                else if (ui.Checkpoint == ResolveCheckpoint.PostMoves)
                {
                    bool hasBattles = (ui.ResolveData.battles?.Length ?? 0) > 0;
                    UnityEngine.Debug.Log($"[ResolveReducer] ResolveNext: PostMoves -> {(hasBattles ? "Battle(0)" : "Final")}");
                    ui = hasBattles
                        ? ui with { Checkpoint = ResolveCheckpoint.Battle, BattleIndex = 0 }
                        : ui with { Checkpoint = ResolveCheckpoint.Final };
                }
                else if (ui.Checkpoint == ResolveCheckpoint.Battle)
                {
                    int next = ui.BattleIndex + 1;
                    int total = ui.ResolveData.battles?.Length ?? 0;
                    UnityEngine.Debug.Log($"[ResolveReducer] ResolveNext: Battle {ui.BattleIndex} -> {(next < total ? $"Battle {next}" : "Final")}");
                    ui = next < total
                        ? ui with { BattleIndex = next }
                        : ui with { Checkpoint = ResolveCheckpoint.Final };
                }
                UnityEngine.Debug.Log($"[ResolveReducer] Emit ResolveCheckpointChangedEvent: checkpoint={ui.Checkpoint} index={ui.BattleIndex}");
                return (state with { Ui = ui }, new List<GameEvent>{ new ResolveCheckpointChangedEvent(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, state.Net)});
            }
            case ResolveSkip:
            {
                UnityEngine.Debug.Log($"[ResolveReducer] ResolveSkip at {ui.Checkpoint}");
                ClientMode nextMode = ModeDecider.DecideClientMode(state.Net, default, ui);
                LocalUiState ui2 = LocalUiState.Empty with { HoveredPos = ui.HoveredPos };
                GameSnapshot s2 = state with { Ui = ui2, Mode = nextMode };
                UnityEngine.Debug.Log($"[ResolveReducer] Emitting ClientModeChangedEvent: nextMode={nextMode}");
                return (s2, new List<GameEvent>{ new ClientModeChangedEvent(s2)});
            }
            default:
                return (state, null);
        }
    }
}


