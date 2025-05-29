using Contract;
using Stellar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContractTester : MonoBehaviour
{
    public Button swapAddressButton;
    public TextMeshProUGUI swapAddressText;
    
    public Button makeLobbyButton;

    public Button joinLobbyButton;

    public Button leaveLobbyButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        swapAddressButton.onClick.AddListener(OnSwapAddress);
        makeLobbyButton.onClick.AddListener(OnMakeLobby);
        joinLobbyButton.onClick.AddListener(OnJoinLobby);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobby);
    }

    bool isTestHost = true;
    void OnSwapAddress()
    {
        
        if (isTestHost)
        {
            StellarManagerTest.stellar.SetSneed(StellarManagerTest.testGuestSneed);
        }
        else
        {
            StellarManagerTest.stellar.SetSneed(StellarManagerTest.testHostSneed);
        }
        isTestHost = !isTestHost;
        swapAddressText.text = StellarManagerTest.stellar.userAddress;
    }
    async void OnMakeLobby()
    {
        Contract.LobbyParameters lobbyParameters = new Contract.LobbyParameters
        {
            board_hash = "hash",
            dev_mode = false,
            host_team = 0,
            max_ranks = new MaxRank[]
            {
                new MaxRank
                {
                    max = 1,
                    rank = 0,
                }
            },
            must_fill_all_tiles = false,
            security_mode = false,
        };
        int result = await StellarManagerTest.MakeLobbyRequest(lobbyParameters);
        Debug.Log(result);
    }

    void OnJoinLobby()
    {
        
    }

    void OnLeaveLobby()
    {
        
    }
}
