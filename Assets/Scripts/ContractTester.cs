using System;
using System.Linq;
using System.Security.Cryptography;
using Contract;
using Stellar;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContractTester : MonoBehaviour
{
    public Button swapAddressButton;
    public TextMeshProUGUI swapAddressText;
    
    public Button makeLobbyButton;
    public Button getUserButton;
    public Button getLobbyInfoButton;
    public Button joinLobbyButton;

    public Button leaveLobbyButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        swapAddressButton.onClick.AddListener(OnSwapAddress);
        makeLobbyButton.onClick.AddListener(OnMakeLobby);
        getUserButton.onClick.AddListener(OnGetUser);
        getLobbyInfoButton.onClick.AddListener(OnGetLobbyInfo);
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
        BoardDef[] boardDefs = Resources.LoadAll<BoardDef>("Boards");
        BoardDef boardDef = boardDefs.FirstOrDefault();
        if (boardDef == null)
        {
            throw new Exception();
        }
        byte[] boardHash = boardDef.GetHash();
        Contract.LobbyParameters lobbyParameters = new Contract.LobbyParameters
        {
            board_hash = boardHash,
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

    async void OnGetUser()
    {
        User? result = await StellarManagerTest.stellar.ReqUserData(StellarManagerTest.stellar.userAddress);
        Debug.Log(result.Value);
    }

    async void OnGetLobbyInfo()
    {
        LobbyInfo? lobbyInfo = await StellarManagerTest.stellar.ReqLobbyInfo(new LobbyId(3676860869));
        var account = lobbyInfo.Value.host_address.address as SCAddress.ScAddressTypeAccount;
        var pk = account.accountId.InnerValue as PublicKey.PublicKeyTypeEd25519;
        Debug.Log(StrKey.EncodeStellarAccountId(pk.ed25519));
        Debug.Log(lobbyInfo.Value);
    }
    void OnJoinLobby()
    {
        
    }

    void OnLeaveLobby()
    {
        
    }
}
