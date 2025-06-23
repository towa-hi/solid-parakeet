using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Contract;
using Stellar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestLobbyMaker : TestGuiElement
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

    Contract.LobbyParameters GetLobbyParameters()
    {
        // TODO: more secure hash later
        BoardDef boardDef = boardDefs[boardDropdown.value];
        string boardName = boardDef.boardName;
        SHA256 sha256 = SHA256.Create();
        byte[] boardHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(boardName));
        MaxRank[] maxRanks = new MaxRank[boardDef.maxPawns.Length];
        for (int i = 0; i < boardDef.maxPawns.Length; i++)
        {
            maxRanks[i] = new MaxRank()
            {
                max = (uint)boardDef.maxPawns[i].max,
                rank = boardDef.maxPawns[i].rank,
            };
        }
        return new LobbyParameters
        {
            board_hash = boardHash,
            dev_mode = true,
            host_team = 1,
            max_ranks = maxRanks,
            must_fill_all_tiles = mustFillAllSetupTilesToggle.isOn,
            security_mode = securityModeToggle.isOn,
        };
    }
}
