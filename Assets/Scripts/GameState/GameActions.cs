using System;
using Contract;
using System.Collections.Generic;
using UnityEngine;

namespace System.Runtime.CompilerServices {
    public class IsExternalInit {
    }
}

public abstract record GameAction;

public record NetworkStateChanged(GameNetworkState Net, NetworkDelta Delta) : GameAction;

public record UiWaitingForResponse(UiWaitingForResponseData Data) : GameAction;

// Movement input (new system)
public record MoveHoverAction(Vector2Int Pos) : GameAction;
public record MoveClickAt(Vector2Int Pos) : GameAction;
public record ResolvePrev() : GameAction;
public record ResolveNext() : GameAction;
public record ResolveSkip() : GameAction;

// Setup UI actions
public record SetupSelectRank(Rank? Rank) : GameAction;
public record SetupClearAll() : GameAction;
public record SetupAutoFill() : GameAction;
public record SetupCommitAt(Vector2Int Pos) : GameAction;
public record SetupUncommitAt(Vector2Int Pos) : GameAction;
public record SetupHoverAction(Vector2Int Pos) : GameAction;
public record SetupClickAt(Vector2Int Pos) : GameAction;

// just for waiting
public record UpdateState() : GameAction;
public record CommitSetup(CommitSetupReq Req) : GameAction;
public record CommitMoveAndProve(CommitMoveReq CommitReq, ProveMoveReq ProveReq) : GameAction;
public record ProveMove(ProveMoveReq Req) : GameAction;
public record ProveRank(ProveRankReq Req) : GameAction;

