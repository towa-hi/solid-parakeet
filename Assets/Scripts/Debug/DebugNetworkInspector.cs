using Contract;
using UnityEngine;

public class DebugNetworkInspector : MonoBehaviour
{
    [SerializeField] 
    public User user;
    [SerializeField]
    public LobbyInfo lobbyInfo;
    [SerializeField]
    public LobbyParameters lobbyParameters;

    void Awake()
    {
        
    }

    void Reset()
    {
        user = new User
        {
            current_lobby = 420,
            games_completed = 420,
        };
        lobbyInfo = new LobbyInfo
        {
            index = 0,
            guest_address = null,
            host_address = null,
            status = LobbyStatus.Aborted,
        };
        lobbyParameters = new LobbyParameters
        {
            board_hash = new byte[]
            {
            },
            dev_mode = false,
            host_team = 420,
            max_ranks = new MaxRank[]
            {
            },
            must_fill_all_tiles = false,
            security_mode = false,
        };
    }
    void Update()
    {
        NetworkState state = StellarManager.networkState;
        if (state.user.HasValue)
        {
            user = state.user.Value;
        }
        else
        {
            user = new User
            {
                current_lobby = 420,
                games_completed = 420,
            };
        }
        if (state.lobbyInfo.HasValue)
        {
            lobbyInfo = state.lobbyInfo.Value;
        }
        else
        {
            lobbyInfo = new LobbyInfo
            {
                index = 0,
                guest_address = null,
                host_address = null,
                status = LobbyStatus.WaitingForPlayers,
            };
        }
        if (state.lobbyParameters.HasValue)
        {
            lobbyParameters = state.lobbyParameters.Value;
        }
        else
        {
            lobbyParameters = new LobbyParameters
            {
                board_hash = new byte[]
                {
                },
                dev_mode = false,
                host_team = 420,
                max_ranks = new MaxRank[]
                {
                },
                must_fill_all_tiles = false,
                security_mode = false,
            };
        }
    }
}
