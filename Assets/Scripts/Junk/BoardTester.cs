using System.Collections.Generic;
using Contract;
using Stellar;
using Stellar.Utilities;
using UnityEngine;

public class BoardTester : MonoBehaviour
{
    public BoardTesterGui gui;
    public BoardDef defaultBoardDef;
    public uint defaultBlitzInterval;
    public uint defaultBlitzMaxSimultaneousMoves;
    public GameObject tilePrefab;
    
    public GameObject pawnPrefab;

    public BoardGrid grid;

    
    readonly Dictionary<Vector2Int, TileView> tileViews = new();
    readonly Dictionary<PawnId, PawnView> pawnViews = new();

    public GameNetworkState gameNetworkState;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // hook up gui
        gui.OnButton1 += AutoSetupAndStartGame;
        
        
        
        
        string boardName = defaultBoardDef.boardName;
        uint[] maxRanks = new uint[13]; // Array size for all possible ranks (0-12)
        foreach (SMaxPawnsPerRank maxPawn in defaultBoardDef.maxPawns)
        {
            maxRanks[(int)maxPawn.rank] = (uint)maxPawn.max;
        }
        List<TileState> tilesList = new();
        foreach (Tile tile in defaultBoardDef.tiles)
        {
            TileState tileDef = new()
            {
                passable = tile.isPassable,
                pos = tile.pos,
                setup = tile.setupTeam,
                setup_zone = (uint)tile.autoSetupZone,
            };
            if (tileDef.setup != Team.NONE && !tileDef.passable)
            {
                Debug.LogError($"{tileDef.pos} is invalid");
            }
            tilesList.Add(tileDef);
        }
        Board board = new()
        {
            name = boardName,
            hex = defaultBoardDef.isHex,
            size = defaultBoardDef.boardSize,
            tiles = tilesList.ToArray(),
        };
        byte[] hash = SCUtility.Get16ByteHash(board);
        LobbyParameters lobbyParameters = new()
        {
            blitz_interval = defaultBlitzInterval,
            blitz_max_simultaneous_moves = defaultBlitzMaxSimultaneousMoves,
            board = board,
            board_hash =hash,
            dev_mode = true,
            host_team = Team.RED,
            max_ranks = maxRanks,
            must_fill_all_tiles = true,
            security_mode = false,
            liveUntilLedgerSeq = 0,
        };
        
        DefaultSettings defaultSettings = ResourceRoot.DefaultSettings;
        MuxedAccount.KeyTypeEd25519 guestAccount = MuxedAccount.FromSecretSeed(defaultSettings.defaultGuestSneed);
        string guestPublicAddress = StrKey.EncodeStellarAccountId(guestAccount.PublicKey);
        MuxedAccount.KeyTypeEd25519 hostAccount = MuxedAccount.FromSecretSeed(defaultSettings.defaultHostSneed);
        string hostPublicAddress = StrKey.EncodeStellarAccountId(hostAccount.PublicKey);
        LobbyInfo lobbyInfo = new LobbyInfo
        {
            guest_address = new AccountAddress(guestPublicAddress),
            host_address = new AccountAddress(hostPublicAddress),
            index = new LobbyId(12345),
            last_edited_ledger_seq = 0,
            phase = Phase.SetupCommit,
            subphase = Subphase.Both,
            liveUntilLedgerSeq = 0,
        };
        List<PawnState> pawnStates = new();
        foreach (TileState tile in lobbyParameters.board.tiles)
        {
            uint user = 0;
            if (tile.setup == Team.BLUE)
            {
                user = 1;
            }
            if (tile.setup is Team.RED or Team.BLUE)
            {
                PawnState pawnState = new PawnState
                {
                    alive = true,
                    moved = false,
                    moved_scout = false,
                    pawn_id = EncodePawnId(tile.pos, user),
                    pos = tile.pos,
                    rank = Rank.UNKNOWN,
                    zz_revealed = false,
                };
                pawnStates.Add(pawnState);
            }
        }

        UserMove[] moves = new UserMove[]
        {
            new UserMove
            {
                move_hashes = new byte[][]
                {
                },
                move_proofs = new HiddenMove[]
                {
                },
                needed_rank_proofs = new PawnId[]
                {
                }
            },
            new UserMove
            {
                move_hashes = new byte[][]
                {
                },
                move_proofs = new HiddenMove[]
                {
                },
                needed_rank_proofs = new PawnId[]
                {
                }
            }
        };
        GameState gameState = new GameState
        {
            moves = moves,
            pawns = pawnStates.ToArray(),
            rank_roots = new byte[][] { },
            turn = 1,
            liveUntilLedgerSeq = 0,
        };
        NetworkState fakeNetworkState = new NetworkState
        {
            address = hostPublicAddress,
            user = new User
            {
                current_lobby = new LobbyId(12345),
                games_completed = 0
            },
            lobbyInfo = lobbyInfo,
            lobbyParameters = lobbyParameters,
            gameState = gameState,
        };
        gameNetworkState = new GameNetworkState(fakeNetworkState);
        
        // draw the board
        grid.SetBoard(board.hex);
        foreach (TileState tile in gameNetworkState.lobbyParameters.board.tiles)
        {
            Vector3 worldPosition = grid.CellToWorld(tile.pos);
            GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity, transform);
            TileView tileView = tileObject.GetComponent<TileView>();
            tileView.Initialize(tile, board.hex);
            tileView.SetTileDebug();
            tileViews.Add(tile.pos, tileView);
        }
        // draw pawns
        foreach (PawnState pawn in gameNetworkState.gameState.pawns)
        {
            GameObject pawnObject = Instantiate(pawnPrefab, transform);
            PawnView pawnView = pawnObject.GetComponent<PawnView>();
            pawnView.Initialize(pawn, tileViews[pawn.pos]);
            pawnViews.Add(pawn.pawn_id, pawnView);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void ButtonExampleFunction()
    {
        Debug.Log("wewlad");
    }

    void AutoSetupAndStartGame()
    {
        Dictionary<PawnId, (int, PawnState)> pawnsMap = CreatePawnsMap(gameNetworkState.gameState.pawns);
        Dictionary<Vector2Int, Rank> autoSetupRed = gameNetworkState.AutoSetup(Team.RED);
        foreach ((Vector2Int pos, Rank rank) in autoSetupRed)
        {
            PawnId pawnId = gameNetworkState.GetAlivePawnFromPosUnchecked(pos).pawn_id;
            (int index, PawnState pawn) = pawnsMap[pawnId];
            pawn.rank = rank;
            gameNetworkState.gameState.pawns[index] = pawn;
        }
        Dictionary<Vector2Int, Rank> autoSetupBlue = gameNetworkState.AutoSetup(Team.BLUE);
        foreach ((Vector2Int pos, Rank rank) in autoSetupBlue)
        {
            PawnId pawnId = gameNetworkState.GetAlivePawnFromPosUnchecked(pos).pawn_id;
            (int index, PawnState pawn) = pawnsMap[pawnId];
            pawn.rank = rank;
            gameNetworkState.gameState.pawns[index] = pawn;
        }
        gameNetworkState.lobbyInfo.phase = Phase.MoveCommit;
        gameNetworkState.gameState.turn = 1;
        gameNetworkState.lobbyInfo.subphase = Subphase.Both;
        
        foreach (PawnView pawnView in pawnViews.Values)
        {
            Debug.Log(pawnView.pawnId);
            PawnState pawnState = gameNetworkState.GetPawnFromId(pawnView.pawnId);
            if (pawnState.rank is Rank rank)
            {
                pawnView.TestSetSprite(rank, pawnState.GetTeam());
            }
            
        }
    }

    uint EncodePawnId(Vector2Int setupPos, uint userIndex)
    {
        uint id = 0u;
        id |= (userIndex & 1u);
        id |= (((uint)setupPos.x & 0xFu) << 1);
        id |= (((uint)setupPos.y & 0xFu) << 5);
        return id;
    }

    void DecodePawnId(uint pawnId, out Vector2Int setupPos, out uint userIndex)
    {
        userIndex = pawnId & 1u;
        int x = (int)((pawnId >> 1) & 0xFu);
        int y = (int)((pawnId >> 5) & 0xFu);
        setupPos = new Vector2Int(x, y);
    }

    Dictionary<PawnId, (int, PawnState)> CreatePawnsMap(PawnState[] pawnStates)
    {
        Dictionary<PawnId, (int, PawnState)> map = new();
        for (int index = 0; index < pawnStates.Length; index++)
        {
            PawnState pawn = pawnStates[index];
            map[pawn.pawn_id] = (index, pawn);
        }
        return map;
    }
}
