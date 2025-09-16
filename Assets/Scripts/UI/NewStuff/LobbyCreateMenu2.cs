using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using Contract;

public class LobbyCreateMenu2 : MenuBase
{
    // eclipses are called blitz internally
    public TMP_Dropdown boardDropdown;
    public ButtonExtended hostRedTeamToggle;
    public ButtonExtended hostBlueTeamToggle;
    public TMP_Dropdown eclipseIntervalDropdown;
    public ButtonExtended securityModeTrueToggle;
    public ButtonExtended securityModeFalseToggle;
    public TextMeshProUGUI networkText;
    public TextMeshProUGUI contractText;
    public TextMeshProUGUI addressText;
    public TextMeshProUGUI walletText;
    public ButtonExtended multiplayerButton;
    public ButtonExtended singleplayerButton;
    public Button backButton;

    public LobbyCreateData lobbyCreateData;
    public BoardDef[] boardDefs;
    
    private void Start()
    {
        ResetBoardDropdown();
        lobbyCreateData = new LobbyCreateData() {
            isMultiplayer = false,
            boardDef = boardDefs[boardDropdown.value],
            hostTeam = true,
            blitzInterval = 0,
            securityMode = false,
        };
        multiplayerButton.onClick.AddListener(HandleMultiplayer);
        singleplayerButton.onClick.AddListener(HandleSingleplayer);
        backButton.onClick.AddListener(HandleBack);
        boardDropdown.onValueChanged.AddListener(HandleBoardDropdown);
        hostRedTeamToggle.onClick.AddListener(HandleHostRedTeamToggle);
        hostBlueTeamToggle.onClick.AddListener(HandleHostBlueTeamToggle);
        eclipseIntervalDropdown.onValueChanged.AddListener(HandleEclipseIntervalDropdown);
        securityModeTrueToggle.onClick.AddListener(HandleSecurityModeTrueToggle);
        securityModeFalseToggle.onClick.AddListener(HandleSecurityModeFalseToggle);
        Refresh();
    }

    void ResetBoardDropdown()
    {
        boardDropdown.ClearOptions();
        boardDefs = ResourceRoot.BoardDefs.ToArray();
        boardDropdown.AddOptions(boardDefs.Select(board => board.name).ToList());
        boardDropdown.SetValueWithoutNotify(0);
    }

    void ResetEclipseIntervalDropdown()
    {
        eclipseIntervalDropdown.ClearOptions();
        eclipseIntervalDropdown.AddOptions(new List<string> { "DISABLED", "EVERY TURN", "EVERY 2 TURNS", "EVERY 3 TURNS", "EVERY 4 TURNS", "EVERY 5 TURNS", "EVERY 6 TURNS" });
        eclipseIntervalDropdown.SetValueWithoutNotify(0);
    }

    public void HandleMultiplayer()
    {
        lobbyCreateData.isMultiplayer = true;
        EmitAction(new CreateLobbySignal(lobbyCreateData));
    }

    public void HandleSingleplayer()
    {
        lobbyCreateData.isMultiplayer = false;
        EmitAction(new CreateLobbySignal(lobbyCreateData));
    }

    public void HandleBack()
    {
        EmitAction(MenuAction.GotoMainMenu);
    }
    public void HandleBoardDropdown(int index)
    {
        lobbyCreateData.boardDef = boardDefs[index];
        Refresh();
    }
    public void HandleHostRedTeamToggle()
    {
        lobbyCreateData.hostTeam = true;
        Refresh();
    }
    public void HandleHostBlueTeamToggle()
    {
        lobbyCreateData.hostTeam = false;
        Refresh();
    }
    public void HandleEclipseIntervalDropdown(int index)
    {
        lobbyCreateData.blitzInterval = index;
        Refresh();
    }
    public void HandleSecurityModeTrueToggle()
    {
        lobbyCreateData.securityMode = true;
        Refresh();
    }
    public void HandleSecurityModeFalseToggle()
    {
        lobbyCreateData.securityMode = false;
        Refresh();
    }
    public override void Refresh()
    {
        bool isOnline = StellarManager.networkContext.online;
        // deal with toggle buttons
        hostRedTeamToggle.interactable = !lobbyCreateData.hostTeam;
        hostBlueTeamToggle.interactable = lobbyCreateData.hostTeam;
        securityModeTrueToggle.interactable = !lobbyCreateData.securityMode;
        securityModeFalseToggle.interactable = lobbyCreateData.securityMode;
        // deal with dropdowns
        eclipseIntervalDropdown.SetValueWithoutNotify(lobbyCreateData.blitzInterval);
        boardDropdown.SetValueWithoutNotify(boardDefs.ToList().IndexOf(lobbyCreateData.boardDef));
        // deal with text
        string networkUri = isOnline ? StellarManager.networkContext.serverUri.ToString() : "Offline";
        networkText.text = networkUri;
        contractText.text = isOnline ? StellarManager.networkContext.contractAddress : "Offline";
        addressText.text = isOnline ? StellarManager.networkContext.userAccount.AccountId : "Offline";
        walletText.text = isOnline ? StellarManager.networkContext.isWallet ? "Using Wallet" : "Using Key" : "Offline";

        multiplayerButton.interactable = isOnline;


    }
}

public class LobbyCreateData
{
    public bool isMultiplayer;
    public BoardDef boardDef;
    public bool hostTeam;
    public int blitzInterval; // 0 means disabled
    public bool securityMode;
    

    public Result<LobbyParameters> ToLobbyParameters()
    {
        uint[] maxRanks = new uint[13]; // Array size for all possible ranks (0-12)
        foreach (SMaxPawnsPerRank maxPawn in boardDef.maxPawns)
        {
            maxRanks[(int)maxPawn.rank] = (uint)maxPawn.max;
        }
        // TODO: mirror server side validation here
        Result<Board> boardResult = boardDef.ToSCVal();
        if (boardResult.IsError)
        {
            return Result<LobbyParameters>.Err(boardResult);
        }
        Board board = boardResult.Value;

        return Result<LobbyParameters>.Ok(new LobbyParameters() {
            blitz_interval = (uint)blitzInterval,
            blitz_max_simultaneous_moves = 3,
            board = board,
            board_hash = SCUtility.Get16ByteHash(board),
            dev_mode = false,
            host_team = hostTeam ? Team.RED : Team.BLUE,
            max_ranks = maxRanks,
            must_fill_all_tiles = true,
            security_mode = securityMode,
            liveUntilLedgerSeq = 0,
        });
    }
}
