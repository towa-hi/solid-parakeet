using Contract;

public static class ModeDecider
{
    public static ClientMode DecideClientMode(GameNetworkState netState, NetworkDelta delta)
    {
        Phase serverPhase = netState.lobbyInfo.phase;
        // Resolve takes precedence when a TurnResolve payload exists
        if (delta.TurnResolve.HasValue)
        {
            return ClientMode.Resolve;
        }
        switch (serverPhase)
        {
            case Phase.SetupCommit: return ClientMode.Setup;
            case Phase.MoveCommit:
            case Phase.MoveProve:
            case Phase.RankProve:
                return ClientMode.Move;
            case Phase.Finished: return ClientMode.Finished;
            case Phase.Aborted: return ClientMode.Aborted;
            case Phase.Lobby: return ClientMode.Lobby;
            default: return ClientMode.Lobby;
        }
    }
}


