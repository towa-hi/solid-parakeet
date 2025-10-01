using System;
using UnityEngine;
using Contract;

[CreateAssetMenu(menuName = "ScryingStratego/Store Debug", fileName = "StoreDebug")]
public sealed class StoreDebugSO : ScriptableObject
{
	[Header("Connection")]
	public bool isHookedUp;

	[Header("Snapshot Basics")]
	public ClientMode mode;
	public Vector2Int hoveredPos;
	public Vector2Int selectedPos;
	public bool hasSelectedPos;
	public int movePairsCount;
	public int pendingCommitCount;

	[Header("Network Basics")]
	public int turn;
	public Phase phase;
	public Subphase subphase;
	public bool isMySubphase;

	[Header("Network Delta (last action)")]
	public string lastAction;
	public bool deltaPhaseChanged;
	public bool deltaTurnChanged;
	public bool deltaHasResolve;

	[Header("Network Snapshot Stats")]
	public int pawnCount;
	public int myPawnCount;
	public int opponentPawnCount;

	[Header("Recent Events")] 
	public string[] lastEventNames = Array.Empty<string>();

	[TextArea(3, 10)] public string stateSummary;

	public void ResetState()
	{
		isHookedUp = false;
		mode = default;
		hoveredPos = default;
		selectedPos = default;
		hasSelectedPos = false;
		movePairsCount = 0;
		pendingCommitCount = 0;
		turn = 0;
		phase = default;
		subphase = default;
		isMySubphase = false;
		lastAction = null;
		deltaPhaseChanged = false;
		deltaTurnChanged = false;
		deltaHasResolve = false;
		pawnCount = 0;
		myPawnCount = 0;
		opponentPawnCount = 0;
		lastEventNames = Array.Empty<string>();
		stateSummary = string.Empty;
	}

	public void UpdateFrom(GameSnapshot state, System.Collections.Generic.IReadOnlyList<GameEvent> events, GameAction action)
	{
		isHookedUp = true;
		mode = state.Mode;
		hoveredPos = state.Ui?.HoveredPos ?? default;
		hasSelectedPos = state.Ui?.SelectedPos.HasValue ?? false;
		selectedPos = hasSelectedPos ? state.Ui.SelectedPos.Value : default;
		movePairsCount = state.Ui?.MovePairs?.Count ?? 0;
		pendingCommitCount = state.Ui?.PendingCommits?.Count ?? 0;
		turn = (int)state.Net.gameState.turn;
		phase = state.Net.lobbyInfo.phase;
		subphase = state.Net.lobbyInfo.subphase;
		isMySubphase = state.Net.IsMySubphase();
		// Action + delta
		lastAction = action != null ? action.GetType().Name : null;
		deltaPhaseChanged = false;
		deltaTurnChanged = false;
		deltaHasResolve = false;
		if (action is NetworkStateChanged ns)
		{
			deltaPhaseChanged = ns.Delta.PhaseChanged;
			deltaTurnChanged = ns.Delta.TurnChanged;
			deltaHasResolve = ns.Delta.TurnResolve.HasValue;
		}
		// Pawn counts
		pawnCount = state.Net.gameState.pawns?.Length ?? 0;
		myPawnCount = 0;
		opponentPawnCount = 0;
		for (int i = 0; i < (state.Net.gameState.pawns?.Length ?? 0); i++)
		{
			var p = state.Net.gameState.pawns[i];
			if (!p.alive) continue;
			if (p.GetTeam() == state.Net.userTeam) myPawnCount++; else opponentPawnCount++;
		}
		if (events != null && events.Count > 0)
		{
			int n = Mathf.Min(10, events.Count);
			lastEventNames = new string[n];
			for (int i = 0; i < n; i++) lastEventNames[i] = events[i].GetType().Name;
		}
		stateSummary = $"Mode={mode} Turn={turn} Phase={phase}/{subphase} Hover={hoveredPos} Selected={(hasSelectedPos ? selectedPos.ToString() : "-")} Moves={movePairsCount} Commits={pendingCommitCount} MySub={isMySubphase}";
	}
}


