using System;
using System.Collections.Generic;

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

        UnityEngine.Debug.Log($"GameStore.Dispatch action={action.GetType().Name}");
        GameSnapshot next = State;
        List<GameEvent> allEvents = null;
        foreach (IGameReducer reducer in reducers)
        {
            (GameSnapshot reduced, List<GameEvent> events) = reducer.Reduce(next, action);
            UnityEngine.Debug.Log($"Reducer {reducer.GetType().Name} -> stateChanged={(reduced != null)} events={(events?.Count ?? 0)}");
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
            UnityEngine.Debug.Log($"GameStore.EventsEmitted count={allEvents.Count}");
            EventsEmitted?.Invoke(allEvents);
        }
        IReadOnlyList<GameEvent> emitted = allEvents != null ? (IReadOnlyList<GameEvent>)allEvents : Array.Empty<GameEvent>();
        foreach (IGameEffect effect in effects)
        {
            effect.OnActionAndEvents(action, emitted, State);
        }
    }
}


