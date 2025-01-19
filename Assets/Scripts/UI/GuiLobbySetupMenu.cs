using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GuiLobbySetupMenu : MenuElement
{
    public Button cancelButton;
    public Button startButton;
    public TMP_Dropdown boardDropdown;
    public Toggle mustFillAllTilesToggle;
    public Button teamButton;
    public event Action OnCancelButton;
    public event Action<LobbyParameters> OnStartButton;

    public BoardDef[] boardDefs;
    public BoardDef selectedBoardDef;
    
    public int hostTeam;
    public bool mustFillAllTiles;
    void Start()
    {
        cancelButton.onClick.AddListener(HandleCancelButton);
        startButton.onClick.AddListener(HandleStartButton);
        boardDropdown.onValueChanged.AddListener(HandleBoardDropdown);
        mustFillAllTilesToggle.onValueChanged.AddListener(HandleMustFillAllTilesToggle);
        teamButton.onClick.AddListener(HandleTeamButton);
    }

    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        cancelButton.interactable = enable;
        startButton.interactable = enable;
        
        hostTeam = 1;
        mustFillAllTiles = true;
        UpdateTeamButton();
        PopulateBoardDropdown();
    }

    void UpdateTeamButton()
    {
        if (hostTeam == 1)
        {
            teamButton.image.color = Color.red;
        }
        else
        {
            teamButton.image.color = Color.blue;
        }
    }
    void HandleCancelButton()
    {
        OnCancelButton?.Invoke();
    }

    void HandleStartButton()
    {
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
        List<string> options = new List<string>();
        foreach (BoardDef board in boardDefs)
        {
            options.Add(board.name);
        }
        boardDropdown.ClearOptions();
        boardDropdown.AddOptions(options);
        HandleBoardDropdown(0);
        boardDropdown.RefreshShownValue();
    }

    void HandleBoardDropdown(int index)
    {
        selectedBoardDef = boardDefs[index];
    }

    void HandleTeamButton()
    {
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
    }
}