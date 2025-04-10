using System;
using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class GuiNetworkMenu : MenuElement
{
    public TextMeshProUGUI walletConnectedText;
    public Button connectWalletButton;
    public Button testButton;
    public Button secondTestButton;
    public Button oldContractButton;
    public Button newContractButton;
    public Button toggleFiltersButton;
    
    public Button backButton;

    public TextMeshProUGUI connectionStatusText;
    public TextMeshProUGUI userNameText;
    public TextMeshProUGUI userIdText;
    public TextMeshProUGUI userGamesPlayedText;
    public TextMeshProUGUI userCurrentLobbyText;
    public TextMeshProUGUI contractText;
    public TextMeshProUGUI toggleFiltersText;
    //public TMP_InputField guestInputField;
    public TMP_InputField contractInputField;
    public event Action OnConnectWalletButton;
    public event Action OnTestButton;
    public event Action OnBackButton;

    void Start()
    {
        connectWalletButton.onClick.AddListener(HandleConnectWalletButton);
        testButton.onClick.AddListener(HandleTestButton);
        secondTestButton.onClick.AddListener(HandleSecondTestButton);
        newContractButton.onClick.AddListener(HandleNewContractButton);
        toggleFiltersButton.onClick.AddListener(HandleToggleFiltersButton);
        backButton.onClick.AddListener(HandleBackButton);
        GameManager.instance.stellarManager.OnCurrentUserChanged += OnCurrentUserChangedEvent;
        GameManager.instance.stellarManager.OnContractChanged += OnContractChangedEvent;
        OnContractChangedEvent(GameManager.instance.stellarManager.stellar.contractId);
        //EnableLobbySetupModal(true);
    }

    void OnWalletConnectedEvent(bool success)
    {
        string message = success ? "Connected" : "Disconnected";
        walletConnectedText.text = message;
    }

    void OnContractChangedEvent(string contract)
    {
        contractText.text = contract;
    }
    
    void OnCurrentUserChangedEvent()
    {
        User? currentUser = GameManager.instance.stellarManager.currentUser;
        Debug.Log("OnCurrentUserChangedEvent");
        if (currentUser != null)
        {
            Debug.Log("setting currentUser");
            userNameText.text = currentUser.Value.name;
            userIdText.text = currentUser.Value.index;
            userGamesPlayedText.text = currentUser.Value.games_completed.ToString();
        }
        else
        {
            Debug.Log("clearing currentUser");
            userNameText.text = "no user";
            userIdText.text = "";
            userGamesPlayedText.text = "";
            userCurrentLobbyText.text = "";
        }
    }

    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        connectWalletButton.interactable = enable;
        testButton.interactable = enable;
        backButton.interactable = enable;
        
    }
    void HandleConnectWalletButton()
    {
        _ = GameManager.instance.RunWithEvents<bool>(GameManager.instance.stellarManager.OnConnectWallet);
        OnConnectWalletButton?.Invoke();
    }

    void HandleTestButton()
    {
        _ = GameManager.instance.stellarManager.TestFunction();
        OnTestButton?.Invoke();
    }

    void HandleBackButton()
    {
        OnBackButton?.Invoke();
    }
    
    void HandleSecondTestButton()
    {
        //_ = GameManager.instance.stellarManager.SecondTestFunction(guestInputField.text);
    }

    void HandleNewContractButton()
    {
        GameManager.instance.stellarManager.SetContract(contractInputField.text);
    }

    bool filterOn;
    void HandleToggleFiltersButton()
    {
        if (!filterOn)
        {
            filterOn = true;
            toggleFiltersText.text = "event filter enabled";
        }
        else
        {
            filterOn = false;
            toggleFiltersText.text = "event filter disabled";
        }
    }


    //
    // void OnInviteButton()
    // {
    //     BoardDef selectedBoard = boardDefs[boardDropdown.value];
    //     MaxPawns[] default_max_pawns = new MaxPawns[selectedBoard.maxPawns.Length];
    //     for (int i = 0; i < selectedBoard.maxPawns.Length; i++)
    //     {
    //         SMaxPawnsPerRank oldMaxPawn = selectedBoard.maxPawns[i];
    //         default_max_pawns[i] = new MaxPawns()
    //         {
    //             max = oldMaxPawn.max,
    //             rank = (int)oldMaxPawn.rank,
    //         };
    //     }
    //     Contract.Tile[] tiles = new Contract.Tile[selectedBoard.tiles.Length];
    //     for (int i = 0; i < selectedBoard.tiles.Length; i++)
    //     {
    //         Tile oldTile = selectedBoard.tiles[i];
    //         tiles[i] = new Contract.Tile
    //         {
    //             auto_setup_zone = oldTile.autoSetupZone,
    //             is_passable = oldTile.isPassable,
    //             pos = new Pos(oldTile.pos),
    //             setup_team = (int)oldTile.setupTeam,
    //         };
    //     }
    //     Contract.BoardDef board = new Contract.BoardDef
    //     {
    //         default_max_pawns = default_max_pawns,
    //         is_hex = selectedBoard.isHex,
    //         name = selectedBoard.name,
    //         size = new Pos(selectedBoard.boardSize),
    //         tiles = tiles,
    //     };
    //     
    //     SendInviteReq sendInviteReq = new SendInviteReq
    //     {
    //         guest_address = guestInputField.text,
    //         host_address = hostInputField.text,
    //         ledgers_until_expiration = 999,
    //         parameters = new Contract.LobbyParameters
    //         {
    //             board_def = board,
    //             dev_mode = false,
    //             max_pawns = default_max_pawns,
    //             must_fill_all_tiles = mustFillAllTilesToggle.isOn,
    //             security_mode = securityModeToggle.isOn,
    //         },
    //     };
    //     _ = GameManager.instance.stellarManager.OnSendInviteButton(sendInviteReq);
    // }
    //
    // void OnCancelButton()
    // {
    //     _ = GameManager.instance.stellarManager.CheckInvites();
    // }
    //
    // void OnInviteSent(bool status)
    // {
    //     if (status)
    //     {
    //         waitingForInvites = true;
    //         waitingScreen.SetActive(true);
    //     }
    // }
    //
    // void OnInviteCheck(List<Invite> invites)
    // {
    //     inviteList.gameObject.SetActive(true);
    //     inviteList.Initialize(invites);
    //     
    // }
    
}
