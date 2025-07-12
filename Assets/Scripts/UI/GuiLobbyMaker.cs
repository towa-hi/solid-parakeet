using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Contract;
using Stellar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyMaker : TestGuiElement
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
    public event Action<Contract.LobbyParameters> OnSubmitLobbyButton;
    public event Action<Contract.LobbyParameters> OnSinglePlayerButton;

    void Start()
    {
        backButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        makeLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnSubmitLobbyButton?.Invoke(GetLobbyParameters());
        });
        singlePlayerButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnSinglePlayerButton?.Invoke(GetLobbyParameters());
        });
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }

    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        if (isEnabled && networkUpdated)
        {
            ResetBoardDropdown();
            OnNetworkStateUpdated();
        }
    }
    
    void OnNetworkStateUpdated()
    {
        if (!isEnabled) return;
        Refresh();
    }
    
    void Refresh()
    {
        hostAddressField.text = StellarManager.GetUserAddress();
        statusText.text = "Making a new lobby";
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
        if (options.Count > 4)
        {
            boardDropdown.SetValueWithoutNotify(4);
        }
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
        List<byte[]> pawnIds = new();
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
            if (tile.setupTeam == Team.RED || tile.setupTeam == Team.BLUE)
            {
                byte[] id = new byte[2];
                int x = tile.pos.x;
                int y = tile.pos.y;
                
                // Pack team (1 bit), x (4 bits), and y (4 bits) into 9 bits
                // Bit 0: team (0 = RED, 1 = BLUE)
                // Bits 1-4: x coordinate
                // Bits 5-8: y coordinate
                int teamBit = (tile.setupTeam == Team.BLUE) ? 1 : 0;
                int packedValue = teamBit | ((x & 0xF) << 1) | ((y & 0xF) << 5);
                
                // Store packed value in first 2 bytes of the id array
                id[0] = (byte)(packedValue & 0xFF);
                id[1] = (byte)((packedValue >> 8) & 0x01);
                
                pawnIds.Add(id);
            }
        }
        Board board = new()
        {
            name = boardName,
            hex = boardDef.isHex,
            size = boardDef.boardSize,
            tiles = tilesList.ToArray(),
        };
        byte[] hash = boardDef.GetHash();
        return new()
        {
            board = board,
            board_hash = hash,
            dev_mode = false,
            host_team = Team.RED,
            max_ranks = maxRanks,
            must_fill_all_tiles = mustFillAllSetupTilesToggle.isOn,
            security_mode = securityModeToggle.isOn,
            liveUntilLedgerSeq = 0,
        };
    }
}
