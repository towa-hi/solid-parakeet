using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;

public static class UiSelectors
{
	public static CursorInputTool ComputeCursorTool(ClientMode mode, GameNetworkState net, LocalUiState uiState)
	{
		if (!net.IsMySubphase()) return CursorInputTool.NONE;
		switch (mode)
		{
			case ClientMode.Setup:
			{
				Vector2Int pos = uiState.HoveredPos;
				bool hoveringCommitted = false;
				if (net.GetAlivePawnFromPosChecked(pos) is PawnState pawnAtPos)
				{
					hoveringCommitted = uiState.PendingCommits.TryGetValue(pawnAtPos.pawn_id, out Rank? r) && r != null;
				}
				if (hoveringCommitted) return CursorInputTool.SETUP_UNSET_RANK;
				if (uiState.SelectedRank is Rank)
				{
					if (net.GetTileChecked(pos) is TileState tile && tile.setup == net.userTeam)
					{
						Rank selected = uiState.SelectedRank.Value;
						int max = net.lobbyParameters.GetMax(selected);
						int used = 0;
						foreach (var kv in uiState.PendingCommits)
						{
							if (kv.Value is Rank rr && rr == selected) used++;
						}
						if (used < max) return CursorInputTool.SETUP_SET_RANK;
					}
				}
				return CursorInputTool.NONE;
			}
			case ClientMode.Move:
			{
				Vector2Int hovered = uiState.HoveredPos;
				if (uiState.SelectedPos is Vector2Int sel)
				{
					if (net.GetAlivePawnFromPosChecked(sel) is PawnState selectedPawn)
					{
						HashSet<Vector2Int> validTargets = net.GetValidMoveTargetList(selectedPawn.pawn_id, uiState.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
						return validTargets.Contains(hovered) ? CursorInputTool.MOVE_TARGET : CursorInputTool.MOVE_CLEAR;
					}
					return CursorInputTool.MOVE_CLEAR;
				}
				bool hoveringExistingStart = uiState.MovePairs.Any(kv => kv.Value.start == hovered);
				if (hoveringExistingStart) return CursorInputTool.MOVE_CLEAR_MOVEPAIR;
				if (uiState.MovePairs.Count < net.GetMaxMovesThisTurn() && net.GetAlivePawnFromPosChecked(hovered) is PawnState hoveredPawn && net.CanUserMovePawn(hoveredPawn.pawn_id))
				{
					return CursorInputTool.MOVE_SELECT;
				}
				return CursorInputTool.NONE;
			}
			default:
				return CursorInputTool.NONE;
		}
	}
}


