using System;
using System.Collections.Generic;
using System.Linq;

// NOTE: GameState is supposed to be a pure c# class that can work without UnityEngine
// but we still use it for stuff like Vector2Int and [SerializeField] out of convenience.
// When writing code here, kindly avoid using UnityEngine functions and classes that aren't
// simple structs
 
using UnityEngine;

[System.Serializable]
public class GameState
{
    public BoardDef board;
    public GamePhase phase = GamePhase.UNINITIALIZED;
    [SerializeField] public List<Pawn> pawns;

    public event Action<Pawn> PawnAdded;
    public event Action<Pawn> PawnDeleted;
    public event Action<GamePhase> PhaseChanged;
    
    public GameState(BoardDef inBoard)
    {
        board = inBoard;
        pawns = new List<Pawn>();
    }

    public void OnSetupPawnSelectorSelected(PawnDef pawnDef, Vector2Int pos)
    {
        if (!IsPositionValid(pos))
        {
            throw new ArgumentOutOfRangeException();
        }
        Pawn pawnAlreadyAtPos = GetPawnFromPos(pos);
        if (pawnAlreadyAtPos != null)
        {
            DeletePawn(pawnAlreadyAtPos);
        }
        AddPawn(pawnDef, pos);
    }
    
    void AddPawn(PawnDef pawnDef, Vector2Int pos)
    {
        if (pawnDef == null)
        {
            return;
        }
        Pawn pawn = new Pawn(pawnDef, pos);
        pawns.Add(pawn);
        PawnAdded?.Invoke(pawn);
        //BoardManager.instance.SpawnPawnView(pawn, pos);
    }

    public Pawn GetPawnFromPos(Vector2Int pos)
    {
        return pawns.FirstOrDefault(pawn => pawn.pos == pos);
    }

    public void DeletePawn(Pawn pawn)
    {
        bool removed = pawns.Remove(pawn);
        PawnDeleted?.Invoke(pawn);
        //BoardManager.instance.DeletePawnView(pawn);
    }
    
    public void ChangePhase(GamePhase inPhase)
    {
        phase = inPhase;
        //Debug.Log("Game phase changed to: " + phase.ToString());

        switch (phase)
        {
            case GamePhase.UNINITIALIZED:
                //Debug.LogError("Don't change GameState's phase to UNINITIALIZED!!!");
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

    bool IsPositionValid(Vector2Int pos)
    {
        if (board == null)
        {
            return false;
        }

        return pos.x >= 0 && pos.x < board.boardSize.x && pos.y >= 0 && pos.y < board.boardSize.y;
    }
    
    void OnSetupPhase()
    {
        //Debug.Log("Setting up game. Initializing board, player positions, etc.");
        
    }

    void OnMovePhase()
    {
        //Debug.Log("Player's move phase. Awaiting player input for moves...");
    }

    void OnResolvePhase()
    {
        //Debug.Log("Resolving actions. Processing the results of moves...");
    }

    void OnEndPhase()
    {
        //Debug.Log("Game has ended. Final cleanup.");
    }

}
