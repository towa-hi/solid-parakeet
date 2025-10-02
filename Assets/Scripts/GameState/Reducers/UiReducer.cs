using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;

public sealed class UiReducer : IGameReducer
{
    private static bool IsMoveSelect(CursorInputTool tool) => tool == CursorInputTool.MOVE_SELECT;
    private static bool IsMoveTarget(CursorInputTool tool) => tool == CursorInputTool.MOVE_TARGET;
    private static bool IsMoveClearSelect(CursorInputTool tool) => tool == CursorInputTool.MOVE_CLEAR;
    private static bool IsMoveClearPair(CursorInputTool tool) => tool == CursorInputTool.MOVE_CLEAR_MOVEPAIR;

    public (GameSnapshot nextState, List<GameEvent> events) Reduce(GameSnapshot state, GameAction action)
    {
        if (state == null) state = GameSnapshot.Empty;
        LocalUiState ui = state.Ui ?? LocalUiState.Empty;
        List<GameEvent> emitted = null;
        switch (action)
        {
            case UiWaitingForResponse w:
            {
                ui = ui with { WaitingForResponse = w.Data };
                return (state with { Ui = ui }, null);
            }
            // Setup
            case SetupSelectRank selRank:
            {
                Rank? old = ui.SelectedRank;
                Rank? next = selRank.Rank;
                if (old is Rank oldR && selRank.Rank is Rank newR && oldR == newR)
                {
                    next = null; // toggle off when clicking the same rank
                }
                Debug.Log($"UiReducer: SetupSelectRank old={old} new={next}");
                ui = ui with { SelectedRank = next };
                emitted ??= new List<GameEvent>();
                emitted.Add(new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase()));
                emitted.Add(new SetupRankSelectedEvent(old, ui.SelectedRank));
                return (state with { Ui = ui }, emitted);
            }
            case SetupClearAll:
            {
                Debug.Log("UiReducer: SetupClearAll");
                var oldMap = new Dictionary<PawnId, Rank?>(ui.PendingCommits);
                ui = ui with { PendingCommits = new Dictionary<PawnId, Rank?>() };
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase()),
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
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase()),
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
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase()),
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
                emitted = new List<GameEvent>
                {
                    new SetupHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase()),
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
                emitted = new List<GameEvent> { new SetupHoverChangedEvent(hover.Pos, isMyTurn) };
                return (state with { Ui = ui }, emitted);
            }
            case SetupClickAt click:
            {
                var cursorTool = UiSelectors.ComputeCursorTool(state.Mode, state.Net, ui);
                Debug.Log($"UiReducer: SetupClickAt pos={click.Pos} tool={cursorTool}");
                // Delegate to existing handlers based on current tool
                if (!state.Net.IsMySubphase()) return (state, null);
                if (cursorTool == CursorInputTool.SETUP_SET_RANK) return Reduce(state, new SetupCommitAt(click.Pos));
                if (cursorTool == CursorInputTool.SETUP_UNSET_RANK) return Reduce(state, new SetupUncommitAt(click.Pos));
                return (state, null);
            }
            // Movement
            case MoveHoverAction moveHover:
            {
                ui = ui with { HoveredPos = moveHover.Pos };
                bool isMyTurn = state.Net.IsMySubphase();
                HashSet<Vector2Int> targets = new HashSet<Vector2Int>();
                if (isMyTurn && ui.SelectedPos is not Vector2Int &&
                    ui.MovePairs.Count < state.Net.GetMaxMovesThisTurn() &&
                    state.Net.GetAlivePawnFromPosChecked(moveHover.Pos) is PawnState pawn &&
                    state.Net.CanUserMovePawn(pawn.pawn_id))
                {
                    targets = state.Net.GetValidMoveTargetList(pawn.pawn_id, ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                }
                emitted = new List<GameEvent> { new MoveHoverChangedEvent(moveHover.Pos, isMyTurn, targets) };
                return (state with { Ui = ui }, emitted);
            }
            case MoveClickAt moveClick:
            {
                if (!state.Net.IsMySubphase()) return (state, null);
                var cursorTool = UiSelectors.ComputeCursorTool(state.Mode, state.Net, ui);
                if (IsMoveSelect(cursorTool))
                {
                    Vector2Int pos = moveClick.Pos;
                    ui = ui with { SelectedPos = pos };
                    emitted = new List<GameEvent>();
                    if (state.Net.GetAlivePawnFromPosChecked(pos) is PawnState pawn)
                    {
                        var validTargets = state.Net.GetValidMoveTargetList(pawn.pawn_id, ui.MovePairs.ToDictionary(kv => kv.Key, kv => (kv.Value.start, kv.Value.target)));
                        emitted.Add(new MoveSelectionChangedEvent(ui.SelectedPos, validTargets));
                    }
                    var newCursorTool = UiSelectors.ComputeCursorTool(state.Mode, state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
                }
                if (IsMoveTarget(cursorTool))
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
                    var newCursorTool = UiSelectors.ComputeCursorTool(state.Mode, state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
                }
                if (IsMoveClearSelect(cursorTool))
                {
                    ui = ui with { SelectedPos = null };
                    emitted = new List<GameEvent> { new MoveSelectionChangedEvent(null, new HashSet<Vector2Int>()) };
                    var newCursorTool = UiSelectors.ComputeCursorTool(state.Mode, state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), new HashSet<Vector2Int>()));
                    return (state with { Ui = ui }, emitted);
                }
                if (IsMoveClearPair(cursorTool))
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
                    var newCursorTool = UiSelectors.ComputeCursorTool(state.Mode, state.Net, ui);
                    emitted.Add(new MoveHoverChangedEvent(ui.HoveredPos, state.Net.IsMySubphase(), new HashSet<Vector2Int>()));
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


