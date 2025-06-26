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

    public static DebugNetworkInspector instance;
    void Awake()
    {
        instance = this;
    }

    public static void UpdateDebugNetworkInspector(NetworkState networkState)
    {
        
    }
}
