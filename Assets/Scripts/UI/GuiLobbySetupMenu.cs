using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbySetupMenu : MenuElement
{
    #region Public Fields

    public Button cancelButton;
    public Button startButton;
    public TextMeshProUGUI startButtonText;
    public TMP_Dropdown boardDropdown;
    public Toggle mustFillAllTilesToggle;
    public Button teamButton;
    public TextMeshProUGUI teamButtonText;
    public TMP_InputField addressInput;

    public event Action OnCancelButton;
    public event Action<LobbyParameters> OnStartButton;

    public BoardDef[] boardDefs;
    public BoardDef selectedBoardDef;

    public int hostTeam;
    public bool mustFillAllTiles;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        // Wire up UI events
        cancelButton.onClick.AddListener(HandleCancelButton);
        startButton.onClick.AddListener(HandleStartButton);
        boardDropdown.onValueChanged.AddListener(HandleBoardDropdown);
        mustFillAllTilesToggle.onValueChanged.AddListener(HandleMustFillAllTilesToggle);
        teamButton.onClick.AddListener(HandleTeamButton);
        addressInput.onValueChanged.AddListener(HandleAddressInput);
    }

    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        cancelButton.interactable = enable;
        startButton.interactable = enable;

        // Reset default selections when showing the menu
        hostTeam = 1;
        mustFillAllTiles = true;
        addressInput.text = string.Empty;
        UpdateTeamButton();
        PopulateBoardDropdown();
        UpdateLobbyButton();
    }

    #endregion

    #region UI Update Methods

    void UpdateLobbyButton()
    {
        // Determine configuration validity based on board selection and address input
        bool isBoardValid = selectedBoardDef != null;
        bool isAddressValid = StellarDotnet.IsValidStellarAddress(addressInput.text);
        bool isValidConfig = isBoardValid && isAddressValid;

        // Set appropriate message for the start button based on UI state
        if (!isBoardValid)
        {
            startButtonText.text = "Please select a board";
        }
        else if (!isAddressValid)
        {
            startButtonText.text = "Please enter a valid address";
        }
        else
        {
            startButtonText.text = "Start Lobby";
        }

        startButton.interactable = isValidConfig;
    }

    void UpdateTeamButton()
    {
        // Update the team button's appearance based on the current hostTeam value
        if (hostTeam == 1)
        {
            teamButton.image.color = Color.red;
            teamButtonText.text = "RED";
        }
        else
        {
            teamButton.image.color = Color.blue;
            teamButtonText.text = "BLUE";
        }

        // Refresh the lobby button in case changes affect overall UI configuration.
        UpdateLobbyButton();
    }

    #endregion

    #region Event Handlers

    void HandleCancelButton()
    {
        OnCancelButton?.Invoke();
    }

    void HandleStartButton()
    {
        // Create lobby parameters based on the current configuration
        LobbyParameters lobbyParameters = new LobbyParameters
        {
            hostTeam = hostTeam,
            guestTeam = Shared.OppTeam(hostTeam),
            board = selectedBoardDef,
            maxPawns = selectedBoardDef.maxPawns,
            mustFillAllTiles = mustFillAllTiles,
        };

        OnStartButton?.Invoke(lobbyParameters);
    }

    void PopulateBoardDropdown()
    {
        boardDefs = Resources.LoadAll<BoardDef>("Boards");
        if (boardDefs == null || boardDefs.Length == 0)
        {
            Debug.LogWarning("No BoardDef objects found in Resources/Boards!");
            return;
        }

        // Populate dropdown options from loaded BoardDef objects
        List<string> options = new List<string>();
        foreach (BoardDef board in boardDefs)
        {
            options.Add(board.name);
        }
        boardDropdown.ClearOptions();
        boardDropdown.AddOptions(options);

        // Select the first board as default
        HandleBoardDropdown(0);
        boardDropdown.RefreshShownValue();
    }

    void HandleBoardDropdown(int index)
    {
        if (boardDefs != null && boardDefs.Length > index)
        {
            selectedBoardDef = boardDefs[index];
        }
        else
        {
            selectedBoardDef = null;
        }
        UpdateLobbyButton();
    }

    void HandleTeamButton()
    {
        // Toggle between teams 1 and 2
        hostTeam = hostTeam switch
        {
            1 => 2,
            2 => 1,
            _ => hostTeam
        };

        UpdateTeamButton();
    }

    void HandleMustFillAllTilesToggle(bool isOn)
    {
        mustFillAllTiles = isOn;
        UpdateLobbyButton();
    }

    public void HandleAddressInput(string input)
    {
        UpdateLobbyButton();
    }

    #endregion
}
