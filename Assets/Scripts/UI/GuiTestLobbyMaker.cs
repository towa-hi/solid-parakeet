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
        StellarManagerTest.OnNetworkStateUpdated += OnNetworkStateUpdated;
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
        if (options.Count > 4)
        {
            boardDropdown.SetValueWithoutNotify(4);
        }
        boardDropdown.RefreshShownValue();
    }

    Contract.LobbyParameters GetLobbyParameters()
    {
        // Contract.BoardDef selectedBoard = new Contract.BoardDef(boardDefs[boardDropdown.value]);
        // return new Contract.LobbyParameters
        // {
        //     board_def_name = selectedBoard.name,
        //     dev_mode = false,
        //     max_pawns = selectedBoard.default_max_pawns,
        //     must_fill_all_tiles = mustFillAllSetupTilesToggle.isOn,
        //     security_mode = securityModeToggle.isOn,
        // };
        return new Contract.LobbyParameters() { };
    }
}
