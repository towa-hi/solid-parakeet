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
        List<GameEvent> emitted = null;
        switch (action)
        {
            // Setup
            case SetupSelectRank selRank:
            {
                Debug.Log($"UiReducer: SetupSelectRank old={ui.SelectedRank} new={selRank.Rank}");
                Rank? old = ui.SelectedRank;
                ui = ui with { SelectedRank = selRank.Rank };
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                emitted ??= new List<GameEvent>();
                emitted.Add(new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), setupTool));
                emitted.Add(new SetupRankSelectedEvent(old, ui.SelectedRank));
                return (state with { Ui = ui }, emitted);
            }
            case SetupClearAll:
            {
                Debug.Log("UiReducer: SetupClearAll");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                ui = ui with { PendingCommits = new Dictionary<PawnId, Rank?>() };
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), setupTool),
                    new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(ui.PendingCommits))
                };
                return (state with { Ui = ui }, emitted);
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
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), setupTool),
                    new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(dict))
                };
                return (state with { Ui = ui }, emitted);
            }
            case SetupCommitAt commit:
            {
                Debug.Log($"UiReducer: SetupCommitAt pos={commit.Pos} selectedRank={ui.SelectedRank}");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var dict = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var pawn = state.Net.GetAlivePawnFromPosUnchecked(commit.Pos);
                dict[pawn.pawn_id] = ui.SelectedRank;
                ui = ui with { PendingCommits = dict };
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), setupTool),
                    new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(dict))
                };
                return (state with { Ui = ui }, emitted);
            }
            case SetupUncommitAt uncommit:
            {
                Debug.Log($"UiReducer: SetupUncommitAt pos={uncommit.Pos}");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var dict = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                var pawn = state.Net.GetAlivePawnFromPosUnchecked(uncommit.Pos);
                dict[pawn.pawn_id] = null;
                ui = ui with { PendingCommits = dict };
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), setupTool),
                    new SetupPendingChangedEvent(oldMap, new Dictionary<PawnId, Rank?>(dict))
                };
                return (state with { Ui = ui }, emitted);
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
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                emitted = new List<GameEvent> { new SetupHoverChangedEvent(hover.Pos, isMyTurn, setupTool) };
                return (state with { Ui = ui }, emitted);
            }
            case SetupClickAt click:
            {
                var setupTool = UiSelectors.ComputeSetupTool(state.Net, ui);
                Debug.Log($"UiReducer: SetupClickAt pos={click.Pos} tool={setupTool}");
                // Delegate to existing handlers based on current tool
                if (!state.Net.IsMySubphase()) return (state, null);
                return setupTool switch
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
                MoveInputTool tool = UiSelectors.ComputeMoveTool(state.Net, ui);
                HashSet<Vector2Int> targets = new HashSet<Vector2Int>();
                if (tool == MoveInputTool.SELECT && state.Net.GetAlivePawnFromPosChecked(moveHover.Pos) is PawnState pawn)
                {
                    targets = state.Net.GetValidMoveTargetList(pawn.pawn_id, ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                }
                emitted = new List<GameEvent> { new MoveHoverChangedEvent(moveHover.Pos, isMyTurn, tool, targets) };
                return (state with { Ui = ui }, emitted);
            }
            case MoveClickAt moveClick:
            {
                if (!state.Net.IsMySubphase()) return (state, null);
                MoveInputTool tool = UiSelectors.ComputeMoveTool(state.Net, ui);
                if (tool == MoveInputTool.SELECT)
                {
                    Vector2Int pos = moveClick.Pos;
                    ui = ui with { SelectedPos = pos };
                    emitted = new List<GameEvent>();
                    if (state.Net.GetAlivePawnFromPosChecked(pos) is PawnState pawn)
                    {
                        var validTargets = state.Net.GetValidMoveTargetList(pawn.pawn_id, ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                        emitted.Add(new MoveSelectionChangedEvent(ui.SelectedPos, validTargets));
                    }
                    MoveInputTool newTool = UiSelectors.ComputeMoveTool(state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
                }
                if (tool == MoveInputTool.TARGET)
                {
                    if (ui.SelectedPos is not Vector2Int sel)
                    {
                        return (state, null);
                    }
                    var dict = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                    PawnId id = state.Net.GetAlivePawnFromPosUnchecked(sel).pawn_id;
                    // Diagnostics: log move pair construction and sanity-check current occupant
                    var occNow = state.Net.GetAlivePawnFromPosChecked(sel);
                    if (occNow is PawnState occPawn && occPawn.pawn_id != id)
                    {
                        Debug.LogWarning($"UiReducer: occupant id changed at start before setting move pair. start={sel} expectedId={id} nowId={occPawn.pawn_id}");
                    }
                    Debug.Log($"UiReducer: setting move pair id={id} start={sel} target={moveClick.Pos}");
                    dict[id] = (sel, moveClick.Pos);
                    var oldPairs = new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(ui.MovePairs);
                    ui = ui with { MovePairs = dict, SelectedPos = null };
                    emitted = new List<GameEvent>
                    {
                        new MovePairsChangedEvent(oldPairs, new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(dict)),
                        new MoveSelectionChangedEvent(null, new HashSet<Vector2Int>())
                    };
                    MoveInputTool newTool = UiSelectors.ComputeMoveTool(state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
                }
                if (tool == MoveInputTool.CLEAR_SELECT)
                {
                    ui = ui with { SelectedPos = null };
                    emitted = new List<GameEvent> { new MoveSelectionChangedEvent(null, new HashSet<Vector2Int>()) };
                    MoveInputTool newTool = UiSelectors.ComputeMoveTool(state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
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
                    emitted = new List<GameEvent> { new MovePairsChangedEvent(oldPairs, new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>(dict)) };
                    MoveInputTool newTool = UiSelectors.ComputeMoveTool(state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), newTool, new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
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


