public enum MenuAction
{
    None = 0,

    // Navigation intents
    GotoStartMenu,
    GotoNetwork,
    GotoMainMenu,
    GotoLobbyCreate,
    GotoLobbyView,
    GotoLobbyJoin,
    GotoSettings,
    GotoGallery,
    GotoGame,

    // Operations (may result in navigation on success)
    ConnectToNetwork,
    GoOffline,
    CreateLobby,
    JoinGame,
    LeaveLobby,
    Refresh,
    SaveChanges,
    Quit,
    CheatModeToggle,
    FastModeToggle,
    DisplayBadgesToggle,
    MoveCameraToggle,
    MasterVolumeSlider,
    MusicVolumeSlider,
    EffectsVolumeSlider,
    EnterGame,
}


