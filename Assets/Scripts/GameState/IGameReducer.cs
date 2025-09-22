using System.Collections.Generic;

public interface IGameReducer
{
    (GameSnapshot nextState, List<GameEvent> events) Reduce(GameSnapshot state, GameAction action);
}


