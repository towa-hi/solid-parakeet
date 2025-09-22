using Contract;

public record GameSnapshot
{
    // Authoritative snapshot from network
    public GameNetworkState Net { get; init; }
    // Derived client mode
    public ClientMode Mode { get; init; }
    // Local per-mode UI state
    public LocalUiState Ui { get; init; }
    // Placeholder for expansion during migration
    public static GameSnapshot Empty => new GameSnapshot { Ui = LocalUiState.Empty };
}


