using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Contract;
using Stellar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyMaker : MenuElement
{
    public TMP_Dropdown boardDropdown;
    public Toggle mustFillAllSetupTilesToggle;
    public Toggle securityModeToggle;
    public TMP_InputField hostAddressField;
    public TextMeshProUGUI statusText;
    public Button backButton;
    public Button singlePlayerButton;
    public Button makeLobbyButton;
    
    BoardDef[] boardDefs;

    public event Action OnBackButton;
    public event Action<LobbyParameters> OnSubmitLobbyButton;
    public event Action<LobbyParameters> OnSinglePlayerButton;

    void Start()
    {
        backButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        makeLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnSubmitLobbyButton?.Invoke(GetLobbyParameters());
        });
        singlePlayerButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnSinglePlayerButton?.Invoke(GetLobbyParameters());
        });
    }
    
    public override void Refresh()
    {
        ResetBoardDropdown();
        hostAddressField.text = StellarManager.networkContext.userAccount.AccountId;
        bool isOnline = StellarManager.networkContext.online;
        makeLobbyButton.interactable = isOnline;
        statusText.text = isOnline ? "Making a new lobby" : "Offline Mode: Multiplayer disabled";
        securityModeToggle.SetIsOnWithoutNotify(true);
        mustFillAllSetupTilesToggle.SetIsOnWithoutNotify(true);
        // disabled for now
        mustFillAllSetupTilesToggle.interactable = false;
    }
    
    void ResetBoardDropdown()
    {
        boardDropdown.ClearOptions();
        boardDefs = ResourceRoot.BoardDefs.ToArray();
        List<string> options = boardDefs.Select(board => board.name).ToList();
        boardDropdown.AddOptions(options);
        boardDropdown.RefreshShownValue();
    }

    LobbyParameters GetLobbyParameters()
    {
        // TODO: more secure hash later
        BoardDef boardDef = boardDefs[boardDropdown.value];
        string boardName = boardDef.boardName;
        uint[] maxRanks = new uint[13]; // Array size for all possible ranks (0-12)
        foreach (SMaxPawnsPerRank maxPawn in boardDef.maxPawns)
        {
            maxRanks[(int)maxPawn.rank] = (uint)maxPawn.max;
        }
        List<TileState> tilesList = new();
        foreach (Tile tile in boardDef.tiles)
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
            hex = boardDef.isHex,
            size = boardDef.boardSize,
            tiles = tilesList.ToArray(),
        };
        // Contract expects BoardHash = BytesN<16>
        byte[] hash = SCUtility.Get16ByteHash(board);
        return new()
        {
            blitz_interval = 1,
            blitz_max_simultaneous_moves = 3,
            board = board,
            board_hash = hash,
            dev_mode = true,
            host_team = Team.RED,
            max_ranks = maxRanks,
            must_fill_all_tiles = mustFillAllSetupTilesToggle.isOn,
            security_mode = securityModeToggle.isOn,
            liveUntilLedgerSeq = 0,
        };
    }
}
