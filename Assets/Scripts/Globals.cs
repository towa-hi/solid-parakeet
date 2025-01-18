using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class Globals
{
    public static Vector2Int PURGATORY = new(-666, -666);
    public static float PAWNMOVEDURATION = 1f;
    public static float HOVEREDHEIGHT = 0.1f;
    // Static instance of GameInputActions to be shared among all Hoverable instances
    public static readonly InputSystem_Actions inputActions = new();
    

    public static bool IsNicknameValid(string nickname)
    {
        // Check length constraints
        if (string.IsNullOrEmpty(nickname) || nickname.Length >= 16)
            return false;

        // Check for alphanumeric characters and spaces only
        return Regex.IsMatch(nickname, @"^[a-zA-Z0-9 ]+$");
    }

    public static bool IsPasswordValid(string password)
    {
        return true;
    }
    
    public static Guid LoadOrGenerateClientId()
    {
        if (PlayerPrefs.HasKey("ClientId"))
        {
            string clientIdStr = PlayerPrefs.GetString("ClientId");
            if (Guid.TryParse(clientIdStr, out Guid parsedId))
            {
                Debug.Log($"Loaded existing ClientId: {parsedId}");
                return parsedId;
            }
            else
            {
                Guid newId = Guid.NewGuid();
                PlayerPrefs.SetString("ClientId", newId.ToString());
                PlayerPrefs.Save();
                Debug.Log($"Generated new ClientId (invalid stored ID was replaced): {newId}");
                return newId;
            }
        }
        else
        {
            Guid newId = Guid.NewGuid();
            PlayerPrefs.SetString("ClientId", newId.ToString());
            PlayerPrefs.Save();
            Debug.Log($"Generated new ClientId: {newId}");
            return newId;
        }
    }
    
    public static string GetNickname()
    {
        string nick = PlayerPrefs.GetString("nickname");
        if (nick == String.Empty)
        {
            PlayerPrefs.SetString("nickname", "defaultnick");
        }
        return PlayerPrefs.GetString("nickname");
    }
    
    public static List<PawnDef> GetOrderedPawnList()
    {
        List<PawnDef> orderedPawns = new List<PawnDef>
        {
            Resources.Load<PawnDef>("Pawn/00-throne"),
            Resources.Load<PawnDef>("Pawn/01-assassin"),
            Resources.Load<PawnDef>("Pawn/02-scout"),
            Resources.Load<PawnDef>("Pawn/03-seer"),
            Resources.Load<PawnDef>("Pawn/04-grunt"),
            Resources.Load<PawnDef>("Pawn/05-knight"),
            Resources.Load<PawnDef>("Pawn/06-wraith"),
            Resources.Load<PawnDef>("Pawn/07-reaver"),
            Resources.Load<PawnDef>("Pawn/08-herald"),
            Resources.Load<PawnDef>("Pawn/09-champion"),
            Resources.Load<PawnDef>("Pawn/10-warlord"),
            Resources.Load<PawnDef>("Pawn/11-trap"),
            Resources.Load<PawnDef>("Pawn/99-unknown"),
        };
        return orderedPawns;
    }
    
    public static float EaseOutQuad(float t)
    {
        return t * (2 - t);
    }
    
    public static string ShortGuid(Guid guid)
    {
        return guid.ToString().Substring(0, 4);
    }

    public static Vector2Int[] GetDirections(Vector2Int pos, bool isHex)
    {
        if (isHex)
        {
            Vector2Int[] neighbors = new Vector2Int[6];
            bool oddCol = pos.x % 2 == 1; // Adjust for origin offset
            
            if (oddCol)
            {
                neighbors[0] = new Vector2Int(0, 1);  // top
                neighbors[1] = new Vector2Int(-1, 0);  // top right
                neighbors[2] = new Vector2Int(-1, -1);  // bot right
                neighbors[3] = new Vector2Int(0, -1); // bot
                neighbors[4] = new Vector2Int(1, -1); // bot left
                neighbors[5] = new Vector2Int(1, 0);  // top left
            }
            else
            {
                neighbors[0] = new Vector2Int(0, 1);  // top
                neighbors[1] = new Vector2Int(-1, 1);  // top right
                neighbors[2] = new Vector2Int(-1, -0); // bot right
                neighbors[3] = new Vector2Int(0, -1); // bot
                neighbors[4] = new Vector2Int(1, 0);// bot left
                neighbors[5] = new Vector2Int(1, 1); // top left
            }
            
            return neighbors;
        }
        else
        {
            return new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };
        }
    }
    
    public static Vector2Int[] GetNeighbors(Vector2Int pos, bool isHex)
    {
        Vector2Int[] directions = Globals.GetDirections(pos, isHex);
        if (isHex)
        {
            Vector2Int[] neighbors = new Vector2Int[6];
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i] = pos + directions[i];
            }
            return neighbors;
        }
        else
        {
            Vector2Int[] neighbors = new Vector2Int[4];
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i] = pos + directions[i];
            }
            return neighbors;
        }
    }
}

public enum MessageGenre : uint
{
    SERVERERROR, // only called when error is server fault, disconnects the client forcibly
    REGISTERCLIENT, // request: clientId only, response: none 
    REGISTERNICKNAME, // request: nickname, response: success
    GAMELOBBY, // request: lobby parameters, response: lobby
    LEAVEGAMELOBBY, // request: clientId only, response: leave notification
    JOINGAMELOBBY, // request: password, response: lobby 
    READYLOBBY,
    GAMESTART,
    GAMESETUP, // request holds piece deployment or move data, response is a gamestate object
    SETUPFINISHED,
    MOVE,
    RESOLVE,
}



[Serializable]
public class SLobby
{
    public Guid lobbyId;
    public Guid hostId;
    public Guid guestId;
    public SBoardDef sBoardDef;
    public int gameMode;
    public bool isGameStarted;
    public string password;
    public bool hostReady;
    public bool guestReady;
    public SLobby() { }

    public bool IsHost(Guid clientId)
    {
        return clientId == hostId;
    }
}

public struct SSetupParameters
{
    public int player;
    public SBoardDef board;
    public SMaxPawnsPerRank[] maxPawns;
    public bool mustPlaceAllPawns;

}

public interface IGameClient
{
    // Events
    event Action<Response<string>> OnRegisterClientResponse;
    event Action<Response<string>> OnDisconnect;
    event Action<ResponseBase> OnErrorResponse;
    event Action<Response<string>> OnRegisterNicknameResponse;
    event Action<Response<SLobby>> OnGameLobbyResponse;
    event Action<Response<string>> OnLeaveGameLobbyResponse;
    event Action<Response<string>> OnJoinGameLobbyResponse;
    event Action<Response<SLobby>> OnReadyLobbyResponse;
    event Action<Response<SSetupParameters>> OnDemoStartedResponse;
    event Action<Response<bool>> OnSetupSubmittedResponse;
    event Action<Response<SGameState>> OnSetupFinishedResponse;
    event Action<Response<bool>> OnMoveResponse;
    event Action<Response<SResolveReceipt>> OnResolveResponse;
    
    
    // Methods
    void ConnectToServer();
    void SendRegisterNickname(string nicknameInput);
    void SendGameLobby();
    void SendGameLobbyLeaveRequest();
    void SendGameLobbyReadyRequest(bool ready);
    void SendStartGameDemoRequest();
    void SendSetupSubmissionRequest(SSetupPawn[] setupPawnList);
    void SendMove(SQueuedMove move);
}


public class ResponseBase
{
    public Guid requestId;
    public bool success;
    public int responseCode;
    public string message;
}

public class Response<T> : ResponseBase
{
    public T data;
    
    public Response() { }
    
    public Response(bool inSuccess, int inResponseCode, T inData)
    {
        success = inSuccess;
        responseCode = inResponseCode;
        data = inData;
    }

}

public class RequestBase
{
    public Guid requestId;
    public Guid clientId;
}

public class RegisterClientRequest : RequestBase
{
}

public class RegisterNicknameRequest : RequestBase
{
    public string nickname { get; set; }
}

public class GameLobbyRequest : RequestBase
{
    public int gameMode { get; set; }
    public SBoardDef sBoardDef { get; set; }
}

public class LeaveGameLobbyRequest : RequestBase
{
    
}

public class JoinGameLobbyRequest : RequestBase
{
    
}

public class ReadyGameLobbyRequest : RequestBase
{
    public bool ready { set; get; }
}

public class StartGameRequest : RequestBase
{
    public SSetupParameters setupParameters;
}

public class SetupRequest : RequestBase
{
    public int player;
    public SSetupPawn[] setupPawns;
}

public class MoveRequest : RequestBase
{
    public SQueuedMove move;
}


[Serializable]
public struct SQueuedMove
{
    public int player;
    public Guid pawnId;
    public Vector2Int initialPos;
    public Vector2Int pos;

    public SQueuedMove(in SPawn pawn, in Vector2Int inPos)
    {
        player = pawn.player;
        pawnId = pawn.pawnId;
        initialPos = pawn.pos;
        pos = inPos;
    }

    public SQueuedMove(Pawn pawn, Vector2Int inPos)
    {
        player = (int)pawn.player;
        pawnId = pawn.pawnId;
        initialPos = pawn.pos;
        pos = inPos;
    }
    
    public SQueuedMove(int inPlayer, Guid inPawnId, Vector2Int inInitialPos, Vector2Int inPos)
    {
        player = inPlayer;
        pawnId = inPawnId;
        initialPos = inInitialPos;
        pos = inPos;
    }
}

[Serializable]
public struct SGameState
{
    public int winnerPlayer;
    public int player;
    public SBoardDef boardDef;
    public SPawn[] pawns;
    
    public SGameState(int inPlayer, SBoardDef inBoardDef, SPawn[] inPawns)
    {
        winnerPlayer = (int)Player.NONE;
        player = inPlayer;
        boardDef = inBoardDef;
        pawns = inPawns;
    }
    
    public readonly STile[] GetMovableTiles(in SPawn pawn)
    {
        if (pawn.def.movementRange == 0)
        {
            return Array.Empty<STile>();
        }
        if (!pawn.isAlive)
        {
            return Array.Empty<STile>();
        }
        Vector2Int[] initialDirections = Globals.GetDirections(pawn.pos, boardDef.isHex);
        // Define the maximum possible number of movable tiles
        int maxMovableTiles = boardDef.tiles.Length; // Adjust based on your board size
        STile[] movableTiles = new STile[maxMovableTiles];
        int tileCount = 0;
        for (int dirIndex = 0; dirIndex < initialDirections.Length; dirIndex++)
        {
            Vector2Int currentPos = pawn.pos;
            int walkedTiles = 0;
            bool enemyEncountered = false;
            bool obstacleEncountered = false;
            while (walkedTiles < pawn.def.movementRange)
            {
                // directions change depending on odd or even col in hexagons so we have to get it again
                Vector2Int[] currentDirections = Globals.GetDirections(currentPos, boardDef.isHex);
                // peek one tile in this direction ahead
                currentPos += currentDirections[dirIndex];
                if (!IsPosValid(currentPos))
                {
                    obstacleEncountered = true;
                    break;
                }
                STile tile = GetTileByPos(currentPos);
                if (!tile.isPassable)
                {
                    obstacleEncountered = true;
                    break;
                }
                SPawn? pawnOnPos = GetPawnByPos(currentPos);
                if (pawnOnPos.HasValue)
                {
                    if (pawnOnPos.Value.player == pawn.player)
                    {
                        // Cannot move through own pawns
                        obstacleEncountered = true;
                        break;
                    }
                    else
                    {
                        // Tile is occupied by an enemy pawn
                        movableTiles[tileCount++] = tile;
                        enemyEncountered = true;
                        break;
                    }
                }
                else
                {
                    // Tile is unoccupied
                    movableTiles[tileCount++] = tile;
                }
                walkedTiles++;
            }
        }
        // Create a final array of the correct size
        STile[] result = new STile[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            result[i] = movableTiles[i];
        }
        return result;
    }

    public static bool IsMoveValid(in SGameState gameState, in SQueuedMove move)
    {
        if (gameState.boardDef.IsPosValid(move.pos))
        {
            STile destinationTile = gameState.GetTileByPos(move.pos);
            if (!destinationTile.isPassable)
            {
                // move is to impassable tile
                return false;
            }
        }
        else
        {
            return false;
        }
        try
        {
            SPawn movingPawn = gameState.GetPawnById(move.pawnId);
            if (move.player != movingPawn.player)
            {
                return false;
            }
            var maybePawn = gameState.GetPawnByPos(move.pos);
            if (maybePawn.HasValue)
            {
                SPawn obstructingPawn = maybePawn.Value;
                if (obstructingPawn.player == move.player)
                {
                    return false;
                }
            }
            bool isInMovableTiles = false;
            foreach (STile tile in gameState.GetMovableTiles(movingPawn))
            {
                if (tile.pos == move.pos)
                {
                    isInMovableTiles = true;
                }
            }
            if (!isInMovableTiles)
            {
                return false;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        return true;
    }
    
    public readonly bool IsPosValid(Vector2Int pos)
    {
        return boardDef.tiles.Any(tile => tile.pos == pos);

    }

    public static SGameState Censor(in SGameState masterGameState, int targetPlayer)
    {
        if (masterGameState.player != (int)Player.NONE)
        {
            throw new Exception("Censor can only be done on master game states!");
        }
        SGameState censoredGameState = new SGameState
        {
            winnerPlayer = masterGameState.winnerPlayer,
            player = targetPlayer,
            boardDef = masterGameState.boardDef,
        };
        SPawn[] censoredPawns = new SPawn[masterGameState.pawns.Length];
        for (int i = 0; i < masterGameState.pawns.Length; i++)
        {
            SPawn serverPawn = masterGameState.pawns[i];
            SPawn censoredPawn;
            if (serverPawn.player != targetPlayer)
            {
                if (PlayerPrefs.GetInt("CHEATMODE") == 1)
                {
                    censoredPawn = serverPawn;
                    censoredPawn.isVisibleToOpponent = true;
                }
                else
                {
                    if (serverPawn.isVisibleToOpponent)
                    {
                        censoredPawn = serverPawn;
                    }
                    else
                    {
                        censoredPawn = serverPawn.Censor();
                    }
                }
            }
            else
            {
                censoredPawn = serverPawn;
            }
            censoredPawns[i] = censoredPawn;
        }
        censoredGameState.pawns = censoredPawns;
        return censoredGameState;
    }
    
    public static SQueuedMove? GenerateValidMove(in SGameState gameState, int targetPlayer)
    {
        // NOTE: gameState should be censored if it isn't already for fairness
        List<SQueuedMove> allPossibleMoves = new List<SQueuedMove>();
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.player == targetPlayer)
            {
                STile[] movableTiles = gameState.GetMovableTiles(pawn);
                foreach (var tile in movableTiles)
                {
                    allPossibleMoves.Add(new SQueuedMove(targetPlayer, pawn.pawnId, pawn.pos, tile.pos));
                }
            }
        }
        if (allPossibleMoves.Count == 0)
        {
            return null;
        }
        System.Random random = new();
        int randomIndex = random.Next(0, allPossibleMoves.Count);
        SQueuedMove randomMove = allPossibleMoves[randomIndex];
        SPawn randomPawn = gameState.GetPawnById(randomMove.pawnId);
        Debug.Log($"GenerateValidMove chose {randomMove.pawnId} {randomPawn.def.pawnName} from {randomPawn.pos} to {randomMove.pos}");
        if (!IsMoveValid(gameState, randomMove))
        {
            return null;
        }
        return randomMove;
    }
    
    public readonly STile GetTileByPos(Vector2Int pos)
    {
        foreach (STile tile in boardDef.tiles.Where(tile => tile.pos == pos))
        {
            return tile;
        }
        throw new ArgumentOutOfRangeException($"GetTileByPos tile on pos {pos.ToString()} not found!");
    }

    public readonly SPawn? GetPawnByPos(in Vector2Int pos)
    {
        Vector2Int i = pos;
        foreach (SPawn pawn in pawns.Where(pawn => pawn.pos == i))
        {
            return pawn;
        }
        return null;
    }

    public readonly SPawn GetPawnById(Guid pawnId)
    {
        foreach (SPawn pawn in pawns)
        {
            if (pawn.pawnId == pawnId)
            {
                return pawn;
            }
        }
        throw new ArgumentOutOfRangeException($"GetPawnById pawnId {pawnId} not found!");
    }
    
    static void UpdatePawnIsAlive(ref SGameState gameState, in Guid pawnId, bool inIsAlive)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                Vector2Int inPos = inIsAlive ? oldPawn.pos : Globals.PURGATORY;
                SPawn updatedPawn = new()
                {
                    pawnId = oldPawn.pawnId,
                    def = oldPawn.def,
                    player = oldPawn.player,
                    pos = inPos, // sets
                    isSetup = oldPawn.isSetup,
                    isAlive = inIsAlive, // sets
                    hasMoved = oldPawn.hasMoved,
                    isVisibleToOpponent = oldPawn.isVisibleToOpponent,
                };
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }

    static void UpdateRevealPawn(ref SGameState gameState, in Guid pawnId, in bool inIsVisibleToOpponent)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                SPawn updatedPawn = new()
                {
                    pawnId = oldPawn.pawnId,
                    def = oldPawn.def,
                    player = oldPawn.player,
                    pos = oldPawn.pos,
                    isSetup = oldPawn.isSetup,
                    isAlive = oldPawn.isAlive,
                    hasMoved = oldPawn.hasMoved,
                    isVisibleToOpponent = inIsVisibleToOpponent, // sets
                };
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }
    
    static void UpdatePawnPosition(ref SGameState gameState, in Guid pawnId, in Vector2Int inPos)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawnId)
            {
                SPawn oldPawn = gameState.pawns[i];
                SPawn updatedPawn = new()
                {
                    pawnId = oldPawn.pawnId,
                    def = oldPawn.def,
                    player = oldPawn.player,
                    pos = inPos, // sets
                    isSetup = oldPawn.isSetup,
                    isAlive = oldPawn.isAlive,
                    hasMoved = true, // sets true because moved
                    isVisibleToOpponent = oldPawn.isVisibleToOpponent,
                };
                gameState.pawns[i] = updatedPawn;
                break;
            }
        }
    }
    
    static void UpdatePawn(ref SGameState gameState, ref SPawn pawn)
    {
        for (int i = 0; i < gameState.pawns.Length; i++)
        {
            if (gameState.pawns[i].pawnId == pawn.pawnId)
            {
                gameState.pawns[i] = pawn;
                break;
            }
        }
    }
    
    static List<Vector2Int> GenerateMovementPath(in SGameState gameState, in Guid pawnId, in Vector2Int targetPos)
    {
        List<Vector2Int> path = new();
        SPawn pawn = gameState.GetPawnById(pawnId);
        Vector2Int currentPos = pawn.pos;

        if (pawn.def.Rank == Rank.SCOUT)
        {
            Vector2Int currentUnityPos = currentPos;
            Vector2Int targetUnityPos = targetPos;

            // Calculate the difference
            int deltaX = targetUnityPos.x - currentUnityPos.x;
            int deltaY = targetUnityPos.y - currentUnityPos.y;

            // Ensure movement is in a straight line (either horizontal or vertical)
            if (deltaX != 0 && deltaY != 0)
            {
                throw new Exception("Invalid move: Scout must move in a straight line (horizontal or vertical).");
            }

            // Determine the direction
            int stepX = deltaX != 0 ? Math.Sign(deltaX) : 0;
            int stepY = deltaY != 0 ? Math.Sign(deltaY) : 0;
            Vector2Int direction = new Vector2Int(stepX, stepY);

            // Calculate the number of steps
            int steps = Math.Abs(deltaX != 0 ? deltaX : deltaY);

            for (int i = 1; i <= steps; i++)
            {
                Vector2Int nextPosUnity = currentUnityPos + direction * i;
                Vector2Int nextPos = nextPosUnity;
                if (!gameState.boardDef.IsPosValid(nextPos))
                {
                    break;
                }
                path.Add(nextPos);
                // Check if the path is blocked
                SPawn? pawnOnPos = gameState.GetPawnByPos(nextPos);
                if (pawnOnPos.HasValue)
                {
                    if (pawnOnPos.Value.player == pawn.player)
                    {
                        // Cannot move through own pawns
                        path.RemoveAt(path.Count - 1); // Remove the blocked position
                        break;
                    }
                    else if (nextPos != targetPos)
                    {
                        // Enemy pawn is blocking the path before the target position
                        path.RemoveAt(path.Count - 1); // Remove the blocked position
                        break;
                    }
                    // If enemy pawn is at the target position, we can include it
                    // Conflict will be resolved during movement simulation
                }
                STile tile = gameState.GetTileByPos(nextPos);
                if (tile.isPassable)
                {
                    // Cannot move through impassable tiles
                    path.RemoveAt(path.Count - 1); // Remove the blocked position
                    break;
                }
            }
        }
        else
        {
            // Non-scouts move only one tile
            path.Add(targetPos);
        }

        return path;
    }

    public static SResolveReceipt Resolve(in SGameState gameState, in SQueuedMove redMove, in SQueuedMove blueMove)
    {
        if (gameState.player != (int)Player.NONE)
        {
            throw new ArgumentException("GameState.targetPlayer must be NONE as resolve can only happen on an uncensored board!");
        }
        

        // process movement
        SPawn redMovePawn = gameState.GetPawnById(redMove.pawnId);
        SPawn blueMovePawn = gameState.GetPawnById(blueMove.pawnId);

        SPawn? maybePawnOnRedMovePos = gameState.GetPawnByPos(redMove.pos);
        SPawn? maybePawnOnBlueMovePos = gameState.GetPawnByPos(blueMove.pos);
        
        bool redGotDodgedByBlue = false;
        bool blueGotDodgedByRed = false;
        if (maybePawnOnRedMovePos.HasValue && maybePawnOnRedMovePos.Value.pawnId == blueMovePawn.pawnId)
        {
            redGotDodgedByBlue = true;
        }
        if (maybePawnOnBlueMovePos.HasValue && maybePawnOnBlueMovePos.Value.pawnId == redMovePawn.pawnId)
        {
            blueGotDodgedByRed = true;
        }

        // if redGotDodgedByBlue and BlueGotDodgedByRed we just run redConflict and then redConflictAfterDeaths
        // and then redMoveAfterConflict or blueMoveAfterConflict depending on who survived
        
        // if blueGotDodgedByRed, this sequence should be done first
        SEventState? redConflict = null;
        HashSet<SEventState> redConflictAfterDeaths = new();
        SEventState? redMoveAfterConflict = null;
        // if redGotDodgedByBlue, this sequence should be done first
        SEventState? blueConflict = null;
        HashSet<SEventState> blueConflictAfterDeaths = new();
        SEventState? blueMoveAfterConflict = null;
        bool redMoveDeferred = false;
        bool blueMoveDeferred = false;
        // if red and blue swapped places
        if (redGotDodgedByBlue && blueGotDodgedByRed)
        {
            SConflictReceipt swapConflictResult = Rules.ResolveConflict(redMovePawn, blueMovePawn);
            SEventState blueAndRedSwappedConflictEvent = new()
            {
                player = (int)Player.NONE,
                eventType = (int)ResolveEvent.SWAPCONFLICT,
                pawnId = redMovePawn.pawnId,
                defenderPawnId = blueMovePawn.pawnId,
                originalPos = redMovePawn.pos, // arbitrary
                targetPos = blueMovePawn.pos, // arbitrary
            };
            redConflict = blueAndRedSwappedConflictEvent; // arbitrary for swap
            if (swapConflictResult.redDies)
            {
                SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                redConflictAfterDeaths.Add(redDies); // arbitrary for swap
                // if blue survived, move blue to reds position
                if (!swapConflictResult.blueDies)
                {
                    SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                    blueMoveAfterConflict = blueMovesToRedPos;
                }
            }
            if (swapConflictResult.blueDies)
            {
                SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                redConflictAfterDeaths.Add(blueDies); // arbitrary for swap
                // if red survived, move red to desired position
                if (!swapConflictResult.redDies)
                {
                    SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                    redMoveAfterConflict = redMovesToBluePos;
                }
            }
        }
        // if blue got dodged, red gets to move first
        else if (blueGotDodgedByRed)
        {
            blueMoveDeferred = true;
            // do red move
            if (maybePawnOnRedMovePos.HasValue)
            {
                SPawn blueDefender = maybePawnOnRedMovePos.Value;
                SConflictReceipt redAttackStationaryConflict = Rules.ResolveConflict(redMovePawn, blueDefender);
                SEventState redStartedConflict = new()
                {
                    player = redMovePawn.player,
                    eventType = (int)ResolveEvent.CONFLICT,
                    pawnId = redMovePawn.pawnId,
                    defenderPawnId = blueDefender.pawnId,
                    originalPos = redMovePawn.pos,
                    targetPos = blueDefender.pos,
                };
                redConflict = redStartedConflict;
                if (redAttackStationaryConflict.redDies)
                {
                    SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                    redConflictAfterDeaths.Add(redDies);
                }
                if (redAttackStationaryConflict.blueDies)
                {
                    SEventState blueDies = SEventState.CreateDeathEvent(blueDefender);
                    redConflictAfterDeaths.Add(blueDies);
                    if (!redAttackStationaryConflict.redDies)
                    {
                        SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                        redMoveAfterConflict = redMovesToBluePos;
                    }
                }
            }
            else
            {
                SEventState redPeacefullyMoves = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                redMoveAfterConflict = redPeacefullyMoves;
            }
            // move blue into reds former position
            SEventState bluePeacefullyMoves = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
            blueMoveAfterConflict = bluePeacefullyMoves;
        }
        // if red got dodged, blue gets to move first
        else if (redGotDodgedByBlue)
        {
            redMoveDeferred = true;
            // do blue move
            if (maybePawnOnBlueMovePos.HasValue)
            {
                SPawn redDefender = maybePawnOnBlueMovePos.Value;
                SConflictReceipt blueAttackStationaryConflict = Rules.ResolveConflict(redDefender, blueMovePawn);
                SEventState blueStartedConflict = new()
                {
                    player = blueMovePawn.player,
                    eventType = (int)ResolveEvent.CONFLICT,
                    pawnId = blueMovePawn.pawnId,
                    defenderPawnId = redDefender.pawnId,
                    originalPos = blueMovePawn.pos,
                    targetPos = blueMove.pos,
                };
                blueConflict = blueStartedConflict;
                if (blueAttackStationaryConflict.redDies)
                {
                    SEventState redDies = SEventState.CreateDeathEvent(redDefender);
                    blueConflictAfterDeaths.Add(redDies);
                    // if blue survived, move to attacker (blue) desired position
                    if (!blueAttackStationaryConflict.blueDies)
                    {
                        SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                        blueMoveAfterConflict = blueMovesToRedPos;
                    }
                }
                if (blueAttackStationaryConflict.blueDies)
                {
                    SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                    blueConflictAfterDeaths.Add(blueDies);
                    // we don't queue up a move because the attacker (blue) died
                }
            }
            else
            {
                SEventState bluePeacefullyMoves = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                blueMoveAfterConflict = bluePeacefullyMoves;
            }
            // move red into blues former position
            SEventState redPeacefullyMoves = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
            redMoveAfterConflict = redPeacefullyMoves;
        }
        // arbitrary order since two potential conflicts cant interfere with each other
        else
        {
            if (redMove.pos == blueMove.pos)
            {
                SConflictReceipt collisionConflictResult = Rules.ResolveConflict(redMovePawn, blueMovePawn);
                SEventState collisionConflict = new()
                {
                    player = redMovePawn.player,
                    eventType = (int)ResolveEvent.CONFLICT,
                    pawnId = redMovePawn.pawnId,
                    defenderPawnId = blueMovePawn.pawnId,
                    originalPos = redMovePawn.pos,
                    targetPos = redMove.pos,
                };
                redConflict = collisionConflict; // arbitrary
                if (collisionConflictResult.redDies)
                {
                    SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                    redConflictAfterDeaths.Add(redDies); // arbitrary for swap
                    // if blue survived, move blue to reds position
                    if (!collisionConflictResult.blueDies)
                    {
                        SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                        blueMoveAfterConflict = blueMovesToRedPos;
                    }
                }
                if (collisionConflictResult.blueDies)
                {
                    SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                    redConflictAfterDeaths.Add(blueDies); // arbitrary for swap
                    // if red survived, move red to desired position
                    if (!collisionConflictResult.redDies)
                    {
                        SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                        redMoveAfterConflict = redMovesToBluePos;
                    }
                }
            }
            else
            {
                // if red encounters a blue pawn
                if (maybePawnOnRedMovePos.HasValue)
                {
                    SPawn blueDefender = maybePawnOnRedMovePos.Value;
                    SConflictReceipt redAttackStationaryConflict = Rules.ResolveConflict(redMovePawn, blueDefender);
                    SEventState redStartedConflict = new()
                    {
                        player = redMovePawn.player,
                        eventType = (int)ResolveEvent.CONFLICT,
                        pawnId = redMovePawn.pawnId,
                        defenderPawnId = blueDefender.pawnId,
                        originalPos = redMovePawn.pos,
                        targetPos = blueDefender.pos,
                    };
                    redConflict = redStartedConflict;
                    if (redAttackStationaryConflict.redDies)
                    {
                        SEventState redDies = SEventState.CreateDeathEvent(redMovePawn);
                        redConflictAfterDeaths.Add(redDies);
                        // we don't queue up a move because the attacker (blue) died
                    }
                    if (redAttackStationaryConflict.blueDies)
                    {
                        SEventState blueDies = SEventState.CreateDeathEvent(blueDefender);
                        redConflictAfterDeaths.Add(blueDies);
                        if (!redAttackStationaryConflict.redDies)
                        {
                            SEventState redMovesToBluePos = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                            redMoveAfterConflict = redMovesToBluePos;
                        }
                    }
                }
                else
                {
                    SEventState redPeacefullyMoves = SEventState.CreateMoveEvent(redMovePawn, redMove.pos);
                    redMoveAfterConflict = redPeacefullyMoves;
                }
                // if blue encounters a red pawn
                if (maybePawnOnBlueMovePos.HasValue)
                {
                    SPawn redDefender = maybePawnOnBlueMovePos.Value;
                    SConflictReceipt blueAttackStationaryConflict = Rules.ResolveConflict(redDefender, blueMovePawn);
                    SEventState blueStartedConflict = new()
                    {
                        player = blueMovePawn.player,
                        eventType = (int)ResolveEvent.CONFLICT,
                        pawnId = blueMovePawn.pawnId,
                        defenderPawnId = redDefender.pawnId,
                        originalPos = blueMovePawn.pos,
                        targetPos = blueMove.pos,
                    };
                    blueConflict = blueStartedConflict;
                    if (blueAttackStationaryConflict.redDies)
                    {
                        SEventState redDies = SEventState.CreateDeathEvent(redDefender);
                        blueConflictAfterDeaths.Add(redDies);
                        // if blue survived, move to attacker (blue) desired position
                        if (!blueAttackStationaryConflict.blueDies)
                        {
                            SEventState blueMovesToRedPos = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                            blueMoveAfterConflict = blueMovesToRedPos;
                        }
                    }
                    if (blueAttackStationaryConflict.blueDies)
                    {
                        SEventState blueDies = SEventState.CreateDeathEvent(blueMovePawn);
                        blueConflictAfterDeaths.Add(blueDies);
                        // we don't queue up a move because the attacker (blue) died
                    }
                }
                else
                {
                    SEventState bluePeacefullyMoves = SEventState.CreateMoveEvent(blueMovePawn, blueMove.pos);
                    blueMoveAfterConflict = bluePeacefullyMoves;
                }
            }
        }
        
        SEventState[] receipts = new SEventState[6];
        SGameState nextGameState = new()
        {
            player = (int)Player.NONE,
            boardDef = gameState.boardDef,
            pawns = (SPawn[])gameState.pawns.Clone(),
        };
        
        // apply events to state
        
        if (redGotDodgedByBlue && blueGotDodgedByRed)
        {
            int eventIndex = 0;
            Debug.Assert(redConflict.HasValue); // swap conflicts are done on red conflict. swaps always cause conflict
            UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
            UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
            receipts[eventIndex] = redConflict.Value;
            eventIndex++;
            Debug.Assert(redConflict.Value.eventType == (int)ResolveEvent.SWAPCONFLICT);
            Debug.Assert(redConflictAfterDeaths.Count >= 1); // someone has to die
            // in frontend we run the battle routine, backend just applies the result
            foreach (SEventState deathEvent in redConflictAfterDeaths)
            {
                // in frontend we move pawn from deathEvent.originalPos to targetPos (purgatory) visually
                UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                receipts[eventIndex] = deathEvent;
                eventIndex++;
            }
            // in frontend we wait for event to end
            if (redMoveAfterConflict.HasValue)
            {
                // in frontend we move pawn from redMoveAfterConflict.originalPos to targetPos visually
                UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = redMoveAfterConflict.Value;
                eventIndex++;
            }
            // wait
            if (blueMoveAfterConflict.HasValue)
            {
                UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = blueMoveAfterConflict.Value;
                eventIndex++;
            }
            bool thisCantHappen = redMoveAfterConflict.HasValue && blueMoveAfterConflict.HasValue;
            Debug.Assert(!thisCantHappen);
        }
        else if (blueGotDodgedByRed)
        {
            int eventIndex = 0;
            Debug.Assert(blueMoveDeferred); // blue moves after
            Debug.Assert(!blueConflict.HasValue); // blue got cucked out of a conflict
            Debug.Assert(blueConflictAfterDeaths.Count == 0); // blue didnt kill anyone
            Debug.Assert(blueMoveAfterConflict.HasValue); // blue got to move into reds tile
            if (redConflict.HasValue)
            {
                UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
                UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
                receipts[eventIndex] = redConflict.Value;
                eventIndex++;
                Debug.Assert(redConflictAfterDeaths.Count >= 1); // someone has to die
                // battle routine
                foreach (SEventState deathEvent in redConflictAfterDeaths)
                {
                    UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                    receipts[eventIndex] = deathEvent;
                    eventIndex++;
                }
                // wait
                if (redMoveAfterConflict.HasValue)
                {
                    // in frontend we move pawn from redMoveAfterConflict.originalPos to targetPos visually
                    UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = redMoveAfterConflict.Value;
                    eventIndex++;
                }
            }
            else
            {
                // peacefully move red
                UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = redMoveAfterConflict.Value;
                eventIndex++;
            }
            // peacefully move blue into reds former position
            UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
            receipts[eventIndex] = blueMoveAfterConflict.Value;
            eventIndex++;
        }
        else if (redGotDodgedByBlue)
        {
            int eventIndex = 0;
            Debug.Assert(redMoveDeferred); // red moves after
            Debug.Assert(!redConflict.HasValue); // red got cucked out of a conflict
            Debug.Assert(redConflictAfterDeaths.Count == 0); // red didnt kill anyone
            Debug.Assert(redMoveAfterConflict.HasValue); // red got to move into blues tile
            if (blueConflict.HasValue)
            {
                UpdateRevealPawn(ref nextGameState, blueConflict.Value.pawnId, true);
                UpdateRevealPawn(ref nextGameState, blueConflict.Value.defenderPawnId, true);
                receipts[eventIndex] = blueConflict.Value;
                eventIndex++;
                Debug.Assert(blueConflictAfterDeaths.Count >= 1); // someone has to die
                // battle routine
                foreach (SEventState deathEvent in blueConflictAfterDeaths)
                {
                    UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                    receipts[eventIndex] = deathEvent;
                    eventIndex++;
                }
                // wait
                if (blueMoveAfterConflict.HasValue)
                {
                    // in frontend we move pawn from blueMoveAfterConflict.originalPos to targetPos visually
                    UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = blueMoveAfterConflict.Value;
                    eventIndex++;
                }
            }
            else
            {
                // peacefully move blue
                UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                receipts[eventIndex] = blueMoveAfterConflict.Value;
                eventIndex++;
            }
            // peacefully move red into blues former position
            UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
            receipts[eventIndex] = redMoveAfterConflict.Value;
            eventIndex++;
        }
        else
        {
            if (redMove.pos == blueMove.pos)
            {
                int eventIndex = 0;
                Debug.Assert(redConflict.HasValue);
                Debug.Assert(redConflictAfterDeaths.Count >= 1);
                UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
                UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
                receipts[eventIndex] = redConflict.Value;
                eventIndex++;
                // battle
                foreach (SEventState deathEvent in redConflictAfterDeaths)
                {
                    UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                    receipts[eventIndex] = deathEvent;
                    eventIndex++;
                }
                // wait
                if (redMoveAfterConflict.HasValue)
                {
                    Debug.Assert(blueMoveAfterConflict == null);
                    UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMove.pos);
                    receipts[eventIndex] = redMoveAfterConflict.Value;
                    eventIndex++;
                }
                if (blueMoveAfterConflict.HasValue)
                {
                    Debug.Assert(redMoveAfterConflict == null);
                    UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMove.pos);
                    receipts[eventIndex] = blueMoveAfterConflict.Value;
                    eventIndex++;
                }
                
            }
            else
            {
                int eventIndex = 0;
                // we cant guarantee any conflicts or moves will exist 
                if (redConflict.HasValue)
                {
                    Debug.Assert(redConflictAfterDeaths.Count >= 1); // someone has to die
                    UpdateRevealPawn(ref nextGameState, redConflict.Value.pawnId, true);
                    UpdateRevealPawn(ref nextGameState, redConflict.Value.defenderPawnId, true);
                    receipts[eventIndex] = redConflict.Value;
                    eventIndex++;
                    // battle routine
                    foreach (SEventState deathEvent in redConflictAfterDeaths)
                    {
                        UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                        receipts[eventIndex] = deathEvent;
                        eventIndex++;
                    }

                    // wait
                    if (redMoveAfterConflict.HasValue)
                    {
                        // in frontend we move pawn from redMoveAfterConflict.originalPos to targetPos visually
                        UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                        receipts[eventIndex] = redMoveAfterConflict.Value;
                        eventIndex++;
                    }
                }
                else
                {
                    // peacefully move red
                    Debug.Assert(redMoveAfterConflict.HasValue);
                    UpdatePawnPosition(ref nextGameState, redMoveAfterConflict.Value.pawnId, redMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = redMoveAfterConflict.Value;
                    eventIndex++;
                }

                if (blueConflict.HasValue)
                {
                    Debug.Assert(blueConflictAfterDeaths.Count >= 1); // someone has to die
                    UpdateRevealPawn(ref nextGameState, blueConflict.Value.pawnId, true);
                    UpdateRevealPawn(ref nextGameState, blueConflict.Value.defenderPawnId, true);
                    receipts[eventIndex] = blueConflict.Value;
                    eventIndex++;
                    // battle routine
                    foreach (SEventState deathEvent in blueConflictAfterDeaths)
                    {
                        UpdatePawnIsAlive(ref nextGameState, deathEvent.pawnId, false);
                        receipts[eventIndex] = deathEvent;
                        eventIndex++;
                    }

                    // wait
                    if (blueMoveAfterConflict.HasValue)
                    {
                        // in frontend we move pawn from blueMoveAfterConflict.originalPos to targetPos visually
                        UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                        receipts[eventIndex] = blueMoveAfterConflict.Value;
                        eventIndex++;
                    }
                }
                else
                {
                    // peacefully move blue
                    Debug.Assert(blueMoveAfterConflict.HasValue);
                    UpdatePawnPosition(ref nextGameState, blueMoveAfterConflict.Value.pawnId, blueMoveAfterConflict.Value.targetPos);
                    receipts[eventIndex] = blueMoveAfterConflict.Value;
                    eventIndex++;
                }
            }
        }

        if (!IsStateValid(nextGameState))
        {
            throw new Exception("Resolve nextGameState is not valid");
        }
        SEventState[] trimmedReceipts = receipts.Reverse().SkipWhile(x => x.pawnId == Guid.Empty).Reverse().ToArray();
        nextGameState.winnerPlayer = GetStateWinner(nextGameState);
        if (nextGameState.winnerPlayer != 0)
        {
            Debug.LogWarning("GAME HAS ENDED WINNER IS " + nextGameState.winnerPlayer);
        }
        SResolveReceipt finalReceipt = new()
        {
            player = (int)Player.NONE,
            gameState = nextGameState,
            events = trimmedReceipts,
        };
        return finalReceipt;
    }

    public static bool IsStateValid(in SGameState gameState)
    {
        bool pawnOverlapDetected = false;
        HashSet<SPawn> overlappingPawns = new();
        HashSet<SPawn> deadPawnsOnBoard = new();
        Dictionary<Vector2Int, SPawn> pawnPositions = new();
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.isAlive)
            {
                if (pawnPositions.TryGetValue(pawn.pos, out SPawn position))
                {
                    pawnOverlapDetected = true;
                    overlappingPawns.Add(position);
                    overlappingPawns.Add(pawn);
                }
                else
                {
                    pawnPositions.Add(pawn.pos, pawn);
                }
            }
            else
            {
                if (pawn.pos != Globals.PURGATORY)
                {
                    deadPawnsOnBoard.Add(pawn);
                }
            }
        }
        if (pawnOverlapDetected)
        {
            string error = overlappingPawns.Aggregate("", (current, pawn) => current + $"{pawn.pawnId} {pawn.def.pawnName} {pawn.pos.ToString()}");
            Debug.LogError($"IsStateValid Pawn overlap detected: {error}");
            return false;
        }

        if (deadPawnsOnBoard.Count < 0)
        {
            string error = deadPawnsOnBoard.Aggregate("", (current, pawn) => current + $"{pawn.pawnId} {pawn.def.pawnName} {pawn.pos.ToString()}");
            Debug.LogError($"IsStateValid Dead pawn on board detected: {error}");
            return false;
        }
        return true;
    }

    public static SSetupPawn[] GenerateValidSetup(int targetPlayer, in SSetupParameters setupParameters)
    {
        
        Dictionary<Rank, PawnDef> tempRankDictionary = GameManager.instance.GetPawnDefFromRank(); // NOTE: VERY BAD CODE WILL NOT WORK ON SERVER SIDE!!!!!!!
        if (targetPlayer == (int)Player.NONE)
        {
            throw new Exception("Player can't be none");
        }
        List<SSetupPawn> setupPawns = new();
        HashSet<STile> usedTiles = new();
        
        List<SMaxPawnsPerRank> sortedMaxPawns = new List<SMaxPawnsPerRank>(setupParameters.maxPawns);
        sortedMaxPawns.Sort((a, b) =>
        {
            if (a.rank == Rank.THRONE && b.rank != Rank.THRONE)
            {
                return -1; // a comes first
            }
            return Rules.GetSetupZone(b.rank).CompareTo(Rules.GetSetupZone(a.rank));
        });
        foreach (SMaxPawnsPerRank maxPawnsPerRank in sortedMaxPawns)
        {
            for (int i = 0; i < maxPawnsPerRank.max; i++)
            {
                List<STile> eligibleTiles = setupParameters.board.GetEligibleTilesForPawnSetup(targetPlayer, maxPawnsPerRank.rank, usedTiles);
                if (eligibleTiles.Count == 0)
                {
                    Debug.LogError("NO ELIGIBLE TILES STOPPING SETUP HERE");
                    return setupPawns.ToArray();
                }
                int index = UnityEngine.Random.Range(0, eligibleTiles.Count);
                STile randomTile = eligibleTiles[index];
                usedTiles.Add(randomTile);
                // NOTE: bad
                SPawnDef sPawnDef = new SPawnDef(tempRankDictionary[maxPawnsPerRank.rank]);
                SSetupPawn sSetupPawn = new()
                {
                    player = targetPlayer,
                    def = sPawnDef,
                    pos = randomTile.pos,
                    deployed = true,
                };
                setupPawns.Add(sSetupPawn);
            }
        }
        return setupPawns.ToArray();
    }

    public static int GetStateWinner(in SGameState gameState)
    {
        // Define constants for return values
        const int NO_WINNER = 0;
        const int RED_WIN = 1;
        const int BLUE_WIN = 2;
        const int TIE = 4;

        SPawn redFlag = gameState.pawns.FirstOrDefault(p => p.player == (int)Player.RED && p.def.Rank == Rank.THRONE);
        SPawn blueFlag = gameState.pawns.FirstOrDefault(p => p.player == (int)Player.BLUE && p.def.Rank == Rank.THRONE);
        bool redCanMove = false;
        bool blueCanMove = false;
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.isAlive)
            {
                var movableTiles = gameState.GetMovableTiles(pawn);
                if (movableTiles.Length > 0)
                {
                    if (pawn.player == (int)Player.BLUE)
                    {
                        blueCanMove = true;
                    }
                    if (pawn.player == (int)Player.RED)
                    {
                        redCanMove = true;
                    }
                }
            }
        }
        // Determine win conditions
        bool redWinCondition = !blueFlag.isAlive || !blueCanMove;
        bool blueWinCondition = !redFlag.isAlive || !redCanMove;
        if (!blueCanMove)
        {
            Debug.Log("Blue cant move so win condition set");
        }
        if (!redCanMove)
        {
            Debug.Log("Red cant move so win condition set");
        }
        // Determine the winner based on conditions
        if (redWinCondition && blueWinCondition)
        {
            return TIE;
        }
        else if (redWinCondition)
        {
            return RED_WIN;
        }
        else if (blueWinCondition)
        {
            return BLUE_WIN;
        }
        else
        {
            return NO_WINNER;
        }
    }
}

public struct PawnChanges
{
    public Pawn pawn;
    public bool posChanged;
    public bool isSetupChanged;
    public bool isAliveChanged;
    public bool hasMovedChanged;
    public bool isVisibleToOpponentChanged;

    public bool IsChanged()
    {
        if (posChanged)
        {
            return true;
        }
        if (isSetupChanged)
        {
            return true;
        }
        if (isAliveChanged)
        {
            return true;
        }
        if (hasMovedChanged)
        {
            return true;
        }
        if (isVisibleToOpponentChanged)
        {
            return true;
        }
        return false;
    }
    
    public override string ToString()
    {
        return $"{pawn.pawnId} {pawn.def.pawnName} posChanged: {posChanged} isSetupChanged: {isSetupChanged} isAliveChanged: {isAliveChanged} hasMovedChanged: {hasMovedChanged} isVisibleToOpponentChanged: {isVisibleToOpponentChanged}";
    }
}

public struct SConflictReceipt
{
    public Guid redPawnId;
    public Guid bluePawnId;
    public bool redDies;
    public bool blueDies;

    public override string ToString()
    {
        return $"red id: {redPawnId} blue id: {bluePawnId} redDies: {redDies} blueDies: {blueDies}";
    }
}

public struct SResolveReceipt
{
    public int player;
    public SGameState gameState;
    public SEventState[] events;
}

public struct SEventState
{
    public int player;
    public int eventType;
    public Guid pawnId;
    public Guid defenderPawnId;
    public Vector2Int originalPos;
    public Vector2Int targetPos;

    public static SEventState CreateDeathEvent(SPawn inPawn)
    {
        SEventState deathEvent = new()
        {
            player = inPawn.player,
            eventType = (int)ResolveEvent.DEATH,
            pawnId = inPawn.pawnId,
            defenderPawnId = Guid.Empty,
            originalPos = inPawn.pos,
            targetPos = Globals.PURGATORY,
        };
        return deathEvent;
    }
    
    public static SEventState CreateMoveEvent(SPawn inPawn, Vector2Int inTargetPos)
    {
        if (inPawn.pawnId == Guid.Empty)
        {
            throw new Exception("inPawn pawnId cant be empty");
        }
        SEventState moveEvent = new()
        {
            player = inPawn.player,
            eventType = (int)ResolveEvent.MOVE,
            pawnId = inPawn.pawnId,
            defenderPawnId = Guid.Empty,
            originalPos = inPawn.pos,
            targetPos = inTargetPos,
        };
        return moveEvent;
    }
    
    public override string ToString()
    {
        string baseString = $"{(ResolveEvent)eventType} {(Player)player} {Globals.ShortGuid(pawnId)} ";
        switch ((ResolveEvent)eventType)
        {
            case ResolveEvent.MOVE:
                return baseString + $"moved {originalPos} to {targetPos}";
            case ResolveEvent.CONFLICT:
                return baseString + $"vs {Globals.ShortGuid(defenderPawnId)}";
            case ResolveEvent.SWAPCONFLICT:
                return baseString + $"vs {Globals.ShortGuid(defenderPawnId)}";
            case ResolveEvent.DEATH:
                return baseString + $"died";
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum ResolveEvent
{
    MOVE,
    CONFLICT,
    SWAPCONFLICT,
    DEATH,
}

public struct SSetupPawn
{
    public int player;
    public SPawnDef def;
    public Vector2Int pos;
    public bool deployed;
    
    public SSetupPawn(Pawn pawn)
    {
        player = (int)pawn.player;
        def = new SPawnDef(pawn.def);
        pos = pawn.pos;
        deployed = pawn.isAlive;
    }
}