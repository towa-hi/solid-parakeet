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
        ClientMode mode = ModeDecider.DecideClientMode(a.Net, a.Delta);
        GameSnapshot next = (state ?? GameSnapshot.Empty) with
        {
            Net = a.Net,
            Mode = mode,
        };
        // For now, emit no events; BoardManager still drives PhaseChangeSet
        return (next, null);
    }
}


