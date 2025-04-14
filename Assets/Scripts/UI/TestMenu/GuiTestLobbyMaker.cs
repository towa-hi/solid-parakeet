using System;
using System.Collections.Generic;
using Contract;
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
    public Button makeLobbyButton;
    BoardDef[] boardDefs;
    public bool lobbyMade = false;

    public event Action OnBackButton;
    public event Action OnSubmitLobbyButton;

    void Start()
    {
        backButton.onClick.AddListener(delegate { OnBackButton?.Invoke(); });
        makeLobbyButton.onClick.AddListener(delegate { OnSubmitLobbyButton?.Invoke(); });
    }
    
    public override void Initialize()
    {
        ResetBoardDropdown();
        Refresh();
        
    }


    public override void Refresh()
    {
        hostAddressField.text = StellarManagerTest.GetUserAddress();
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
        boardDropdown.RefreshShownValue();
    }

    public Contract.LobbyParameters GetLobbyParameters()
    {
        Contract.BoardDef selectedBoard = new Contract.BoardDef(boardDefs[boardDropdown.value]);
        return new Contract.LobbyParameters
        {
            board_def_name = selectedBoard.name,
            dev_mode = false,
            max_pawns = selectedBoard.default_max_pawns,
            must_fill_all_tiles = mustFillAllSetupTilesToggle.isOn,
            security_mode = securityModeToggle.isOn,
        };
    }

    public void OnLobbyMade(int code)
    {
        string msg = "Lobby successfully made!";
        switch (code)
        {
            case 0:
                msg = "Lobby successfully made!";
                break;
            case -1:
                msg = "Lobby failed because failed to simulate tx";
                break;
            case -2:
                msg = "Lobby failed because failed to send tx";
                break;
            case -3:
                msg = "Lobby failed because tx result was not success";
                break;
            case -666:
                msg = "Lobby failed because unspecified contract error";
                break;
            default:
                Contract.ErrorCode error = (ErrorCode)code;
                msg = "Lobby failed with server side error: " + error;
                break;
        }
        statusText.text = msg;
    }
}
