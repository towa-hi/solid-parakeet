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
                return new SetupCommitPhase();
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


