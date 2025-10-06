using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameStore
{
    public GameSnapshot State { get; private set; }

    readonly List<IGameReducer> reducers;
    readonly List<IGameEffect> effects;

    public event Action<IReadOnlyList<GameEvent>> EventsEmitted;

    public GameStore(GameSnapshot initialState, IEnumerable<IGameReducer> reducers, IEnumerable<IGameEffect> effects)
    {
        State = initialState ?? GameSnapshot.Empty;
        this.reducers = reducers != null ? new List<IGameReducer>(reducers) : new List<IGameReducer>();
        this.effects = effects != null ? new List<IGameEffect>(effects) : new List<IGameEffect>();
        foreach (IGameEffect effect in this.effects)
        {
            effect.Initialize(this);
        }
    }

    public void Dispatch(GameAction action)
    {
        if (action == null)
        {
            return;
        }

        UnityEngine.Debug.Log($"[GameStore] Dispatch begin action={action.GetType().Name}");
        GameSnapshot previous = State;
        GameSnapshot next = State;
        List<GameEvent> allEvents = null;
        foreach (IGameReducer reducer in reducers)
        {
            (GameSnapshot reduced, List<GameEvent> events) = reducer.Reduce(next, action);
            UnityEngine.Debug.Log($"[GameStore] Reducer {reducer.GetType().Name} -> stateChanged={(reduced != null)} events={(events?.Count ?? 0)}");
            next = reduced ?? next;
            if (events != null && events.Count > 0)
            {
                allEvents ??= new List<GameEvent>();
                allEvents.AddRange(events);
            }
        }
        State = next;
        if (allEvents != null && allEvents.Count > 0)
        {
            for (int i = 0; i < allEvents.Count; i++)
            {
                var e = allEvents[i];
                string details = e switch
                {
                    ResolveCheckpointChangedEvent rc => $"ResolveCheckpointChanged checkpoint={rc.Checkpoint} index={rc.BattleIndex} moves={(rc.ResolveData.moves?.Count ?? 0)} battles={(rc.ResolveData.battles?.Length ?? 0)}",
                    ClientModeChangedEvent cm => $"ClientModeChanged mode={cm.Snapshot.Mode}",
                    MoveHoverChangedEvent mh => $"MoveHoverChanged pos={mh.Pos} targets={mh.Targets?.Count}",
                    MoveSelectionChangedEvent ms => $"MoveSelectionChanged selected={(ms.SelectedPos?.ToString() ?? "-")} targets={ms.Targets?.Count}",
                    MovePairsChangedEvent mp => $"MovePairsChanged old={mp.OldPairs?.Count} new={mp.NewPairs?.Count}",
                    SetupHoverChangedEvent sh => $"SetupHoverChanged pos={sh.Pos}",
                    SetupPendingChangedEvent sp => $"SetupPendingChanged old={sp.OldMap?.Count} new={sp.NewMap?.Count}",
                    SetupRankSelectedEvent sr => $"SetupRankSelected old={sr.OldRank} new={sr.NewRank}",
                    _ => e.GetType().Name,
                };
                Debug.Log($"[GameStore.Events] {details}");
            }
            EventsEmitted?.Invoke(allEvents);
        }
        IReadOnlyList<GameEvent> emitted = allEvents != null ? (IReadOnlyList<GameEvent>)allEvents : Array.Empty<GameEvent>();
        UnityEngine.Debug.Log($"[GameStore] Effects begin action={action.GetType().Name} effectsCount={effects.Count}");
        foreach (IGameEffect effect in effects)
        {
            UnityEngine.Debug.Log($"[GameStore] Effect start {effect.GetType().Name} action={action.GetType().Name} events={(emitted?.Count ?? 0)}");
            effect.OnActionAndEvents(action, emitted, State);
            UnityEngine.Debug.Log($"[GameStore] Effect end   {effect.GetType().Name}");
        }
        // Notify views of state updates when UI or core state has changed
        if (!Equals(previous.Ui, State.Ui) || previous.Mode != State.Mode || !Equals(previous.Net, State.Net))
        {
            Debug.Log($"[GameStore] About to RaiseStateUpdated mode={State.Mode} checkpoint={(State.Ui?.Checkpoint)} waiting={(State.Ui?.WaitingForResponse != null)}");
            ViewEventBus.RaiseStateUpdated(State);
            Debug.Log("[GameStore] After RaiseStateUpdated");
        }
        UnityEngine.Debug.Log($"[GameStore] Dispatch end action={action.GetType().Name}");
    }
}


