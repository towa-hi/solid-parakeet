using System;
using UnityEngine;

[System.Serializable]
public class GameState
{
    public BoardDef board;
    public GamePhase phase = GamePhase.UNINITIALIZED;

    public GameState(BoardDef inBoard)
    {
        board = inBoard;
        Debug.Log("New GameState initialized with parameters");
    }

    // Method to change the game phase
    public void ChangePhase(GamePhase inPhase)
    {
        phase = inPhase;
        //Debug.Log("Game phase changed to: " + phase.ToString());

        switch (phase)
        {
            case GamePhase.UNINITIALIZED:
                Debug.LogError("Don't change GameState's phase to UNINITIALIZED!!!");
                break;
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
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // Handling the Setup phase
    void OnSetupPhase()
    {
        //Debug.Log("Setting up game. Initializing board, player positions, etc.");
        
    }

    // Handling the Move phase
    void OnMovePhase()
    {
        //Debug.Log("Player's move phase. Awaiting player input for moves...");
    }

    // Handling the Resolve phase
    void OnResolvePhase()
    {
        //Debug.Log("Resolving actions. Processing the results of moves...");
    }

    // Handling the End phase
    void OnEndPhase()
    {
        //Debug.Log("Game has ended. Final cleanup.");
    }

}
