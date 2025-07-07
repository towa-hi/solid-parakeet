using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;

public class HumanDebugNetworkInspector : MonoBehaviour
{
    [Header("Client State")] 
    [SerializeField] public bool isRunning;
    [SerializeField] public bool isInitialized;
    [SerializeField] public SerializableCache cache;
    [Header("Network State")]
    [SerializeField] public string address = "";
    [SerializeField] public bool inLobby;
    
    [Header("User")]
    [SerializeField] public bool userHasValue;
    [SerializeField] public SerializableUser _user = new SerializableUser();
    
    [Header("Lobby Info")]
    [SerializeField] public bool lobbyInfoHasValue;
    [SerializeField] public SerializableLobbyInfo _lobbyInfo = new SerializableLobbyInfo();
    
    [Header("Lobby Parameters")]
    [SerializeField] public bool lobbyParametersHasValue;
    [SerializeField] public SerializableLobbyParameters _lobbyParameters = new SerializableLobbyParameters();
    
    [Header("Game State")]
    [SerializeField] public bool gameStateHasValue;
    [SerializeField] public SerializableGameState _gameState = new SerializableGameState();

    static HumanDebugNetworkInspector instance;

    void Awake()
    {
        instance = this;
    }
    
    public static void UpdateCache(Dictionary<PawnId, CachedRankProof> hiddenRanksAndMerkleProofs, Dictionary<string, HiddenMove> hiddenMoveCache)
    {
        List<SerializableRankProofEntry> rankProofEntries = new();
        foreach ((PawnId index, CachedRankProof rankProof) in hiddenRanksAndMerkleProofs)
        {
            rankProofEntries.Add(new SerializableRankProofEntry
            {
                index = index,
                cachedRankProof = rankProof,
            });
        }
        List<SerializableHiddenMoveEntry> hiddenMoveEntries = new();
        foreach ((string index, HiddenMove hiddenMove) in hiddenMoveCache)
        {
            hiddenMoveEntries.Add(new SerializableHiddenMoveEntry
            {
                index = index,
                hiddenMove = hiddenMove,
            });
        }
        instance.cache = new()
        {
            hiddenRanks = rankProofEntries,
            hiddenMoves = hiddenMoveEntries,
        };
    }
    public void UpdateDebugNetworkInspector(NetworkState networkState)
    {
        
        // Update User
        userHasValue = networkState.user.HasValue;
        if (networkState.user.HasValue)
        {
            User user = networkState.user.Value;
            _user.hasValue = true;
            _user.currentLobbyHasValue = user.current_lobby != 0;
            _user.currentLobby = user.current_lobby;
            _user.gamesCompleted = user.games_completed;
        }
        else
        {
            _user.hasValue = false;
            _user.currentLobbyHasValue = false;
            _user.currentLobby = 42069;
            _user.gamesCompleted = 42069;
            _user.index = "NULL";
        }
        
        // Update LobbyInfo
        lobbyInfoHasValue = networkState.lobbyInfo.HasValue;
        if (networkState.lobbyInfo.HasValue)
        {
            var lobbyInfo = networkState.lobbyInfo.Value;
            _lobbyInfo.hasValue = true;
            _lobbyInfo.guestAddressHasValue = lobbyInfo.guest_address.HasValue;
            _lobbyInfo.guestAddress = lobbyInfo.guest_address?.ToString() ?? "NULL";
            _lobbyInfo.hostAddressHasValue = lobbyInfo.host_address.HasValue;
            _lobbyInfo.hostAddress = lobbyInfo.host_address?.ToString() ?? "NULL";
            _lobbyInfo.index = lobbyInfo.index;
            _lobbyInfo.phase = lobbyInfo.phase;
            _lobbyInfo.subphase = lobbyInfo.subphase;
            _lobbyInfo.liveUntilLedgerSeq = lobbyInfo.liveUntilLedgerSeq;
        }
        else
        {
            _lobbyInfo.hasValue = false;
            _lobbyInfo.guestAddressHasValue = false;
            _lobbyInfo.guestAddress = "NULL";
            _lobbyInfo.hostAddressHasValue = false;
            _lobbyInfo.hostAddress = "NULL";
            _lobbyInfo.index = 42069;
            _lobbyInfo.phase = (Phase)42069;
            _lobbyInfo.subphase = (Subphase)42069;
            _lobbyInfo.liveUntilLedgerSeq = 42069;
        }
        
        // Update LobbyParameters
        lobbyParametersHasValue = networkState.lobbyParameters.HasValue;
        if (networkState.lobbyParameters.HasValue)
        {
            LobbyParameters lobbyParams = networkState.lobbyParameters.Value;
            _lobbyParameters.hasValue = true;
            _lobbyParameters.board = lobbyParams.board;
            _lobbyParameters.boardHash = lobbyParams.board_hash;
            _lobbyParameters.devMode = lobbyParams.dev_mode;
            _lobbyParameters.hostTeam = lobbyParams.host_team;
            _lobbyParameters.maxRanks = lobbyParams.max_ranks;
            _lobbyParameters.mustFillAllTiles = lobbyParams.must_fill_all_tiles;
            _lobbyParameters.securityMode = lobbyParams.security_mode;
            _lobbyParameters.liveUntilLedgerSeq = lobbyParams.liveUntilLedgerSeq;
        }
        else
        {
            _lobbyParameters.hasValue = false;
            _lobbyParameters.board = new Board { name = "NULL_BOARD", hex = false, size = new Vector2Int { x = 42069, y = 42069 }, tiles = Array.Empty<Contract.TileState>() };
            _lobbyParameters.boardHash = new byte[] { 42, 0, 69 };
            _lobbyParameters.devMode = false;
            _lobbyParameters.hostTeam = Team.NONE;
            _lobbyParameters.maxRanks = new uint[] { 42069 };
            _lobbyParameters.mustFillAllTiles = false;
            _lobbyParameters.securityMode = false;
            _lobbyParameters.liveUntilLedgerSeq = 42069;
        }
        
        // Update GameState
        gameStateHasValue = networkState.gameState.HasValue;
        if (networkState.gameState.HasValue)
        {
            GameState gameState = networkState.gameState.Value;
            _gameState.hasValue = true;
            _gameState.turn = gameState.turn;
            _gameState.liveUntilLedgerSeq = gameState.liveUntilLedgerSeq;
            
            // Convert UserMoves
            _gameState.moves = gameState.moves?.Select(move => new SerializableUserMove
            {
                moveHash = move.move_hash,
                moveProofHasValue = move.move_proof.HasValue,
                moveProof = move.move_proof.HasValue ? new SerializableHiddenMove
                {
                    hasValue = true,
                    pawnID = move.move_proof.Value.pawn_id.Value,
                    salt = move.move_proof.Value.salt,
                    startPos = new SerializablePos { x = move.move_proof.Value.start_pos.x, y = move.move_proof.Value.start_pos.y },
                    targetPos = new SerializablePos { x = move.move_proof.Value.target_pos.x, y = move.move_proof.Value.target_pos.y }
                } : new SerializableHiddenMove { hasValue = false, pawnID = 42069 },
                neededRankProofs = move.needed_rank_proofs?.Select(id => id.Value).ToArray()
            }).ToArray();
            
            // Convert PawnStates
            _gameState.pawns = gameState.pawns?.Select(pawn => new SerializablePawnState
            {
                alive = pawn.alive,
                moved = pawn.moved,
                movedScout = pawn.moved_scout,
                pawnID = pawn.pawn_id.Value,
                pos = new SerializablePos { x = pawn.pos.x, y = pawn.pos.y },
                rank = pawn.rank ?? (Rank)42069,
                rankHasValue = pawn.rank.HasValue
            }).ToArray();
            
            // Convert UserSetups
            // _gameState.setups = gameState.setups?.Select(setup => new SerializableUserSetup
            // {
            //     setupHash = setup.setup_hash,
            //     setup = setup.setup.Value..Select(s => new SerializableSetup
            //     {
            //         salt = s.salt,
            //         setupCommits = s.setup_commits?.Select(commit => new SerializableSetupCommit
            //         {
            //             hiddenRankHash = commit.hidden_rank_hash,
            //             pawnID = commit.pawn_id.Value
            //         }).ToArray()
            //     }).ToArray()
            // }).ToArray();
        }
        else
        {
            _gameState.hasValue = false;
            _gameState.turn = 42069;
            _gameState.liveUntilLedgerSeq = 42069;
            _gameState.moves = Array.Empty<SerializableUserMove>();
            _gameState.pawns = Array.Empty<SerializablePawnState>();
            _gameState.setups = Array.Empty<SerializableUserSetup>();
        }
        
    }
}
[Serializable]
public class SerializableUser
{
    public bool hasValue;
    public bool currentLobbyHasValue;
    public uint currentLobby = 42069;
    public uint gamesCompleted;
    public string index = "";
}

[Serializable]
public class SerializableLobbyInfo
{
    public bool hasValue;
    public bool guestAddressHasValue;
    public string guestAddress = "";
    public bool hostAddressHasValue;
    public string hostAddress = "";
    public uint index;
    public Phase phase;
    public Subphase subphase;
    public long liveUntilLedgerSeq;
}

[Serializable]
public class SerializableLobbyParameters
{
    public bool hasValue;
    public Board board;
    public byte[] boardHash;
    public bool devMode;
    public Team hostTeam;
    public uint[] maxRanks;
    public bool mustFillAllTiles;
    public bool securityMode;
    public long liveUntilLedgerSeq;
}

[Serializable]
public class SerializableGameState
{
    public bool hasValue;
    public SerializableUserMove[] moves;
    public SerializablePawnState[] pawns;
    public SerializableUserSetup[] setups;
    public uint turn;
    public long liveUntilLedgerSeq;
}

[Serializable]
public class SerializableUserMove
{
    public byte[] moveHash;
    public bool moveProofHasValue;
    public SerializableHiddenMove moveProof = new SerializableHiddenMove();
    public uint[] neededRankProofs;
}

[Serializable]
public class SerializablePawnState
{
    public bool alive;
    public bool moved;
    public bool movedScout;
    public uint pawnID;
    public SerializablePos pos = new SerializablePos();
    public bool rankHasValue;
    public Rank rank = (Rank)42069;
}

[Serializable]
public class SerializableUserSetup
{
    public SerializableSetup[] setup;
    public byte[] setupHash;
}

[Serializable]
public class SerializableHiddenMove
{
    public bool hasValue;
    public uint pawnID = 42069;
    public ulong salt;
    public SerializablePos startPos = new SerializablePos();
    public SerializablePos targetPos = new SerializablePos();
}

[Serializable]
public class SerializableSetup
{
    public ulong salt;
    public SerializableSetupCommit[] setupCommits;
}

[Serializable]
public class SerializableSetupCommit
{
    public byte[] hiddenRankHash;
    public uint pawnID;
}

[Serializable]
public class SerializablePos
{
    public int x;
    public int y;
}
[Serializable]
public class SerializableCache
{
    public List<SerializableRankProofEntry> hiddenRanks;
    public List<SerializableHiddenMoveEntry> hiddenMoves;
}
[Serializable]
public class SerializableRankProofEntry
{
    public PawnId index;
    public CachedRankProof cachedRankProof;
}
[Serializable]
public class SerializableHiddenMoveEntry
{
    public string index;
    public HiddenMove hiddenMove;
}