using System.Collections.Generic;
using Contract;
using UnityEngine;

public sealed class UiReducer : IGameReducer
{
    public (GameSnapshot nextState, List<GameEvent> events) Reduce(GameSnapshot state, GameAction action)
    {
        if (state == null) state = GameSnapshot.Empty;
        LocalUiState ui = state.Ui ?? LocalUiState.Empty;
        // Local function to compute current setup tool based on UI + Net + hover
        SetupInputTool ComputeSetupTool(GameNetworkState net, LocalUiState uiState)
        {
            if (!net.IsMySubphase()) return SetupInputTool.NONE;
            Vector2Int pos = uiState.HoveredPos;
            bool hoveringCommitted = false;
            if (net.GetAlivePawnFromPosChecked(pos) is PawnState pawnAtPos)
            {
                hoveringCommitted = uiState.PendingCommits.TryGetValue(pawnAtPos.pawn_id, out Rank? r) && r != null;
            }
            if (hoveringCommitted)
            {
                return SetupInputTool.REMOVE;
            }
            if (uiState.SelectedRank is Rank)
            {
                if (net.GetTileChecked(pos) is TileState tile && tile.setup == net.userTeam)
                {
                    // Only allow ADD if remaining > 0 for the selected rank
                    Rank selected = uiState.SelectedRank.Value;
                    int max = net.lobbyParameters.GetMax(selected);
                    int used = 0;
                    foreach (var kv in uiState.PendingCommits)
                    {
                        if (kv.Value is Rank rr && rr == selected) used++;
                    }
                    if (used < max)
                    {
                        return SetupInputTool.ADD;
                    }
                }
            }
            return SetupInputTool.NONE;
        }
        switch (action)
        {
            // Setup
            case SetupSelectRank selRank:
            {
                Debug.Log($"UiReducer: SetupSelectRank old={ui.SelectedRank} new={selRank.Rank}");
                Rank? old = ui.SelectedRank;
                ui = ui with { SelectedRank = selRank.Rank };
                // Update tool and notify views
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupCursorToolChanged(ui.SetupTool);
                ViewEventBus.RaiseSetupRankSelected(old, ui.SelectedRank);
                return (state with { Ui = ui }, new List<GameEvent> { new SetupRankSelectedEvent(old, ui.SelectedRank) });
            }
            case SetupClearAll:
            {
                Debug.Log("UiReducer: SetupClearAll");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                ui = ui with { PendingCommits = new Dictionary<PawnId, Rank?>() };
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupCursorToolChanged(ui.SetupTool);
                ViewEventBus.RaiseSetupPendingChanged(oldMap, new Dictionary<PawnId, Rank?>(ui.PendingCommits));
                return (state with { Ui = ui }, new List<GameEvent> { new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(ui.PendingCommits)) });
            }
            case SetupAutoFill:
            {
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var dict = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                // Build auto commitments from current network state
                var auto = state.Net.AutoSetup(state.Net.userTeam);
                foreach (var kv in auto)
                {
                    var pawn = state.Net.GetAlivePawnFromPosUnchecked(kv.Key);
                    dict[pawn.pawn_id] = kv.Value;
                }
                ui = ui with { PendingCommits = dict };
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupCursorToolChanged(ui.SetupTool);
                ViewEventBus.RaiseSetupPendingChanged(oldMap, new Dictionary<PawnId, Rank?>(dict));
                return (state with { Ui = ui }, new List<GameEvent> { new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(dict)) });
            }
            case SetupCommitAt commit:
            {
                Debug.Log($"UiReducer: SetupCommitAt pos={commit.Pos} selectedRank={ui.SelectedRank}");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var dict = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var pawn = state.Net.GetAlivePawnFromPosUnchecked(commit.Pos);
                dict[pawn.pawn_id] = ui.SelectedRank;
                ui = ui with { PendingCommits = dict };
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupCursorToolChanged(ui.SetupTool);
                ViewEventBus.RaiseSetupPendingChanged(oldMap, new Dictionary<PawnId, Rank?>(dict));
                return (state with { Ui = ui }, new List<GameEvent> { new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(dict)) });
            }
            case SetupUncommitAt uncommit:
            {
                Debug.Log($"UiReducer: SetupUncommitAt pos={uncommit.Pos}");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var dict = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var pawn = state.Net.GetAlivePawnFromPosUnchecked(uncommit.Pos);
                dict[pawn.pawn_id] = null;
                ui = ui with { PendingCommits = dict };
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupCursorToolChanged(ui.SetupTool);
                ViewEventBus.RaiseSetupPendingChanged(oldMap, new Dictionary<PawnId, Rank?>(dict));
                return (state with { Ui = ui }, new List<GameEvent> { new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(dict)) });
            }
            case SetupSubmit:
                // No state change; effect will build and dispatch CommitSetupAction
                Debug.Log("UiReducer: SetupSubmit");
                break;
            case SetupHoverAction hover:
            {
                Debug.Log($"UiReducer: SetupHoverAction pos={hover.Pos}");
                ui = ui with { HoveredPos = hover.Pos };
                bool isMyTurn = state.Net.IsMySubphase();
                ViewEventBus.RaiseSetupHoverChanged(hover.Pos, isMyTurn);
                // Update cursor tool from UI state (SelectedRank + PendingCommits determines ADD/REMOVE/NONE)
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupCursorToolChanged(ui.SetupTool);
                return (state with { Ui = ui }, null);
            }
            case SetupClickAt click:
            {
                Debug.Log($"UiReducer: SetupClickAt pos={click.Pos} tool={ui.SetupTool}");
                // Delegate to existing handlers based on current tool
                if (!state.Net.IsMySubphase()) return (state, null);
                return ui.SetupTool switch
                {
                    SetupInputTool.ADD => Reduce(state, new SetupCommitAt(click.Pos)),
                    SetupInputTool.REMOVE => Reduce(state, new SetupUncommitAt(click.Pos)),
                    _ => (state, null),
                };
            }
            case SelectTile sel:
                ui = ui with { SelectedPos = sel.Pos, HoveredPos = sel.Pos };
                break;
            case ClearSelection:
                ui = ui with { SelectedPos = null };
                break;
            case AddMovePair add:
            {
                var dict = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                PawnId id = state.Net.GetAlivePawnFromPosUnchecked(add.Start).pawn_id;
                dict[id] = (add.Start, add.Target);
                ui = ui with { MovePairs = dict };
                break;
            }
            case ClearMovePair clr:
            {
                var dict = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                foreach (var kv in ui.MovePairs)
                {
                    if (kv.Value.start == clr.Start) { dict.Remove(kv.Key); break; }
                }
                ui = ui with { MovePairs = dict };
                break;
            }
            case ResolvePrev:
            case ResolveNext:
            case ResolveSkip:
                // Will be handled by a dedicated ResolveReducer in a later step
                break;
            case SetupRankSelectedAction:
            case SetupCommitEdited:
                // Will be handled by a SetupReducer in a later step
                break;
            default:
                return (state, null);
        }
        return (state with { Ui = ui }, null);
    }
}


