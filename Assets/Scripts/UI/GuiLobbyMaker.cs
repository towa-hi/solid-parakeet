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
        List<Contract.Tile> tilesList = new();
        foreach (Tile tile in boardDef.tiles)
        {
            int newSetupTeam = 0;
            switch (tile.setupTeam)
            {
                case Team.NONE:
                    newSetupTeam = 2;
                    break;
                case Team.RED:
                    newSetupTeam = 0;
                    break;
                case Team.BLUE:
                    newSetupTeam = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            Contract.Tile tileDef = new()
            {
                passable = tile.isPassable,
                pos = new Pos(tile.pos),
                setup = (uint)newSetupTeam,
            };
            tilesList.Add(tileDef);
        }
        Board board = new()
        {
            name = boardName,
            hex = boardDef.isHex,
            size = new Pos(boardDef.boardSize),
            tiles = tilesList.ToArray(),
        };
        byte[] hash = boardDef.GetHash();
        return new()
        {
            board = board,
            board_hash = hash,
            dev_mode = false,
            host_team = 0,
            max_ranks = maxRanks,
            must_fill_all_tiles = mustFillAllSetupTilesToggle.isOn,
            security_mode = securityModeToggle.isOn,
            liveUntilLedgerSeq = 0,
        };
    }
}
