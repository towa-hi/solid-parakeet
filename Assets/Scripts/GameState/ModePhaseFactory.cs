using Contract;

public static class ModePhaseFactory
{
    public static PhaseBase CreatePhase(ClientMode mode, GameNetworkState netState, NetworkDelta delta)
    {
        // Map based on authoritative server phase to preserve behavior
        Phase serverPhase = netState.lobbyInfo.phase;
        switch (serverPhase)
        {
            case Phase.SetupCommit:
#if USE_GAME_STORE
                // In flagged builds, do not instantiate SetupCommitPhase; use MoveCommitPhase placeholder.
                // Input and visuals are driven by ClientMode + event bus.
                return new MoveCommitPhase();
#else
                return new SetupCommitPhase();
#endif
            case Phase.MoveCommit:
                return delta.TurnResolve.HasValue ? new ResolvePhase(delta.TurnResolve.Value) : new MoveCommitPhase();
            case Phase.MoveProve:
                return new MoveProvePhase();
            case Phase.RankProve:
                return new RankProvePhase();
            case Phase.Finished:
            case Phase.Aborted:
                return delta.TurnResolve.HasValue ? new ResolvePhase(delta.TurnResolve.Value) : new FinishedPhase();
            case Phase.Lobby:
            default:
                throw new System.NotImplementedException();
        }
    }
}


