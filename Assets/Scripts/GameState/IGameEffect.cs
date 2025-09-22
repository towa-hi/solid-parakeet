using System.Collections.Generic;

public interface IGameEffect
{
    void Initialize(GameStore store);
    void OnActionAndEvents(GameAction action, IReadOnlyList<GameEvent> events, GameSnapshot state);
}


