using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ViewAdapterEffects : IGameEffect
{
	GameStore store;

	public void Initialize(GameStore s)
	{
		store = s;
	}

	public void OnActionAndEvents(GameAction action, IReadOnlyList<GameEvent> events, GameSnapshot state)
	{
		if (events == null || events.Count == 0) return;
		foreach (GameEvent e in events)
		{
			switch (e)
			{
					case ClientModeChangedEvent m:
						ViewEventBus.RaiseClientModeChanged(m.Snapshot);
						// Emit an initial hover update appropriate for the new mode so cursor updates immediately
						bool isMyTurn = m.Snapshot.Net.IsMySubphase();
					if (m.Snapshot.Mode == ClientMode.Move)
						{
							var targets = new System.Collections.Generic.HashSet<UnityEngine.Vector2Int>();
						if (UiSelectors.ComputeCursorTool(m.Snapshot.Mode, m.Snapshot.Net, m.Snapshot.Ui) == CursorInputTool.MOVE_SELECT && m.Snapshot.Net.GetAlivePawnFromPosChecked(m.Snapshot.Ui.HoveredPos) is Contract.PawnState pawn)
							{
								targets = m.Snapshot.Net.GetValidMoveTargetList(pawn.pawn_id, m.Snapshot.Ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
							}
						ViewEventBus.RaiseMoveHoverChanged(m.Snapshot.Ui.HoveredPos, isMyTurn, targets);
						}
					else if (m.Snapshot.Mode == ClientMode.Setup)
						{
						ViewEventBus.RaiseSetupHoverChanged(m.Snapshot.Ui.HoveredPos, isMyTurn);
						}
						break;
				case SetupHoverChangedEvent sh:
					ViewEventBus.RaiseSetupHoverChanged(sh.Pos, sh.IsMyTurn);
					break;
				case SetupRankSelectedEvent sr:
					ViewEventBus.RaiseSetupRankSelected(sr.OldRank, sr.NewRank);
					break;
				case SetupPendingChangedEvent sp:
					ViewEventBus.RaiseSetupPendingChanged(sp.OldMap, sp.NewMap);
					break;
				case MoveHoverChangedEvent mh:
					ViewEventBus.RaiseMoveHoverChanged(mh.Pos, mh.IsMyTurn, mh.Targets);
					break;
				case MoveSelectionChangedEvent ms:
					ViewEventBus.RaiseMoveSelectionChanged(ms.SelectedPos, ms.Targets);
					break;
				case MovePairsChangedEvent mp:
					ViewEventBus.RaiseMovePairsChanged(mp.OldPairs, mp.NewPairs);
					break;
				case ResolveCheckpointChangedEvent rc:
					Debug.Log($"[Effects] Begin RaiseResolveCheckpointChanged checkpoint={rc.Checkpoint} index={rc.BattleIndex}");
					ViewEventBus.RaiseResolveCheckpointChanged(rc.Checkpoint, rc.ResolveData, rc.BattleIndex, rc.Net);
					Debug.Log($"[Effects] End RaiseResolveCheckpointChanged checkpoint={rc.Checkpoint} index={rc.BattleIndex}");
					break;
				case AssetBalanceUpdatedEvent ab:
					ViewEventBus.RaiseAssetBalanceUpdated(ab.Team, ab.Balance);
					break;
				// no-op
			}
		}
	}

}


