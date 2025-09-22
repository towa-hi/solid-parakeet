using System;
using System.Collections.Generic;
using Contract;
using UnityEngine;

public static class ViewEventBus
{
    public static event Action<bool> OnSetupModeChanged;
    public static event Action<Vector2Int, bool> OnSetupHoverChanged;
    public static event Action<Dictionary<PawnId, Rank?>, Dictionary<PawnId, Rank?>> OnSetupPendingChanged;
    public static event Action<Rank?, Rank?> OnSetupRankSelected;
    public static event Action<SetupInputTool> OnSetupCursorToolChanged;

    public static void RaiseSetupModeChanged(bool active) => OnSetupModeChanged?.Invoke(active);
    public static void RaiseSetupHoverChanged(Vector2Int pos, bool isMyTurn) => OnSetupHoverChanged?.Invoke(pos, isMyTurn);
    public static void RaiseSetupPendingChanged(Dictionary<PawnId, Rank?> oldMap, Dictionary<PawnId, Rank?> newMap) => OnSetupPendingChanged?.Invoke(oldMap, newMap);
    public static void RaiseSetupRankSelected(Rank? oldRank, Rank? newRank) => OnSetupRankSelected?.Invoke(oldRank, newRank);
    public static void RaiseSetupCursorToolChanged(SetupInputTool tool) => OnSetupCursorToolChanged?.Invoke(tool);
}


