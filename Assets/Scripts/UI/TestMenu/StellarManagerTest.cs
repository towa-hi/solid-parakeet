using System;
using System.Threading.Tasks;
using Contract;
using Stellar.RPC;
using UnityEngine;

public class StellarManagerTest : MonoBehaviour
{
    public static string test;
    public static StellarDotnet stellar;
    
    public static string testContract = "CBIYKACJD4FO5HLDB4Y57X42OB22UFDJ6WH4EE56FKLC23ETCLV6K2GC";
    public static string testGuest = "GD6APTUYGQJUR2Q5QQGKCZNBWE7BVLTZQAJ4LRX5ZU55ODB65FMJBGGO";
    public static string testHost = "GCVQEM7ES6D37BROAMAOBYFJSJEWK6AYEYQ7YHDKPJ57Z3XHG2OVQD56";
    public static string testHostSneed = "SDXM6FOTHMAD7Y6SMPGFMP4M7ULVYD47UFS6UXPEAIAPF7FAC4QFBLIV";

    public static event Action<string> OnContractIdChanged;
    public static event Action<string> OnAccountIdChanged;
    
    void Awake()
    {
        stellar = new StellarDotnet(testHostSneed, testContract);
    }
    
    public void Initialize()
    {
        
    }

    public static void SetContractId(string contractId)
    {
        stellar.SetContractId(contractId);
        OnContractIdChanged?.Invoke(contractId);
    }

    public static void SetAccountId(string accountSneed)
    {
        stellar.SetAccountId(accountSneed);
        OnAccountIdChanged?.Invoke(accountSneed);
    }

    public async Task<bool> SendInvite(InviteMenuParameters parameters)
    {
        MaxPawns[] defaultMaxPawns = new MaxPawns[parameters.boardDef.maxPawns.Length];
        for (int i = 0; i < parameters.boardDef.maxPawns.Length; i++)
        {
            SMaxPawnsPerRank oldMaxPawn = parameters.boardDef.maxPawns[i];
            defaultMaxPawns[i] = new MaxPawns()
            {
                max = oldMaxPawn.max,
                rank = (int)oldMaxPawn.rank,
            };
        }
        Contract.Tile[] tiles = new Contract.Tile[parameters.boardDef.tiles.Length];
        for (int i = 0; i < parameters.boardDef.tiles.Length; i++)
        {
            Tile oldTile = parameters.boardDef.tiles[i];
            tiles[i] = new Contract.Tile
            {
                auto_setup_zone = oldTile.autoSetupZone,
                is_passable = oldTile.isPassable,
                pos = new Pos(oldTile.pos),
                setup_team = (int)oldTile.setupTeam,
            };
        }
        Contract.BoardDef board = new Contract.BoardDef
        {
            default_max_pawns = defaultMaxPawns,
            is_hex = parameters.boardDef.isHex,
            name = parameters.boardDef.name,
            size = new Pos(parameters.boardDef.boardSize),
            tiles = tiles,
        };
        SendInviteReq sendInviteReq = new SendInviteReq
        {
            guest_address = parameters.guestAddress,
            host_address = parameters.hostAddress,
            ledgers_until_expiration = 999,
            parameters = new Contract.LobbyParameters
            {
                board_def = board,
                dev_mode = false,
                max_pawns = defaultMaxPawns,
                must_fill_all_tiles = parameters.mustFillAllSetupTiles,
                security_mode = parameters.securityMode,
            },
        };
        GetTransactionResult result = await stellar.CallParameterlessFunction("send_invite", sendInviteReq);
        bool status = result.Status == GetTransactionResultStatus.SUCCESS;
        return status;
    }
}
