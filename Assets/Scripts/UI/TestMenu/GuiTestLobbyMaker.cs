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
    public Button deleteLobbyButton;
    public Button makeLobbyButton;
    BoardDef[] boardDefs;
    public bool lobbyMade;
    public Lobby lobby;
    
    public override void Initialize()
    {
        ResetBoardDropdown();
        Refresh();
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
        if (code != 0)
        {
            Contract.ErrorCode error = (ErrorCode)code;
            msg = "Error: " + error.ToString();
        }
        statusText.text = msg;
    }
}
