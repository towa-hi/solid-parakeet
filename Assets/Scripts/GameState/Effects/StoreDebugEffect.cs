using System.Collections.Generic;
using UnityEngine;

public sealed class StoreDebugEffect : IGameEffect
{
	StoreDebugSO so;
	GameStore store;

	public void Initialize(GameStore s)
	{
		store = s;
		// Try to find an instance in Resources for convenience
		so = Resources.Load<StoreDebugSO>("StoreDebug");
	}

	public void OnActionAndEvents(GameAction action, IReadOnlyList<GameEvent> events, GameSnapshot state)
	{
		if (so == null) return;
		if (store == null || action == null)
		{
			// If store is gone (teardown) or we get a null action from a teardown path, clear SO
			so.ResetState();
			return;
		}
		so.UpdateFrom(state, events, action);
	}
}


