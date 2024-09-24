using UnityEngine;

public class GameState
{
    public BoardDef board;
    public GamePhase phase;

    public GameState()
    {
        phase = GamePhase.SETUP; // Start with the setup phase
        Debug.Log("Game initialized. Current phase: SETUP");
        OnSetupPhase(); // Automatically start with setup
    }

    // Method to change the game phase
    public void ChangePhase(GamePhase inPhase)
    {
        phase = inPhase;
        Debug.Log("Game phase changed to: " + phase.ToString());

        switch (phase)
        {
            case GamePhase.SETUP:
                OnSetupPhase();
                break;

            case GamePhase.MOVE:
                OnMovePhase();
                break;

            case GamePhase.RESOLVE:
                OnResolvePhase();
                break;

            case GamePhase.END:
                OnEndPhase();
                break;
        }
    }

    // Handling the Setup phase
    void OnSetupPhase()
    {
        Debug.Log("Setting up game. Initializing board, player positions, etc.");
        // Initialize board, set up units or pieces, prepare the game state.
        // After setup, transition to the MOVE phase.
        ChangePhase(GamePhase.MOVE);
    }

    // Handling the Move phase
    void OnMovePhase()
    {
        Debug.Log("Player's move phase. Awaiting player input for moves...");
        // This is where player inputs their moves.
        // You could wait for player to make their move here and call ChangePhase(GamePhase.RESOLVE) when done.

        // Example of transitioning after move (this would normally happen after the move is completed):
        // AfterMoveCompleted() should be called after the player's move logic.
        // ChangePhase(GamePhase.RESOLVE);
    }

    // Handling the Resolve phase
    void OnResolvePhase()
    {
        Debug.Log("Resolving actions. Processing the results of moves...");
        // Process the results of the player's moves (attacks, interactions, etc.)
        // After resolving, check if the game should end, or return to MOVE phase.
        // For example:
        // if (gameOver) ChangePhase(GamePhase.END);
        // else ChangePhase(GamePhase.MOVE);
    }

    // Handling the End phase
    void OnEndPhase()
    {
        Debug.Log("Game has ended. Final cleanup.");
        // Show end game screen, score results, or any cleanup needed.
    }

}
