// using System;
// using System.Collections.Generic;
// using System.Linq;
//
// // NOTE: GameState is supposed to be a pure c# class that can work without UnityEngine
// // but we still use it for stuff like Vector2Int and [SerializeField] out of convenience.
// // When writing code here, kindly avoid using UnityEngine functions and classes that aren't
// // simple structs. GameState should not communicate with GameManager or BoardManager and
// // just trigger events that other classes can subscribe to
//  
// using UnityEngine;
//
// [System.Serializable]
// public class GameState
// {
//     public BoardDef board;
//     public GamePhase phase = GamePhase.UNINITIALIZED;
//     [SerializeField] public List<Pawn> pawns;
//     public event Action<Pawn> PawnAdded;
//     public event Action<Pawn> PawnDeleted;
//     public event Action<GamePhase> PhaseChanged;
//     
//     public GameState(BoardDef inBoard)
//     {
//         board = inBoard;
//         pawns = new List<Pawn>();
//     }
//
//     public void OnSetupPawnSelectorSelected(PawnDef pawnDef, Tile tile)
//     {
//         Pawn pawnAlreadyAtPos = GetPawnFromPos(tile.pos);
//         if (pawnAlreadyAtPos != null)
//         {
//             DeletePawn(pawnAlreadyAtPos);
//         }
//
//         Player playerSide = tile.setupPlayer;
//         if (playerSide == Player.NONE)
//         {
//             throw new Exception("Cannot spawn pawn on tile.setupPlayer == Player.NONE");
//         }
//         AddPawn(pawnDef, playerSide, tile);
//     }
//     
//     void AddPawn(PawnDef pawnDef, Player player, Tile tile)
//     {
//         if (pawnDef == null)
//         {
//             return;
//         }
//         Pawn pawn = new Pawn(pawnDef, player, tile.pos);
//         pawns.Add(pawn);
//         PawnAdded?.Invoke(pawn);
//     }
//
//     public Pawn GetPawnFromPos(Vector2Int pos)
//     {
//         return pawns.FirstOrDefault(pawn => pawn.pos == pos);
//     }
//
//     public void DeletePawn(Pawn pawn)
//     {
//         bool removed = pawns.Remove(pawn);
//         PawnDeleted?.Invoke(pawn);
//     }
//     
//     public void ChangePhase(GamePhase inPhase)
//     {
//         phase = inPhase;
//
//         switch (phase)
//         {
//             case GamePhase.UNINITIALIZED:
//                 //Debug.LogError("Don't change GameState's phase to UNINITIALIZED!!!");
//                 break;
//             case GamePhase.SETUP:
//                 OnSetupPhase();
//                 break;
//
//             case GamePhase.MOVE:
//                 OnMovePhase();
//                 break;
//
//             case GamePhase.RESOLVE:
//                 OnResolvePhase();
//                 break;
//
//             case GamePhase.END:
//                 OnEndPhase();
//                 break;
//             default:
//                 throw new ArgumentOutOfRangeException();
//         }
//     }
//
//     bool IsPositionValid(Vector2Int pos)
//     {
//         if (board == null)
//         {
//             return false;
//         }
//
//         return pos.x >= 0 && pos.x < board.boardSize.x && pos.y >= 0 && pos.y < board.boardSize.y;
//     }
//     
//     void OnSetupPhase()
//     {
//     }
//
//     void OnMovePhase()
//     {
//     }
//
//     void OnResolvePhase()
//     {
//     }
//
//     void OnEndPhase()
//     {
//     }
//
// }
