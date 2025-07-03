using System;
using Contract;
using UnityEngine;

// AIDEV-NOTE: Event-driven architecture for game state updates - replaces PhaseChanges system
public static class GameEvents
{
    // Core game state events
    public static event Action<GameNetworkState> OnNetworkStateUpdated;
    public static event Action<Phase, Phase> OnPhaseChanged; // oldPhase, newPhase
    public static event Action<Subphase, Subphase> OnSubphaseChanged; // oldSubphase, newSubphase
    
    // View update events
    public static event Action OnPawnsUpdated;
    public static event Action OnBoardUpdated;
    public static event Action OnUIStateUpdated;
    
    // Input events
    public static event Action<Vector2Int> OnTileHovered;
    public static event Action<Vector2Int> OnTileClicked;
    public static event Action<SetupInputTool> OnInputToolChanged;
    
    // Phase-specific events
    public static event Action<Rank?> OnSelectedRankChanged;
    public static event Action<Vector2Int?> OnSelectedPawnChanged;
    
    public static void NetworkStateUpdated(GameNetworkState state)
    {
        OnNetworkStateUpdated?.Invoke(state);
    }
    
    public static void PhaseChanged(Phase oldPhase, Phase newPhase)
    {
        OnPhaseChanged?.Invoke(oldPhase, newPhase);
    }
    
    public static void SubphaseChanged(Subphase oldSubphase, Subphase newSubphase)
    {
        OnSubphaseChanged?.Invoke(oldSubphase, newSubphase);
    }
    
    public static void PawnsUpdated()
    {
        OnPawnsUpdated?.Invoke();
    }
    
    public static void BoardUpdated()
    {
        OnBoardUpdated?.Invoke();
    }
    
    public static void UIStateUpdated()
    {
        OnUIStateUpdated?.Invoke();
    }
    
    public static void TileHovered(Vector2Int pos)
    {
        OnTileHovered?.Invoke(pos);
    }
    
    public static void TileClicked(Vector2Int pos)
    {
        OnTileClicked?.Invoke(pos);
    }
    
    public static void InputToolChanged(SetupInputTool tool)
    {
        OnInputToolChanged?.Invoke(tool);
    }
    
    public static void SelectedRankChanged(Rank? rank)
    {
        OnSelectedRankChanged?.Invoke(rank);
    }
    
    public static void SelectedPawnChanged(Vector2Int? pos)
    {
        OnSelectedPawnChanged?.Invoke(pos);
    }
    
    // Cleanup method to remove all listeners (useful for scene changes)
    public static void RemoveAllListeners()
    {
        OnNetworkStateUpdated = null;
        OnPhaseChanged = null;
        OnSubphaseChanged = null;
        OnPawnsUpdated = null;
        OnBoardUpdated = null;
        OnUIStateUpdated = null;
        OnTileHovered = null;
        OnTileClicked = null;
        OnInputToolChanged = null;
        OnSelectedRankChanged = null;
        OnSelectedPawnChanged = null;
    }
}