using System.Collections.Generic;
using System.Linq;
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
        // Local function to compute current move tool based on UI + Net + hover
        MoveInputTool ComputeMoveTool(GameNetworkState net, LocalUiState uiState)
        {
            if (!net.IsMySubphase()) return MoveInputTool.NONE;
            Vector2Int hovered = uiState.HoveredPos;
            // selectedPos present
            if (uiState.SelectedPos is Vector2Int sel)
            {
                // valid target positions from selection
                if (net.GetAlivePawnFromPosChecked(sel) is PawnState selectedPawn)
                {
                    HashSet<Vector2Int> validTargets = net.GetValidMoveTargetList(selectedPawn.pawn_id, uiState.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                    return validTargets.Contains(hovered) ? MoveInputTool.TARGET : MoveInputTool.CLEAR_SELECT;
                }
                return MoveInputTool.CLEAR_SELECT;
            }
            // clear existing move pair if hovering a start that is already set
            bool hoveringExistingStart = uiState.MovePairs.Any(kv => kv.Value.start == hovered);
            if (hoveringExistingStart) return MoveInputTool.CLEAR_MOVEPAIR;
            // Otherwise, SELECT if hovering our movable pawn and not at move limit
            if (uiState.MovePairs.Count < net.GetMaxMovesThisTurn() && net.GetAlivePawnFromPosChecked(hovered) is PawnState hoveredPawn && net.CanUserMovePawn(hoveredPawn.pawn_id))
            {
                return MoveInputTool.SELECT;
            }
            return MoveInputTool.NONE;
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
                ViewEventBus.RaiseSetupHoverChanged(selRank.Rank.HasValue ? ui.HoveredPos : ui.HoveredPos, state.Net.IsMySubphase(), ui.SetupTool);
                ViewEventBus.RaiseSetupRankSelected(old, ui.SelectedRank);
                return (state with { Ui = ui }, new List<GameEvent> { new SetupRankSelectedEvent(old, ui.SelectedRank) });
            }
            case SetupClearAll:
            {
                Debug.Log("UiReducer: SetupClearAll");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                ui = ui with { PendingCommits = new Dictionary<PawnId, Rank?>() };
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), ui.SetupTool);
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
                ViewEventBus.RaiseSetupHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), ui.SetupTool);
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
                ViewEventBus.RaiseSetupHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), ui.SetupTool);
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
                ViewEventBus.RaiseSetupHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), ui.SetupTool);
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
                ViewEventBus.RaiseSetupHoverChanged(hover.Pos, isMyTurn, ui.SetupTool);
                // Update cursor tool from UI state (SelectedRank + PendingCommits determines ADD/REMOVE/NONE)
                ui = ui with { SetupTool = ComputeSetupTool(state.Net, ui) };
                ViewEventBus.RaiseSetupHoverChanged(hover.Pos, isMyTurn, ui.SetupTool);
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
            // Movement
            case MoveHoverAction moveHover:
            {
                ui = ui with { HoveredPos = moveHover.Pos };
                bool isMyTurn = state.Net.IsMySubphase();
                MoveInputTool tool = ComputeMoveTool(state.Net, ui);
                HashSet<Vector2Int> targets = new HashSet<Vector2Int>();
                if (tool == MoveInputTool.SELECT && state.Net.GetAlivePawnFromPosChecked(moveHover.Pos) is PawnState pawn)
                {
                    targets = state.Net.GetValidMoveTargetList(pawn.pawn_id, ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                }
                ViewEventBus.RaiseMoveHoverChanged(moveHover.Pos, isMyTurn, tool, targets);
                // tool is conveyed via MoveHoverChanged
                return (state with { Ui = ui }, null);
            }
            case MoveClickAt moveClick:
            {
                if (!state.Net.IsMySubphase()) return (state, null);
                MoveInputTool tool = ComputeMoveTool(state.Net, ui);
                if (tool == MoveInputTool.SELECT)
                {
                    Vector2Int pos = moveClick.Pos;
                    ui = ui with { SelectedPos = pos };
                    // emit selection + targets
                    if (state.Net.GetAlivePawnFromPosChecked(pos) is PawnState pawn)
                    {
                        var validTargets = state.Net.GetValidMoveTargetList(pawn.pawn_id, ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                        ViewEventBus.RaiseMoveSelectionChanged(ui.SelectedPos, validTargets);
                    }
                    // Re-emit hover with updated tool (usually CLEAR_SELECT now)
                    MoveInputTool newTool = ComputeMoveTool(state.Net, ui);
                    ViewEventBus.RaiseMoveHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>());
                    return (state with { Ui = ui }, null);
                }
                if (tool == MoveInputTool.TARGET)
                {
                    if (ui.SelectedPos is not Vector2Int sel)
                    {
                        return (state, null);
                    }
                    var dict = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                    PawnId id = state.Net.GetAlivePawnFromPosUnchecked(sel).pawn_id;
                    dict[id] = (sel, moveClick.Pos);
                    var oldPairs = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                    ui = ui with { MovePairs = dict, SelectedPos = null };
                    ViewEventBus.RaiseMovePairsChanged(oldPairs, new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(dict));
                    ViewEventBus.RaiseMoveSelectionChanged(null, new HashSet<Vector2Int>());
                    // Re-emit hover with updated tool
                    MoveInputTool newTool = ComputeMoveTool(state.Net, ui);
                    ViewEventBus.RaiseMoveHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>());
                    return (state with { Ui = ui }, null);
                }
                if (tool == MoveInputTool.CLEAR_SELECT)
                {
                    ui = ui with { SelectedPos = null };
                    ViewEventBus.RaiseMoveSelectionChanged(null, new HashSet<Vector2Int>());
                    MoveInputTool newTool = ComputeMoveTool(state.Net, ui);
                    ViewEventBus.RaiseMoveHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>());
                    return (state with { Ui = ui }, null);
                }
                if (tool == MoveInputTool.CLEAR_MOVEPAIR)
                {
                    Vector2Int hovered = moveClick.Pos;
                    var oldPairs = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                    var dict = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                    foreach (var kv in ui.MovePairs)
                    {
                        if (kv.Value.start == hovered) { dict.Remove(kv.Key); break; }
                    }
                    ui = ui with { MovePairs = dict };
                    ViewEventBus.RaiseMovePairsChanged(oldPairs, new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(dict));
                    MoveInputTool newTool = ComputeMoveTool(state.Net, ui);
                    ViewEventBus.RaiseMoveHoverChanged(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>());
                    return (state with { Ui = ui }, null);
                }
                return (state, null);
            }
            case MoveSubmit:
                // NetworkEffects handles request construction
                break;
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


