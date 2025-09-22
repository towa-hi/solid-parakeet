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

    public static ClientMode DecideClientMode(GameNetworkState netState, NetworkDelta delta, LocalUiState ui)
    {
        // If resolve payload exists, always Resolve
        if (delta.TurnResolve.HasValue)
        {
            return ClientMode.Resolve;
        }
        // If server says game is Finished/Aborted (no new resolve to show), go straight to Finished/Aborted
        if (netState.lobbyInfo.phase == Phase.Finished) return ClientMode.Finished;
        if (netState.lobbyInfo.phase == Phase.Aborted) return ClientMode.Aborted;
        // If UI indicates we are at Final resolve checkpoint (completed stepping locally)
        if (ui.Checkpoint == ResolveCheckpoint.Final)
        {
            // If the server has ended the game, show GameOver; otherwise go back to Move
            return ClientMode.Move;
        }
        // Otherwise fall back to server phase mapping
        return DecideClientMode(netState, delta);
    }
}


