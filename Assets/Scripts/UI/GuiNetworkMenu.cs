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
        EnableLobbySetupModal(true);
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
        _ = GameManager.instance.stellarManager.SecondTestFunction(guestInputField.text);
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




    public TMP_Dropdown boardDropdown;
    public Toggle mustFillAllTilesToggle;
    public Toggle securityModeToggle;
    public TMP_InputField hostInputField;
    public TMP_InputField guestInputField;
    public Button cancelButton;
    public Button startButton;
    public TextMeshProUGUI startButtonText;
    BoardDef[] boardDefs;
    
    public void EnableLobbySetupModal(bool enable)
    {
        ResetLobbySetupModal();
        SetListeners();
    }

    void SetListeners()
    {
        hostInputField.onValueChanged.RemoveAllListeners();
        hostInputField.onValueChanged.AddListener(OnHostInputFieldValueChanged);
        guestInputField.onValueChanged.RemoveAllListeners();
        guestInputField.onValueChanged.AddListener(OnGuestInputFieldValueChanged);
        boardDropdown.onValueChanged.RemoveAllListeners();
        boardDropdown.onValueChanged.AddListener(OnBoardDropdownValueChanged);
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(OnInviteButton);
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(OnCancelButton);
    }
    void ResetLobbySetupModal()
    {
        hostInputField.text = StellarManager.testHost;
        guestInputField.text = StellarManager.testGuest;
        mustFillAllTilesToggle.isOn = true;
        securityModeToggle.isOn = false;
        ResetBoardDropdown();
        MarkDirty();
    }

    void ResetBoardDropdown()
    {
        boardDropdown.ClearOptions();
        boardDefs = Resources.LoadAll<BoardDef>("Boards");
        List<string> options = new List<string>();
        foreach (BoardDef board in boardDefs)
        {
            options.Add(board.name);
        }
        boardDropdown.AddOptions(options);

        // Select the first board as default
        boardDropdown.RefreshShownValue();
    }

    void OnHostInputFieldValueChanged(string val)
    {
        MarkDirty();
    }

    void OnGuestInputFieldValueChanged(string val)
    {
        MarkDirty();
    }

    void OnBoardDropdownValueChanged(int index)
    {
        MarkDirty();
    }
    
    void MarkDirty()
    {
        Debug.Log("Mark dirty");
        startButton.interactable = false;
        
        bool isBoardValid = false;
        if (boardDropdown.value >= 0 && boardDropdown.value < boardDefs.Length)
        {
            isBoardValid = true;
        }
        bool isGuestValid = StellarDotnet.IsValidStellarAddress(guestInputField.text);
        bool isHostValid = StellarDotnet.IsValidStellarAddress(hostInputField.text);
        
        if (!isBoardValid)
        {
            startButtonText.text = "select a valid board";
            return;
        }
        if (!isHostValid)
        {
            startButtonText.text = "enter a valid host";
            return;
        }
        if (!isGuestValid)
        {
            startButtonText.text = "enter a valid guest";
            return;
        }
        startButton.interactable = true;
        startButtonText.text = "send invite";
    }

    void OnInviteButton()
    {
        BoardDef selectedBoard = boardDefs[boardDropdown.value];
        MaxPawns[] default_max_pawns = new MaxPawns[selectedBoard.maxPawns.Length];
        for (int i = 0; i < selectedBoard.maxPawns.Length; i++)
        {
            SMaxPawnsPerRank oldMaxPawn = selectedBoard.maxPawns[i];
            default_max_pawns[i] = new MaxPawns()
            {
                max = oldMaxPawn.max,
                rank = (int)oldMaxPawn.rank,
            };
        }
        Contract.Tile[] tiles = new Contract.Tile[selectedBoard.tiles.Length];
        for (int i = 0; i < selectedBoard.tiles.Length; i++)
        {
            Tile oldTile = selectedBoard.tiles[i];
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
            default_max_pawns = default_max_pawns,
            is_hex = selectedBoard.isHex,
            name = selectedBoard.name,
            size = new Pos(selectedBoard.boardSize),
            tiles = tiles,
        };
        
        SendInviteReq sendInviteReq = new SendInviteReq
        {
            guest_address = guestInputField.text,
            host_address = hostInputField.text,
            ledgers_until_expiration = 999,
            parameters = new Contract.LobbyParameters
            {
                board_def = board,
                dev_mode = false,
                max_pawns = default_max_pawns,
                must_fill_all_tiles = mustFillAllTilesToggle.isOn,
                security_mode = securityModeToggle.isOn,
            },
        };
        _ = GameManager.instance.stellarManager.OnSendInviteButton(sendInviteReq);
    }

    void OnCancelButton()
    {
        _ = GameManager.instance.stellarManager.TestFunction();
    }
}
