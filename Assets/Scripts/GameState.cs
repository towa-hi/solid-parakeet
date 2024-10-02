using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class GameState
{
    public BoardDef board;
    public GamePhase phase = GamePhase.UNINITIALIZED;
    [SerializeField] public List<Pawn> pawns;
    
    public GameState(BoardDef inBoard)
    {
        board = inBoard;
        pawns = new List<Pawn>();
        Debug.Log("New GameState initialized with parameters");
    }

    public void OnSetupPawnSelectorSelected(PawnDef pawnDef, Vector2Int pos)
    {
        if (!IsPositionValid(pos))
        {
            throw new ArgumentOutOfRangeException();
        }
        Pawn pawnAlreadyAtPos = GetPawnFromPos(pos);
        Debug.Log(pawnAlreadyAtPos);
        if (pawnAlreadyAtPos != null)
        {
            DeletePawn(pawnAlreadyAtPos);
        }
        AddPawn(pawnDef, pos);
    }
    
    Pawn AddPawn(PawnDef pawnDef, Vector2Int pos)
    {
        if (pawnDef == null)
        {
            return null;
        }
        Pawn pawn = new Pawn(pawnDef, pos);
        pawns.Add(pawn);
        Debug.Log("added pawn");
        BoardManager.instance.SpawnPawnView(pawn, pos);
        return pawn;
    }

    public Pawn GetPawnFromPos(Vector2Int pos)
    {
        return pawns.FirstOrDefault(pawn => pawn.pos == pos);
    }

    public void DeletePawn(Pawn pawn)
    {
        bool removed = pawns.Remove(pawn);
        BoardManager.instance.DeletePawnView(pawn);
        if (removed)
        {
            Debug.Log($"Deleted pawn at position {pawn.pos}.");
        }
        else
        {
            Debug.LogWarning("Pawn not found in the list. Cannot delete.");
        }
    }
    
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

    bool IsPositionValid(Vector2Int pos)
    {
        if (board == null)
        {
            Debug.LogError("Board is not initialized.");
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
