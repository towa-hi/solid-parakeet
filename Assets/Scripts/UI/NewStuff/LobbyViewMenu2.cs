using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Contract;

public class LobbyViewMenu2 : MenuBase
{
    public TextMeshProUGUI lobbyIdText;
    public TextMeshProUGUI contractAddressText;
    public TextMeshProUGUI userAddressText;
    public TextMeshProUGUI freighterWalletText;
    public TextMeshProUGUI boardNameText;
    public TextMeshProUGUI eclipseIntervalText;
    public TextMeshProUGUI securityModeText;
    public TextMeshProUGUI hostTeamText;
    public TextMeshProUGUI networkText;
    public TextMeshProUGUI lobbyHostAddressText;
    public TextMeshProUGUI lobbyGuestAddressText;
    public TextMeshProUGUI lobbyStatusText;
    public ButtonExtended backButton;
    public ButtonExtended leaveButton;
    public ButtonExtended enterGameButton;
    public ButtonExtended copyLobbyIdButton;
    public ButtonExtended copyContractAddressButton;
    public ButtonExtended copyUserAddressButton;
    public ButtonExtended copyHostAddressButton;
    public ButtonExtended copyGuestAddressButton;


    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
        leaveButton.onClick.AddListener(HandleLeaveLobby);
        enterGameButton.onClick.AddListener(HandleEnterGame);
        copyLobbyIdButton.onClick.AddListener(CopyLobbyId);
        copyContractAddressButton.onClick.AddListener(CopyContractAddress);
        copyUserAddressButton.onClick.AddListener(CopyUserAddress);
        copyHostAddressButton.onClick.AddListener(CopyHostAddress);
        copyGuestAddressButton.onClick.AddListener(CopyGuestAddress);
        Refresh();
    }

    public void HandleEnterGame()
    {
        _ = menuController.EnterGame();
    }

    public void HandleBack()
    {
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }

    public void HandleLeaveLobby()
    {
        _ = menuController.LeaveLobbyForMenu();
    }

    public override void Refresh()
    {
        bool isOnline = StellarManager.networkState.fromOnline;
        string networkUri = isOnline ? StellarManager.networkContext.serverUri.ToString() : "Offline";
        networkText.text = networkUri;
        contractAddressText.text = isOnline ? StellarManager.networkContext.contractAddress : "Offline";
        userAddressText.text = isOnline ? StellarManager.networkContext.userAccount.AccountId : "Offline";
        freighterWalletText.text = isOnline ? StellarManager.networkContext.isWallet ? "Using Wallet" : "Using Key" : "Offline";
        LobbyInfo? lobbyInfo = StellarManager.networkState.lobbyInfo;
        if (lobbyInfo.HasValue)
        {
            lobbyIdText.text = lobbyInfo.Value.index.ToString();
            lobbyHostAddressText.text = lobbyInfo.Value.host_address.ToString();
            lobbyGuestAddressText.text = lobbyInfo.Value.guest_address.ToString();
            lobbyStatusText.text = lobbyInfo.Value.phase.ToString();
        }
        else
        {
            lobbyIdText.text = "Not found";
            lobbyHostAddressText.text = "Not found";
            lobbyGuestAddressText.text = "Not found";
            lobbyStatusText.text = "Not found";
        }

        LobbyParameters? lobbyParameters = StellarManager.networkState.lobbyParameters;
        if (lobbyParameters.HasValue)
        {
            eclipseIntervalText.text = lobbyParameters.Value.blitz_interval.ToString();
            securityModeText.text = lobbyParameters.Value.security_mode.ToString();
            hostTeamText.text = lobbyParameters.Value.host_team.ToString();
        }
        else
        {
            eclipseIntervalText.text = "Not found";
            securityModeText.text = "Not found";
            hostTeamText.text = "Not found";
        }
        (bool startable, string reason) = IsLobbyStartable(StellarManager.networkState);
        enterGameButton.interactable = startable;
        lobbyStatusText.text = reason;
    }

    (bool startable, string reason) IsLobbyStartable(NetworkState networkState)
    {
        LobbyInfo? maybeLobbyInfo = networkState.lobbyInfo;
        LobbyParameters? maybeLobbyParameters = networkState.lobbyParameters;
        if (!maybeLobbyInfo.HasValue)
        {
            return (false, "lobby not found");
        }
        if (!maybeLobbyParameters.HasValue)
        {
            return (false, "lobby parameters not found");
        }
        LobbyInfo lobbyInfo = maybeLobbyInfo.Value;
        LobbyParameters lobbyParameters = maybeLobbyParameters.Value;

        if (lobbyInfo.phase == Phase.Aborted || lobbyInfo.phase == Phase.Finished)
        {
            string winner = lobbyInfo.subphase switch
            {
                Subphase.Guest when lobbyParameters.host_team == Team.RED => "guest (blue)",
                Subphase.Guest => "guest (red)",
                Subphase.Host when lobbyParameters.host_team == Team.RED => "host (red)",
                Subphase.Host => "host (blue)",
                Subphase.None => "tie",
                _ => "inconclusive",
            };
            return (false, $"lobby has ended. winner: {winner}");

        }
        
        // check if lobby is in a joinable state
        if (lobbyInfo.host_address == null)
        {
            return (false, "lobby.host_address is empty");
        }
        if (lobbyInfo.guest_address == null)
        {
            return (false, "lobby.guest_address is empty");
        }
        if (lobbyParameters.security_mode && lobbyInfo.phase != Phase.SetupCommit && !CacheManager.RankProofsCacheExists(StellarManager.networkState.address, lobbyInfo.index))
        {
            // TODO: more thurough cache check here
            return (false, "this client does not have the required cached data to play this lobby");
        }
        // check if your connection context is involved in the lobby
        return (true, "Game is in progress");
    }
    void CopyLobbyId()
    {
        if (lobbyIdText != null && !string.IsNullOrEmpty(lobbyIdText.text))
        {
            GUIUtility.systemCopyBuffer = lobbyIdText.text;
        }
    }

    void CopyContractAddress()
    {
        if (contractAddressText != null && !string.IsNullOrEmpty(contractAddressText.text))
        {
            GUIUtility.systemCopyBuffer = contractAddressText.text;
        }
    }

    void CopyUserAddress()
    {
        if (userAddressText != null && !string.IsNullOrEmpty(userAddressText.text))
        {
            GUIUtility.systemCopyBuffer = userAddressText.text;
        }
    }

    void CopyHostAddress()
    {
        if (lobbyHostAddressText != null && !string.IsNullOrEmpty(lobbyHostAddressText.text))
        {
            GUIUtility.systemCopyBuffer = lobbyHostAddressText.text;
        }
    }

    void CopyGuestAddress()
    {
        if (lobbyGuestAddressText != null && !string.IsNullOrEmpty(lobbyGuestAddressText.text))
        {
            GUIUtility.systemCopyBuffer = lobbyGuestAddressText.text;
        }
    }
}


