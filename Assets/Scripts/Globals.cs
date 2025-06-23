using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Contract;
using Newtonsoft.Json;
using Stellar;
using Stellar.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

public static class Globals
{
    public static readonly Vector2Int Purgatory = new(-666, -666);
    public const float PawnMoveDuration = 1f;
    public const float HoveredHeight = 0.1f;
    public const float SelectedHoveredHeight = 0.1f;
    public static readonly InputSystem_Actions InputActions = new();

    public static ulong RandomSalt()
    {
        ulong value;
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[8];
        do
        {
            rng.GetBytes(bytes);
            value = BitConverter.ToUInt64(bytes, 0);
        } 
        while (value == 0); // retry if zero
        return value;
    }
    
    public static uint GeneratePawnId(Vector2Int startingPos, Team team)
    {
        if (team == Team.NONE)
        {
            throw new ArgumentException();
        }
        bool isRedTeam = team == Team.RED;
        uint baseId = (uint)startingPos.x * 101 + (uint)startingPos.y;
        return (baseId << 1) | (isRedTeam ? 0u : 1u);
    }

    public static (Vector2Int, Team) DecodeStartingPosAndTeamFromPawnId(uint pawnId)
    {
        bool isRed = (pawnId & 1) == 0;
        uint baseId = pawnId >> 1;
        int x = (int)(baseId / 101);
        int y = (int)(baseId % 101);
        return (new Vector2Int(x, y), isRed ? Team.RED : Team.BLUE);
    }
    
    public static bool AddressIsEmpty(SCVal.ScvAddress address)
    {
        return AddressToString(address) == "GAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAWHF";
    }
    
    public static string AddressToString(SCVal.ScvAddress address)
    {
        switch (address.address)
        {
            case SCAddress.ScAddressTypeAccount scAddressTypeAccount:
                var accountKey = (PublicKey.PublicKeyTypeEd25519)scAddressTypeAccount.accountId.InnerValue;
                return StrKey.EncodeStellarAccountId(accountKey.ed25519);
            case SCAddress.ScAddressTypeContract scAddressTypeContract:
                var contractKey = (Hash)scAddressTypeContract.contractId.InnerValue;
                return StrKey.EncodeContractId(contractKey);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static SCVal.ScvAddress StringToAddress(StrKey.VersionByte version, string addressString)
    {
        byte[] bytes = StrKey.DecodeCheck(version, addressString);
        switch (version)
        {
            case StrKey.VersionByte.ACCOUNT_ID:
                var pk = new PublicKey.PublicKeyTypeEd25519() { ed25519 = bytes };
                var accountId = new AccountID(pk);
                var scAddress = new SCAddress.ScAddressTypeAccount() {accountId = accountId};
                return new SCVal.ScvAddress() { address = scAddress };
            case StrKey.VersionByte.MUXED_ACCOUNT:
                throw new NotImplementedException();
            case StrKey.VersionByte.SEED:
                throw new NotImplementedException();
            case StrKey.VersionByte.PRE_AUTH_TX:
                throw new NotImplementedException();
            case StrKey.VersionByte.SHA256_HASH:
                throw new NotImplementedException();
            case StrKey.VersionByte.SIGNED_PAYLOAD:
                throw new NotImplementedException();
            case StrKey.VersionByte.CONTRACT:
                var cAddress = new SCAddress.ScAddressTypeContract() { contractId = bytes };
                return new SCVal.ScvAddress() { address = cAddress };
            default:
                throw new ArgumentOutOfRangeException(nameof(version), version, null);
        }
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

    public static string PawnDefToFakeHash(PawnDef pawnDef)
    {
        return pawnDef.pawnName;
    }

    public static PawnDef RankToPawnDef(Rank rank)
    {
        foreach (PawnDef def in GameManager.instance.orderedPawnDefList.Where(def => def.rank == rank))
        {
            return def;
        }
        throw new KeyNotFoundException($"Could not find pawnDef {rank}");
    }

    // public static MoveResolveReq ResolveTurn(Lobby lobby, string address)
    // {
    //     // // Get the latest turn
    //     // Turn turn = lobby.GetLatestTurn();
    //     //
    //     // // Check if both moves are initialized
    //     // if (!turn.host_turn.initialized || !turn.guest_turn.initialized)
    //     // {
    //     //     throw new ArgumentException("Both turns must be initialized");
    //     // }
    //     //
    //     // // Convert TurnMoves to SQueuedMoves
    //     // SQueuedMove redMove;
    //     // SQueuedMove blueMove;
    //     //
    //     // // Determine which turn is red and which is blue based on team assignments
    //     // if (lobby.host_state.team == (uint)Team.RED)
    //     // {
    //     //     redMove = new SQueuedMove(
    //     //         (int)Team.RED,
    //     //         Guid.Parse(turn.host_turn.pawn_id),
    //     //         turn.host_turn.pos.ToVector2Int(),
    //     //         turn.host_turn.pos.ToVector2Int()
    //     //     );
    //     //     blueMove = new SQueuedMove(
    //     //         (int)Team.BLUE,
    //     //         Guid.Parse(turn.guest_turn.pawn_id),
    //     //         turn.guest_turn.pos.ToVector2Int(),
    //     //         turn.guest_turn.pos.ToVector2Int()
    //     //     );
    //     // }
    //     // else
    //     // {
    //     //     redMove = new SQueuedMove(
    //     //         (int)Team.RED,
    //     //         Guid.Parse(turn.guest_turn.pawn_id),
    //     //         turn.guest_turn.pos.ToVector2Int(),
    //     //         turn.guest_turn.pos.ToVector2Int()
    //     //     );
    //     //     blueMove = new SQueuedMove(
    //     //         (int)Team.BLUE,
    //     //         Guid.Parse(turn.host_turn.pawn_id),
    //     //         turn.host_turn.pos.ToVector2Int(),
    //     //         turn.host_turn.pos.ToVector2Int()
    //     //     );
    //     // }
    //     //
    //     // // Load board definition
    //     // BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
    //     // BoardDef boardDef = boardDefs.FirstOrDefault(def => def.name == lobby.parameters.board_def_name);
    //     // if (boardDef == null)
    //     // {
    //     //     throw new ArgumentException($"Could not find board definition {lobby.parameters.board_def_name}");
    //     // }
    //     //
    //     // // Create game state from lobby
    //     // SGameState gameState = new()
    //     // {
    //     //     team = (int)Team.NONE, // Resolution must happen with uncensored state
    //     //     boardDef = new SBoardDef(boardDef),
    //     //     pawns = lobby.pawns.Select(p => new SPawn
    //     //     {
    //     //         pawnId = Guid.Parse(p.pawn_id),
    //     //         def = new SPawnDef(FakeHashToPawnDef(p.pawn_def_hash)),
    //     //         team = (int)p.team,
    //     //         pos = p.pos.ToVector2Int(),
    //     //         isSetup = false, // Deprecated
    //     //         isAlive = p.is_alive,
    //     //         hasMoved = p.is_moved,
    //     //         isVisibleToOpponent = p.is_revealed
    //     //     }).ToArray()
    //     // };
    //     //
    //     // // Call old resolution function
    //     // SResolveReceipt receipt = SGameState.Resolve(gameState, redMove, blueMove);
    //     // if (address == FakeServer.ins.fakeHost.index)
    //     // {
    //     //     Debug.Log($"XXX Turn: {turn.turn} printing original events -----");
    //     //     foreach (SEventState thing in receipt.events)
    //     //     {
    //     //         Debug.Log("XXX " + thing);
    //     //     }
    //     //     Debug.Log($"XXX Turn: {turn.turn} end -----");
    //     // }
    //     //
    //     // // Convert SEventState[] to Contract.ResolveEvent[]
    //     // Contract.ResolveEvent[] events = new Contract.ResolveEvent[receipt.events.Length];
    //     // for (int i = 0; i < receipt.events.Length; i++)
    //     // {
    //     //     SEventState evt = receipt.events[i];
    //     //     string pawnId = evt.pawnId != Guid.Empty ? evt.pawnId.ToString() : "";
    //     //     string defenderPawnId = evt.defenderPawnId != Guid.Empty ? evt.defenderPawnId.ToString() : "";
    //     //     events[i] = new Contract.ResolveEvent
    //     //     {
    //     //         team = (uint)evt.team,
    //     //         event_type = (uint)evt.eventType,
    //     //         pawn_id = pawnId,
    //     //         defender_pawn_id = defenderPawnId,
    //     //         original_pos = new Pos(evt.originalPos),
    //     //         target_pos = new Pos(evt.targetPos),
    //     //     };
    //     // }
    //     //
    //     // // Create and return MoveResolveReq
    //     // return new MoveResolveReq
    //     // {
    //     //     events = events,
    //     //     events_hash = HashEvents(events), // TODO: Implement this
    //     //     lobby = lobby.index,
    //     //     turn = turn.turn,
    //     //     user_address = address,
    //     // };
    //     return new MoveResolveReq
    //     {
    //         
    //     };
    // }

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
    public SLobbyParameters lobbyParameters;
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

// public class LobbyParameters
// {
//     public int hostTeam;
//     public int guestTeam;
//     public BoardDef board;
//     public SMaxPawnsPerRank[] maxPawns;
//     public bool mustFillAllTiles;
// }
public struct SLobbyParameters
{
    public int hostTeam;
    public int guestTeam;
    public SBoardDef board;
    public SMaxPawnsPerRank[] maxPawns;
    public bool mustFillAllTiles;

    // public SLobbyParameters(LobbyParameters lobbyParameters)
    // {
    //     hostTeam = lobbyParameters.hostTeam;
    //     guestTeam = lobbyParameters.guestTeam;
    //     board = new SBoardDef(lobbyParameters.board);
    //     maxPawns = lobbyParameters.maxPawns;
    //     mustFillAllTiles = lobbyParameters.mustFillAllTiles;
    // }
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
    event Action<Response<SLobbyParameters>> OnDemoStartedResponse;
    event Action<Response<bool>> OnSetupSubmittedResponse;
    event Action<Response<SGameState>> OnSetupFinishedResponse;
    event Action<Response<bool>> OnMoveResponse;
    event Action<Response<SResolveReceipt>> OnResolveResponse;
    
    
    // Methods
    void ConnectToServer();
    void SendRegisterNickname(string nicknameInput);
    void SendGameLobby(SLobbyParameters lobbyParameters);
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
    public SLobbyParameters lobbyParameters { get; set; }
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
    
}

public class SetupRequest : RequestBase
{
    // TODO: refactor this to be more coherent and not just an array
    public SSetupPawn[] setupPawns;
}

public class MoveRequest : RequestBase
{
    public SQueuedMove move;
}


[Serializable]
public struct SQueuedMove
{
    public int team;
    public Guid pawnId;
    public Vector2Int initialPos;
    public Vector2Int pos;

    public SQueuedMove(in SPawn pawn, in Vector2Int inPos)
    {
        team = pawn.team;
        pawnId = pawn.pawnId;
        initialPos = pawn.pos;
        pos = inPos;
    }

    public SQueuedMove(Pawn pawn, Vector2Int inPos)
    {
        team = (int)pawn.team;
        pawnId = pawn.pawnId;
        initialPos = pawn.pos;
        pos = inPos;
    }
    
    public SQueuedMove(int inTeam, Guid inPawnId, Vector2Int inInitialPos, Vector2Int inPos)
    {
        team = inTeam;
        pawnId = inPawnId;
        initialPos = inInitialPos;
        pos = inPos;
    }
}

[Serializable]
public struct SGameState
{
    public int winnerTeam;
    public int team;
    public SBoardDef boardDef;
    public SPawn[] pawns;
    
    public SGameState(int inTeam, SBoardDef inBoardDef, SPawn[] inPawns)
    {
        winnerTeam = (int)Team.NONE;
        team = inTeam;
        boardDef = inBoardDef;
        pawns = inPawns;
    }
    
    public readonly STile[] GetMovableTiles(in SPawn pawn)
    {
        if (!pawn.isAlive)
        {
            return Array.Empty<STile>();
        }
        if (pawn.def.movementRange == 0)
        {
            return Array.Empty<STile>();
        }
        Vector2Int[] initialDirections = Shared.GetDirections(pawn.pos, boardDef.isHex);
        STile[] movableTiles = new STile[boardDef.tiles.Length]; // allocate big ass array
        int tileCount = 0;
        for (int dirIndex = 0; dirIndex < initialDirections.Length; dirIndex++)
        {
            Vector2Int currentPos = pawn.pos;
            int walkedTiles = 0;
            while (walkedTiles < pawn.def.movementRange)
            {
                // directions change depending on odd or even col in hexagons so we have to get it again
                Vector2Int[] currentDirections = Shared.GetDirections(currentPos, boardDef.isHex);
                // peek one tile in this direction ahead
                currentPos += currentDirections[dirIndex];
                if (!boardDef.IsPosValid(currentPos))
                {
                    break;
                }
                STile tile = boardDef.GetTileByPos(currentPos);
                if (!tile.isPassable)
                {
                    break;
                }
                SPawn? pawnOnPos = GetPawnByPos(currentPos);
                if (pawnOnPos.HasValue)
                {
                    if (pawnOnPos.Value.team == pawn.team)
                    {
                        // Cannot move through own pawns
                        break;
                    }
                    // Tile is occupied by an enemy pawn
                    movableTiles[tileCount++] = tile;
                    break;
                }
                // Tile is unoccupied
                movableTiles[tileCount++] = tile;
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
            STile destinationTile = gameState.boardDef.GetTileByPos(move.pos);
            if (!destinationTile.isPassable)
            {
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
            if (move.team != movingPawn.team)
            {
                return false;
            }
            SPawn? maybePawn = gameState.GetPawnByPos(move.pos);
            if (maybePawn.HasValue)
            {
                SPawn obstructingPawn = maybePawn.Value;
                if (obstructingPawn.team == move.team)
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
    
    public static SGameState Censor(in SGameState masterGameState, int team)
    {
        if (masterGameState.team != (int)Team.NONE)
        {
            throw new Exception("Censor can only be done on master game states!");
        }
        SPawn[] censoredPawns = new SPawn[masterGameState.pawns.Length];
        for (int i = 0; i < masterGameState.pawns.Length; i++)
        {
            SPawn serverPawn = masterGameState.pawns[i];
            SPawn censoredPawn;
            if (serverPawn.team != team)
            {
                if (PlayerPrefs.GetInt("CHEATMODE") == 1)
                {
                    censoredPawn = serverPawn;
                    censoredPawn.isVisibleToOpponent = true;
                }
                else
                {
                    censoredPawn = serverPawn.isVisibleToOpponent ? serverPawn : serverPawn.Censor();
                }
            }
            else
            {
                censoredPawn = serverPawn;
            }
            censoredPawns[i] = censoredPawn;
        }
        SGameState censoredGameState = new()
        {
            winnerTeam = masterGameState.winnerTeam,
            team = team,
            boardDef = masterGameState.boardDef,
            pawns = censoredPawns,
        };
        return censoredGameState;
    }
    
    public static SQueuedMove GenerateValidMove(in SGameState gameState, int team)
    {
        // NOTE: gameState should be censored if it isn't already for fairness
        List<SQueuedMove> allPossibleMoves = new();
        foreach (SPawn pawn in gameState.pawns)
        {
            if (pawn.team != team) continue;
            STile[] movableTiles = gameState.GetMovableTiles(pawn);
            allPossibleMoves.AddRange(movableTiles.Select(tile => new SQueuedMove(team, pawn.pawnId, pawn.pos, tile.pos)));
        }
        if (allPossibleMoves.Count == 0)
        {
            throw new Exception("GenerateValidMove() no valid moves");
        }
        System.Random random = new();
        int randomIndex = random.Next(0, allPossibleMoves.Count);
        SQueuedMove randomMove = allPossibleMoves[randomIndex];
        SPawn randomPawn = gameState.GetPawnById(randomMove.pawnId);
        Debug.Log($"GenerateValidMove() chose {randomMove.pawnId} {randomPawn.def.pawnName} from {randomPawn.pos} to {randomMove.pos}");
        if (!IsMoveValid(gameState, randomMove))
        {
            throw new Exception("GenerateValidMove() chose invalid move");
        }
        return randomMove;
    }
    
    public readonly SPawn? GetPawnByPos(Vector2Int pos)
    {
        SPawn maybePawn = Array.Find(pawns, pawn => pawn.pos == pos);
        if (maybePawn.pawnId == Guid.Empty)
        {
            return null;
        }
        return maybePawn;
    }

    public readonly SPawn GetPawnById(Guid pawnId)
    {
        SPawn maybePawn = Array.Find(pawns, pawn => pawn.pawnId == pawnId);
        if (maybePawn.pawnId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException($"GetPawnById() could not find {pawnId}");
        }
        return maybePawn;
    }

    public (int, SPawn) GetPawnIndexById(Guid pawnId)
    {
        int index = Array.FindIndex(pawns, pawn => pawn.pawnId == pawnId);
        if (index == -1)
        {
            throw new ArgumentOutOfRangeException($"GetPawnIndexById() could not find {pawnId}");
        }
        SPawn pawn = pawns[index];
        return (index, pawn);
    }
    
    static void UpdatePawnIsAlive(ref SGameState gameState, in Guid pawnId, bool inIsAlive)
    {
        (int index, SPawn oldPawn) = gameState.GetPawnIndexById(pawnId);
        Vector2Int inPos = inIsAlive ? oldPawn.pos : Globals.Purgatory;
        SPawn updatedPawn = new()
        {
            pawnId = oldPawn.pawnId,
            def = oldPawn.def,
            team = oldPawn.team,
            pos = inPos, // sets
            isSetup = oldPawn.isSetup,
            isAlive = inIsAlive, // sets
            hasMoved = oldPawn.hasMoved,
            isVisibleToOpponent = oldPawn.isVisibleToOpponent,
        };
        gameState.pawns[index] = updatedPawn;
    }

    static void UpdateRevealPawn(ref SGameState gameState, in Guid pawnId, in bool inIsVisibleToOpponent)
    {
        (int index, SPawn oldPawn) = gameState.GetPawnIndexById(pawnId);
        SPawn updatedPawn = new()
        {
            pawnId = oldPawn.pawnId,
            def = oldPawn.def,
            team = oldPawn.team,
            pos = oldPawn.pos,
            isSetup = oldPawn.isSetup,
            isAlive = oldPawn.isAlive,
            hasMoved = oldPawn.hasMoved,
            isVisibleToOpponent = inIsVisibleToOpponent, // sets
        };
        gameState.pawns[index] = updatedPawn;
    }
    
    static void UpdatePawnPosition(ref SGameState gameState, in Guid pawnId, in Vector2Int inPos)
    {
        (int index, SPawn oldPawn) = gameState.GetPawnIndexById(pawnId);
        SPawn updatedPawn = new()
        {
            pawnId = oldPawn.pawnId,
            def = oldPawn.def,
            team = oldPawn.team,
            pos = inPos, // sets
            isSetup = oldPawn.isSetup,
            isAlive = oldPawn.isAlive,
            hasMoved = true, // sets true because moved
            isVisibleToOpponent = oldPawn.isVisibleToOpponent,
        };
        gameState.pawns[index] = updatedPawn;
    }
    
    public static SResolveReceipt Resolve(in SGameState gameState, in SQueuedMove redMove, in SQueuedMove blueMove)
    {
        if (gameState.team != (int)Team.NONE)
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
                team = (int)Team.NONE,
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
                    team = redMovePawn.team,
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
                    team = blueMovePawn.team,
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
                    team = redMovePawn.team,
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
                        team = redMovePawn.team,
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
                        team = blueMovePawn.team,
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
            team = (int)Team.NONE,
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
        nextGameState.winnerTeam = GetStateWinner(nextGameState);
        if (nextGameState.winnerTeam != 0)
        {
            Debug.LogWarning("GAME HAS ENDED WINNER IS " + nextGameState.winnerTeam);
        }
        SResolveReceipt finalReceipt = new()
        {
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
                if (pawn.pos != Globals.Purgatory)
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

    public static SSetupPawn[] GenerateValidSetup(int team, in SLobbyParameters lobbyParameters)
    {
        
        Dictionary<Rank, PawnDef> tempRankDictionary = GameManager.instance.GetPawnDefFromRank(); // NOTE: VERY BAD CODE WILL NOT WORK ON SERVER SIDE!!!!!!!
        if (team == (int)Team.NONE)
        {
            throw new Exception("Team can't be none");
        }
        List<SSetupPawn> setupPawns = new();
        HashSet<STile> usedTiles = new();
        
        List<SMaxPawnsPerRank> sortedMaxPawns = new List<SMaxPawnsPerRank>(lobbyParameters.maxPawns);
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
                List<STile> eligibleTiles = lobbyParameters.board.GetEmptySetupTiles(team, maxPawnsPerRank.rank, usedTiles);
                if (eligibleTiles.Count == 0)
                {
                    Debug.LogError("NO ELIGIBLE TILES STOPPING SETUP HERE");
                    return setupPawns.ToArray();
                }
                int index = UnityEngine.Random.Range(0, eligibleTiles.Count);
                STile randomTile = eligibleTiles[index];
                usedTiles.Add(randomTile);
                // NOTE: bad
                SPawnDef sPawnDef = new(tempRankDictionary[maxPawnsPerRank.rank]);
                SSetupPawn sSetupPawn = new()
                {
                    team = team,
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
        const int noWinner = 0;
        const int redWin = 1;
        const int blueWin = 2;
        const int tie = 4;

        SPawn redFlag = gameState.pawns.FirstOrDefault(p => p.team == (int)Team.RED && p.def.Rank == Rank.THRONE);
        SPawn blueFlag = gameState.pawns.FirstOrDefault(p => p.team == (int)Team.BLUE && p.def.Rank == Rank.THRONE);
        bool redCanMove = false;
        bool blueCanMove = false;
        foreach (SPawn pawn in gameState.pawns)
        {
            STile[] movableTiles = gameState.GetMovableTiles(pawn);
            if (movableTiles.Length <= 0) continue;
            switch (pawn.team)
            {
                case (int)Team.BLUE:
                    blueCanMove = true;
                    break;
                case (int)Team.RED:
                    redCanMove = true;
                    break;
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
            return tie;
        }
        else if (redWinCondition)
        {
            return redWin;
        }
        else if (blueWinCondition)
        {
            return blueWin;
        }
        else
        {
            return noWinner;
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
    public SGameState gameState;
    public SEventState[] events;
}

public struct SEventState
{
    public int team;
    public int eventType;
    public Guid pawnId;
    public Guid defenderPawnId;
    public Vector2Int originalPos;
    public Vector2Int targetPos;

    public static SEventState CreateDeathEvent(SPawn inPawn)
    {
        SEventState deathEvent = new()
        {
            team = inPawn.team,
            eventType = (int)ResolveEvent.DEATH,
            pawnId = inPawn.pawnId,
            defenderPawnId = Guid.Empty,
            originalPos = inPawn.pos,
            targetPos = Globals.Purgatory,
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
            team = inPawn.team,
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
        string baseString = $"{(ResolveEvent)eventType} {(Team)team} {Shared.ShortGuid(pawnId)} ";
        switch ((ResolveEvent)eventType)
        {
            case ResolveEvent.MOVE:
                return baseString + $"moved {originalPos} to {targetPos}";
            case ResolveEvent.CONFLICT:
                return baseString + $"vs {Shared.ShortGuid(defenderPawnId)}";
            case ResolveEvent.SWAPCONFLICT:
                return baseString + $"vs {Shared.ShortGuid(defenderPawnId)}";
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
    public int team;
    public SPawnDef def;
    public Vector2Int pos;
    public bool deployed;
    
    public SSetupPawn(Pawn pawn)
    {
        team = (int)pawn.team;
        def = new SPawnDef(pawn.def);
        pos = pawn.pos;
        deployed = pawn.isAlive;
    }
}

public readonly struct AccountAddress : IEquatable<AccountAddress>
{
    private readonly string ed25519PublicKey;
    
    public AccountAddress(string accountId)
    {
        if (accountId == null)
        {
            throw new ArgumentNullException(nameof(accountId));
        }

        if (!StrKey.IsValidEd25519PublicKey(accountId))
        {
            throw new ArgumentException($"Invalid account id: {accountId}");
        }
        ed25519PublicKey = accountId;
    }
    
    public AccountAddress(byte[] rawBytes)
    {
        if (rawBytes is null)
            throw new ArgumentNullException(nameof(rawBytes));
        if (rawBytes.Length != 44)
            throw new ArgumentException("StellarAccountId rawBytes must be exactly 44 bytes.", nameof(rawBytes));
        // copy to guard against external mutation
        
        ed25519PublicKey = StrKey.EncodeStellarAccountId(rawBytes);
    }

    public AccountAddress(SCVal.ScvAddress scvAddress)
    {
        if (scvAddress.address is SCAddress.ScAddressTypeAccount account)
        {
            if (account.accountId.InnerValue is PublicKey.PublicKeyTypeEd25519 ed25519)
            {
                ed25519PublicKey = StrKey.EncodeStellarAccountId(ed25519.ed25519);
            }
            else { throw new ArgumentException($"Invalid account id: {account.accountId}"); }
        }
        else { throw new ArgumentException($"Invalid account id: {scvAddress.address}"); }
    }

    public SCVal.ScvAddress ToScvAddress()
    {
        return new SCVal.ScvAddress()
        {
            address = new SCAddress.ScAddressTypeAccount()
            {
                accountId = new AccountID( new PublicKey.PublicKeyTypeEd25519()
                {
                    ed25519 = StrKey.DecodeStellarAccountId(ed25519PublicKey),
                }),
            },
        };
    }
    
    /// <summary>
    /// Non-throwing parse.
    /// </summary>
    public static bool TryParse(string s, out AccountAddress result)
    {
        if (StrKey.IsValidEd25519PublicKey(s))
        {
            result = new AccountAddress(s);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Throws if invalid.
    /// </summary>
    public static AccountAddress Parse(string s)
        => new AccountAddress(s);

    /// <summary>
    /// Get the raw 32-byte public key.
    /// </summary>
    public byte[] ToBytes()
        => StrKey.DecodeStellarAccountId(ed25519PublicKey);

    public override string ToString()
        => ed25519PublicKey;

    #region Equality members
    public bool Equals(AccountAddress other)
        => string.Equals(ed25519PublicKey, other.ed25519PublicKey, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is AccountAddress other && Equals(other);

    public override int GetHashCode()
        => ed25519PublicKey?.GetHashCode() ?? 0;

    public static bool operator ==(AccountAddress left, AccountAddress right)
        => left.Equals(right);

    public static bool operator !=(AccountAddress left, AccountAddress right)
        => !(left == right);
    #endregion

    /// <summary>
    /// Allow: StellarAccount acct = "G…";
    /// </summary>
    public static implicit operator AccountAddress(string accountId)
        => new AccountAddress(accountId);

    /// <summary>
    /// Allow: string s = acct;
    /// </summary>
    public static implicit operator string(AccountAddress acct)
        => acct.ed25519PublicKey;
}

public readonly struct LobbyId : IEquatable<LobbyId>
{
    private readonly uint val;
    public uint Value => val;

    public LobbyId(uint value)
    {
        val = value;
    }

    // Allow easy conversion to/from raw uint:
    public static implicit operator uint(LobbyId id)   => id.val;
    public static explicit operator LobbyId(uint raw)  => new LobbyId(raw);

    // IEquatable<T> implementation:
    public bool Equals(LobbyId other) 
        => val == other.val;

    public override bool Equals(object obj) 
        => obj is LobbyId other && Equals(other);

    public override int GetHashCode() 
        => val.GetHashCode();

    // == and != operators:
    public static bool operator ==(LobbyId left, LobbyId right) 
        => left.Equals(right);
    public static bool operator !=(LobbyId left, LobbyId right) 
        => !left.Equals(right);

    public override string ToString() 
        => val.ToString();
}

public struct GameNetworkState
{
    public AccountAddress address;
    public bool isHost;
    public Team clientTeam;
    public Team opponentTeam;
    public User user;
    public LobbyInfo lobbyInfo;
    public LobbyParameters lobbyParameters;
    public GameState gameState;
    
    public GameNetworkState(NetworkState networkState)
    {
        address = networkState.address;
        System.Diagnostics.Debug.Assert(networkState.user != null, "networkState.user != null");
        user = networkState.user.Value;
        System.Diagnostics.Debug.Assert(networkState.lobbyInfo != null, "networkState.lobbyInfo != null");
        lobbyInfo = networkState.lobbyInfo.Value;
        System.Diagnostics.Debug.Assert(networkState.lobbyParameters != null, "networkState.lobbyParameters != null");
        lobbyParameters = networkState.lobbyParameters.Value;
        System.Diagnostics.Debug.Assert(networkState.gameState != null, "networkState.gameState != null");
        gameState = networkState.gameState.Value;
        // do some checks
        if (new AccountAddress(lobbyInfo.host_address) == address)
        {
            isHost = true;
        }
        else if (new AccountAddress(lobbyInfo.guest_address) == address)
        {
            isHost = false;
        }
        else
        {
            throw new ArgumentException($"address not in state: {address}");
        }

        if (isHost)
        {
            if (lobbyParameters.host_team == 1)
            {
                clientTeam = Team.RED;
                opponentTeam = Team.BLUE;
            }
            else
            {
                clientTeam = Team.BLUE;
                opponentTeam = Team.RED;
            }
        }
        else
        {
            if (lobbyParameters.host_team == 1)
            {
                clientTeam = Team.BLUE;
                opponentTeam = Team.RED;
            }
            else
            {
                clientTeam = Team.RED;
                opponentTeam = Team.BLUE;
            }
        }
    }

    public UserState GetUserState()
    {
        return gameState.GetUserState(isHost);
    }

    public UserState GetOpponentUserState()
    {
        return gameState.GetOpponentUserState(isHost);
    }

    public string GetProveSetupReqPlayerPrefsKey()
    {
        string key = $"{address}, {user.current_lobby}";
        return key;
    }
}

public struct NetworkState
{
    public AccountAddress address;
    public User? user;
    public LobbyInfo? lobbyInfo;
    public LobbyParameters? lobbyParameters;
    public GameState? gameState;
    
    public bool inLobby => lobbyInfo != null && lobbyParameters != null;

    public NetworkState(AccountAddress inAddress)
    {
        address = inAddress;
        user = null;
        lobbyInfo = null;
        lobbyParameters = null;
        gameState = null;
    }
    public bool CurrentLobbyOutdated()
    {
        if (!user.HasValue)
        {
            return false;
        }
        if (user?.current_lobby.)
        {
            return false;
        }
        if (!lobbyInfo.HasValue || !lobbyParameters.HasValue)
        {
            return true;
        }
        return false;
    }
    
    public override string ToString()
    {
        var simplified = new
        {
            address = address,
            user = user?.ToString(),
            lobbyInfo = lobbyInfo?.ToString(),
            lobbyParameters = lobbyParameters?.ToString(),
        };
        return JsonConvert.SerializeObject(simplified, Formatting.Indented);
    }
}

public enum SetupInputTool
{
    NONE,
    ADD,
    REMOVE,
}