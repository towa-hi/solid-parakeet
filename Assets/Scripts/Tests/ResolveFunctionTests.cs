// using NUnit.Framework;
// using System;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class ResolveFunctionTests
// {
//     // Helper method to create a pawn
//     private SPawn CreatePawn(string pawnName, int player, int x, int y)
//     {
//         // Create SPawnDef
//         SPawnDef pawnDef = new SPawnDef
//         {
//             pawnName = pawnName,
//             power = GetPawnPower(pawnName)
//         };
//
//         // Create SPawn
//         SPawn pawn = new SPawn
//         {
//             pawnId = Guid.NewGuid(),
//             def = pawnDef,
//             player = player,
//             pos = new SVector2Int(x, y),
//             isAlive = true,
//             isVisibleToOpponent = false,
//             isSetup = false,
//             hasMoved = false
//         };
//
//         return pawn;
//     }
//
//     // Helper method to get pawn power based on name
//     private int GetPawnPower(string pawnName)
//     {
//         Dictionary<string, int> pawnPowers = new Dictionary<string, int>
//         {
//             { "Marshal", 10 },
//             { "General", 9 },
//             { "Colonel", 8 },
//             { "Major", 7 },
//             { "Captain", 6 },
//             { "Lieutenant", 5 },
//             { "Sergeant", 4 },
//             { "Miner", 3 },
//             { "Scout", 2 },
//             { "Spy", 1 },
//             { "Bomb", 999 },
//             { "Flag", 0 }
//         };
//
//         return pawnPowers[pawnName];
//     }
//
//     // Helper method to create a board
//     private SBoardDef CreateBoard(int width, int height)
//     {
//         List<STile> tiles = new List<STile>();
//
//         for (int x = 1; x <= width; x++)
//         {
//             for (int y = 1; y <= height; y++)
//             {
//                 tiles.Add(new STile
//                 {
//                     pos = new SVector2Int(x, y),
//                     isPassable = true,
//                 });
//             }
//         }
//
//         return new SBoardDef
//         {
//             tiles = tiles.ToArray()
//         };
//     }
//
//     [Test]
//     public void Test_TwoScouts_CollideHeadOn()
//     {
//         // Arrange
//         int boardWidth = 10;
//         int boardHeight = 10;
//         SBoardDef boardDef = CreateBoard(boardWidth, boardHeight);
//
//         // Create red scout at (5, 1)
//         SPawn redScout = CreatePawn("Scout", (int)Player.RED, 5, 1);
//
//         // Create blue scout at (5, 10)
//         SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 5, 10);
//
//         // Set up initial game state
//         SGameState gameState = new SGameState
//         {
//             player = (int)Player.NONE,
//             boardDef = boardDef,
//             pawns = new SPawn[] { redScout, blueScout }
//         };
//
//         // Create moves
//         SQueuedMove redMove = new SQueuedMove((int)Player.RED, redScout, new SVector2Int(5, 10));
//         SQueuedMove blueMove = new SQueuedMove((int)Player.BLUE, blueScout, new SVector2Int(5, 1));
//
//         // Act
//         SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove);
//
//         // Assert
//         // Both scouts should be eliminated
//         SPawn? updatedRedScout = nextGameState.GetPawnFromId(redScout.pawnId);
//         SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);
//
//         Assert.IsTrue(updatedRedScout.HasValue && !updatedRedScout.Value.isAlive, "Red scout should be eliminated.");
//         Assert.IsTrue(updatedBlueScout.HasValue && !updatedBlueScout.Value.isAlive, "Blue scout should be eliminated.");
//     }
//
//     [Test]
//     public void Test_TwoScouts_CollidePerpendicularly()
//     {
//         // Arrange
//         int boardWidth = 10;
//         int boardHeight = 10;
//         SBoardDef boardDef = CreateBoard(boardWidth, boardHeight);
//
//         // Create red scout at (1, 5)
//         SPawn redScout = CreatePawn("Scout", (int)Player.RED, 0, 0);
//
//         // Create blue scout at (5, 1)
//         SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 7, 2);
//
//         // Set up initial game state
//         SGameState gameState = new SGameState
//         {
//             player = (int)Player.NONE,
//             boardDef = boardDef,
//             pawns = new SPawn[] { redScout, blueScout }
//         };
//
//         // Create moves
//         SQueuedMove redMove = new SQueuedMove((int)Player.RED, redScout, new SVector2Int(5, 3));
//         SQueuedMove blueMove = new SQueuedMove((int)Player.BLUE, blueScout, new SVector2Int(4, 2));
//
//         // Act
//         SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove);
//
//         // Assert
//         // Both scouts should be eliminated
//         SPawn? updatedRedScout = nextGameState.GetPawnFromId(redScout.pawnId);
//         SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);
//
//         Assert.IsTrue(updatedRedScout.HasValue && !updatedRedScout.Value.isAlive, "Red scout should be eliminated.");
//         Assert.IsTrue(updatedBlueScout.HasValue && !updatedBlueScout.Value.isAlive, "Blue scout should be eliminated.");
//     }
//
//     [Test]
//     public void Test_ScoutCollidesWithMiner_MinerMovesIntoScoutPath()
//     {
//         // Arrange
//         int boardWidth = 10;
//         int boardHeight = 10;
//         SBoardDef boardDef = CreateBoard(boardWidth, boardHeight);
//
//         // Create red miner at (5, 3)
//         SPawn redMiner = CreatePawn("Miner", (int)Player.RED, 5, 3);
//
//         // Create blue scout at (5, 10)
//         SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 5, 10);
//
//         // Set up initial game state
//         SGameState gameState = new SGameState
//         {
//             player = (int)Player.NONE,
//             boardDef = boardDef,
//             pawns = new SPawn[] { redMiner, blueScout }
//         };
//
//         // Create moves
//         SQueuedMove redMove = new SQueuedMove((int)Player.RED, redMiner, new SVector2Int(5, 4));
//         SQueuedMove blueMove = new SQueuedMove((int)Player.BLUE, blueScout, new SVector2Int(5, 3));
//
//         // Act
//         SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove);
//
//         // Assert
//         // Conflict should occur at (5,4)
//         // According to the game rules, Miner (power 3) defeats Scout (power 2)
//
//         SPawn? updatedRedMiner = nextGameState.GetPawnFromId(redMiner.pawnId);
//         SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);
//
//         // Check if conflict occurred and the correct pawn was eliminated
//         Assert.IsTrue(updatedRedMiner.HasValue && updatedRedMiner.Value.isAlive, "Red miner should be alive.");
//         Assert.IsTrue(updatedBlueScout.HasValue && !updatedBlueScout.Value.isAlive, "Blue scout should be eliminated.");
//         Assert.AreEqual(new SVector2Int(5, 4), updatedRedMiner.Value.pos, "Red miner should be at position (5,4).");
//     }
// }
