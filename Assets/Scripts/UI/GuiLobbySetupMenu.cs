using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbySetupMenu : MenuElement
{
    public Button cancelButton;
    public Button startButton;
    public TMP_Dropdown boardDropdown;
    public event Action OnCancelButton;
    public event Action<LobbyParameters> OnStartButton;

    public BoardDef[] boardDefs;
    public BoardDef selectedBoardDef;
    
    public int hostPlayer;
    public bool mustFillAllTiles;
    void Start()
    {
        cancelButton.onClick.AddListener(HandleCancelButton);
        startButton.onClick.AddListener(HandleStartButton);
        boardDropdown.onValueChanged.AddListener(HandleBoardDropdown);
    }

    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        cancelButton.interactable = enable;
        startButton.interactable = enable;
        
        hostPlayer = 1;
        mustFillAllTiles = true;
        
        PopulateBoardDropdown();
    }


    void HandleCancelButton()
    {
        OnCancelButton?.Invoke();
    }

    void HandleStartButton()
    {
        LobbyParameters lobbyParameters = new LobbyParameters
        {
            hostPlayer = hostPlayer,
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
}