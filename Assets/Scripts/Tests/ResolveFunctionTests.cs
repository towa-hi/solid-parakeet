using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ResolveFunctionTests
{
    // Helper method to create a pawn
    private SPawn CreatePawn(string pawnName, int player, int x, int y)
    {
        // Create SPawnDef
        SPawnDef pawnDef = new SPawnDef
        {
            pawnName = pawnName,
            power = GetPawnPower(pawnName)
        };

        // Create SPawn
        SPawn pawn = new SPawn
        {
            pawnId = Guid.NewGuid(),
            def = pawnDef,
            player = player,
            pos = new SVector2Int(x, y),
            isAlive = true,
            isVisibleToOpponent = false,
            isSetup = false,
            hasMoved = false
        };

        return pawn;
    }

    // Helper method to get pawn power based on name
    private int GetPawnPower(string pawnName)
    {
        Dictionary<string, int> pawnPowers = new Dictionary<string, int>
        {
            { "Marshal", 10 },
            { "General", 9 },
            { "Colonel", 8 },
            { "Major", 7 },
            { "Captain", 6 },
            { "Lieutenant", 5 },
            { "Sergeant", 4 },
            { "Miner", 3 },
            { "Scout", 2 },
            { "Spy", 1 },
            { "Bomb", 999 },
            { "Flag", 0 }
        };

        return pawnPowers[pawnName];
    }

    // Helper method to create a board
    private SBoardDef CreateBoard(int width, int height)
    {
        List<STile> tiles = new List<STile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tiles.Add(new STile
                {
                    pos = new SVector2Int(x, y),
                    isPassable = true,
                });
            }
        }

        return new SBoardDef
        {
            tiles = tiles.ToArray()
        };
    }

    [Test]
    public void Test_TwoScouts_CollideHeadOn()
    {
        // Arrange
        int boardWidth = 10;
        int boardHeight = 10;
        SBoardDef boardDef = CreateBoard(boardWidth, boardHeight);
        
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 0, 0);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        // Create red scout at (5, 1)
        SPawn redScout = CreatePawn("Scout", (int)Player.RED, 5, 1);

        // Create blue scout at (5, 10)
        SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 5, 9);

        // Set up initial game state
        SGameState gameState = new SGameState
        {
            winnerPlayer = (int)Player.NONE,
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, redScout, blueScout }
        };

        // Create moves
        SQueuedMove redMove = new SQueuedMove(redScout, new SVector2Int(5, 9));
        SQueuedMove blueMove = new SQueuedMove(blueScout, new SVector2Int(5, 1));

        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;

        // Assert
        // Both scouts should be eliminated
        SPawn? updatedRedScout = nextGameState.GetPawnFromId(redScout.pawnId);
        SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);
        Assert.IsTrue(SGameState.IsStateValid(nextGameState));
        Assert.IsTrue(updatedRedScout.HasValue && !updatedRedScout.Value.isAlive, "Red scout should be eliminated.");
        Assert.IsTrue(updatedBlueScout.HasValue && !updatedBlueScout.Value.isAlive, "Blue scout should be eliminated.");
    }

    [Test]
    public void Test_RedMarshalAttacksBlueGeneral_WhileBlueGeneralAttacksRedMajor()
    {
        // Arrange
        // Create a 3x1 board for simplicity: positions (0,0), (1,0), (2,0)
        SBoardDef boardDef = CreateBoard(3, 1);

        // Create pawns
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 5, 4);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        SPawn redMarshal = CreatePawn("Marshal", (int)Player.RED, 0, 0);  // (0,0)
        SPawn blueGeneral = CreatePawn("General", (int)Player.BLUE, 1, 0); // (1,0)
        SPawn redMajor = CreatePawn("Major", (int)Player.RED, 2, 0);     // (2,0)

        // Initialize game state
        SGameState gameState = new SGameState
        {
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, redMarshal, blueGeneral, redMajor }
        };

        // Define moves
        SQueuedMove redMove = new SQueuedMove(redMarshal.player, redMarshal.pawnId, redMarshal.pos, new SVector2Int(1, 0)); // Marshal to (1,0)
        SQueuedMove blueMove = new SQueuedMove(blueGeneral.player, blueGeneral.pawnId, blueGeneral.pos, new SVector2Int(2, 0)); // General to (2,0)

        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;

        // Assert
        // Fetch updated pawns
        SPawn? updatedRedMarshal = nextGameState.GetPawnFromId(redMarshal.pawnId);
        SPawn? updatedBlueGeneral = nextGameState.GetPawnFromId(blueGeneral.pawnId);
        SPawn? updatedRedMajor = nextGameState.GetPawnFromId(redMajor.pawnId);

        // Assertions based on expected outcome
        Assert.IsNotNull(updatedRedMarshal, "Red Marshal should exist in the game state.");
        Assert.IsNotNull(updatedBlueGeneral, "Blue General should exist in the game state.");
        Assert.IsNotNull(updatedRedMajor, "Red Major should exist in the game state.");

        // Check Red Marshal's new position and alive status
        Assert.AreEqual(new SVector2Int(1, 0), updatedRedMarshal.Value.pos, "Red Marshal should be at (1,0).");
        Assert.IsTrue(updatedRedMarshal.Value.isAlive, "Red Marshal should be alive.");

        // Check Blue General's new position and alive status
        Assert.AreEqual(new SVector2Int(2, 0), updatedBlueGeneral.Value.pos, "Blue General should be at (2,0).");
        Assert.IsTrue(updatedBlueGeneral.Value.isAlive, "Blue General should be alive.");

        // Check Red Major's alive status
        Assert.IsFalse(updatedRedMajor.Value.isAlive, "Red Major should be dead.");
    }
    
    [Test]
    public void Test_MutualAttacks_EqualPower_BothDie()
    {
        // Arrange
        // Create a 4x4 board: positions (2,2) and (3,2)
        SBoardDef boardDef = CreateBoard(4, 4);

        // Create pawns
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 0, 0);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        SPawn redSpy = CreatePawn("Spy", (int)Player.RED, 2, 2);        // (2,2) Power 1
        SPawn blueSpy = CreatePawn("Spy", (int)Player.BLUE, 3, 2);      // (3,2) Power 1

        // Initialize game state
        SGameState gameState = new SGameState
        {
            winnerPlayer = (int)Player.NONE,
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, redSpy, blueSpy }
        };

        // Define moves
        SQueuedMove redMove = new SQueuedMove(redSpy.player, redSpy.pawnId, redSpy.pos, new SVector2Int(3, 2)); // Spy to (3,2)
        SQueuedMove blueMove = new SQueuedMove(blueSpy.player, blueSpy.pawnId, blueSpy.pos, new SVector2Int(2, 2)); // Spy to (2,2)

        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;

        // Assert
        // Fetch updated pawns
        SPawn? updatedRedSpy = nextGameState.GetPawnFromId(redSpy.pawnId);
        SPawn? updatedBlueSpy = nextGameState.GetPawnFromId(blueSpy.pawnId);

        // Ensure pawns exist
        Assert.IsNotNull(updatedRedSpy, "Red Spy should exist in the game state.");
        Assert.IsNotNull(updatedBlueSpy, "Blue Spy should exist in the game state.");

        // Both spies should be eliminated
        Assert.AreEqual(new SVector2Int(-666, -666), updatedRedSpy.Value.pos, "Red Spy should be at PURGATORY (-666, -666).");
        Assert.IsFalse(updatedRedSpy.Value.isAlive, "Red Spy should be dead.");

        Assert.AreEqual(new SVector2Int(-666, -666), updatedBlueSpy.Value.pos, "Blue Spy should be at PURGATORY (-666, -666).");
        Assert.IsFalse(updatedBlueSpy.Value.isAlive, "Blue Spy should be dead.");
    }
    
    [Test]
    public void Test_PawnsSwapPositions()
    {
        // Arrange
        // Create a 6x6 board to accommodate positions (3,3) and (4,3)
        SBoardDef boardDef = CreateBoard(6, 6);

        // Create pawns
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 0, 0);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        SPawn redColonel = CreatePawn("Colonel", (int)Player.RED, 3, 3);   // (3,3)
        SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 4, 3);     // (4,3)

        // Initialize game state
        SGameState gameState = new SGameState
        {
            winnerPlayer = (int)Player.NONE,
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, redColonel, blueScout }
        };

        // Define moves
        SQueuedMove redMove = new SQueuedMove(redColonel.player, redColonel.pawnId, redColonel.pos, new SVector2Int(4, 3)); // Colonel to (4,3)
        SQueuedMove blueMove = new SQueuedMove(blueScout.player, blueScout.pawnId, blueScout.pos, new SVector2Int(3, 3)); // Scout to (3,3)

        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;

        // Assert
        // Fetch updated pawns
        SPawn? updatedRedColonel = nextGameState.GetPawnFromId(redColonel.pawnId);
        SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);

        // Ensure pawns exist
        Assert.IsNotNull(updatedRedColonel, "Red Colonel should exist in the game state.");
        Assert.IsNotNull(updatedBlueScout, "Blue Scout should exist in the game state.");

        // Check Red Colonel's new position and alive status
        Assert.AreEqual(new SVector2Int(4, 3), updatedRedColonel.Value.pos, "Red Colonel should be at (4,3).");
        Assert.IsTrue(updatedRedColonel.Value.isAlive, "Red Colonel should be alive.");

        // Check Blue Scout's alive status and position
        Assert.AreEqual(new SVector2Int(-666, -666), updatedBlueScout.Value.pos, "Blue Scout should be at PURGATORY (-666, -666).");
        Assert.IsFalse(updatedBlueScout.Value.isAlive, "Blue Scout should be dead.");
    }

    [Test]
    public void Test_BlueWinsByFlagCapture()
    {
        // Arrange
        // Create a 6x6 board to accommodate positions (3,3) and (4,3)
        SBoardDef boardDef = CreateBoard(6, 6);

        // Create pawns
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 0, 0);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        SPawn redColonel = CreatePawn("Colonel", (int)Player.RED, 3, 3);
        SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 1, 0);
        SGameState gameState = new SGameState
        {
            winnerPlayer = (int)Player.NONE,
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, redColonel, blueScout }
        };
        
        // Define moves
        SQueuedMove redMove = new SQueuedMove(redColonel.player, redColonel.pawnId, redColonel.pos, new SVector2Int(4, 3));
        SQueuedMove blueMove = new SQueuedMove(blueScout.player, blueScout.pawnId, blueScout.pos, new SVector2Int(0, 0));
        
        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;
        
        // Assert
        // Fetch updated pawns
        SPawn? updatedRedColonel = nextGameState.GetPawnFromId(redColonel.pawnId);
        SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);
        SPawn? updatedRedFlag = nextGameState.GetPawnFromId(redFlag.pawnId);
        SPawn? updatedBlueFlag = nextGameState.GetPawnFromId(blueFlag.pawnId);

        // Check Red alive
        Assert.IsFalse(updatedRedFlag.Value.isAlive, "Red Flag should be dead.");

        // Check winner
        Assert.AreEqual((int)Player.BLUE, nextGameState.winnerPlayer, "Blue should win the game");
        
    }
    
    [Test]
    public void Test_RedWinsByNoMoves()
    {
        // Arrange
        SBoardDef boardDef = CreateBoard(10, 10);

        // Create pawns
        
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 0, 0);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        
        
        SPawn blueBomb1 = CreatePawn("Bomb", (int)Player.BLUE, 8, 9);
        SPawn blueBomb2 = CreatePawn("Bomb", (int)Player.BLUE, 9, 8);
        SPawn blueTrappedScout = CreatePawn("Scout", (int)Player.BLUE, 9, 9);
        
        
        SPawn redColonel = CreatePawn("Colonel", (int)Player.RED, 4, 1);
        SPawn blueTakenScout = CreatePawn("Scout", (int)Player.BLUE, 5, 2);
        SGameState gameState = new SGameState
        {
            winnerPlayer = (int)Player.NONE,
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, blueBomb1, blueBomb2, blueTrappedScout, redColonel, blueTakenScout}
        };
        
        // Define moves
        SQueuedMove redMove = new SQueuedMove(redColonel, new SVector2Int(4, 2));
        SQueuedMove blueMove = new SQueuedMove(blueTakenScout, new SVector2Int(4, 2));
        
        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;
        
        // Assert
        // Fetch updated pawns
        SPawn? updatedBlueTrappedScout = nextGameState.GetPawnFromId(blueTrappedScout.pawnId);
        SPawn? updatedBlueTakenScout = nextGameState.GetPawnFromId(blueTakenScout.pawnId);
        SPawn? updatedRedFlag = nextGameState.GetPawnFromId(redFlag.pawnId);
        SPawn? updatedBlueFlag = nextGameState.GetPawnFromId(blueFlag.pawnId);

        // Check scout dead alive
        Assert.IsFalse(updatedBlueTakenScout.Value.isAlive, "Blue taken scout should be dead.");
        Assert.IsTrue(updatedBlueTrappedScout.Value.isAlive, "Blue trapped scout should be alive");
        // Check winner
        Assert.AreEqual((int)Player.RED, nextGameState.winnerPlayer, "Red should win the game");
        
    }
    
    [Test]
    public void Test_ScoutHitsMovingPiece()
    {
        // Arrange
        SBoardDef boardDef = CreateBoard(10, 10);

        // Create pawns
        
        SPawn redFlag = CreatePawn("Flag", (int)Player.RED, 0, 0);
        SPawn blueFlag = CreatePawn("Flag", (int)Player.BLUE, 5, 5);
        
        
        SPawn blueScout = CreatePawn("Scout", (int)Player.BLUE, 8, 9);
        SPawn redColonel = CreatePawn("Colonel", (int)Player.RED, 8, 2);
        SGameState gameState = new SGameState
        {
            winnerPlayer = (int)Player.NONE,
            player = (int)Player.NONE,
            boardDef = boardDef,
            pawns = new SPawn[] {redFlag, blueFlag, blueScout, redColonel},
        };
        
        // Define moves
        SQueuedMove redMove = new SQueuedMove(redColonel, new SVector2Int(9, 2));
        SQueuedMove blueMove = new SQueuedMove(blueScout, new SVector2Int(8, 2));
        
        // Act
        SGameState nextGameState = SGameState.Resolve(gameState, redMove, blueMove).gameState;
        
        // Assert
        // Fetch updated pawns
        SPawn? updatedBlueScout = nextGameState.GetPawnFromId(blueScout.pawnId);
        SPawn? updatedRedColonel = nextGameState.GetPawnFromId(redColonel.pawnId);

        // Check scout dead alive
        Assert.IsFalse(updatedBlueScout.Value.isAlive, "Blue scout should be dead.");
        Assert.IsTrue(updatedRedColonel.Value.isAlive, "Red colonel scout should be alive");
        // Check winner
        Assert.AreEqual((int)Player.RED, nextGameState.winnerPlayer, "Red should win the game");
        
    }
    // TODO: make a test for scout vs spy when spy moves
}

