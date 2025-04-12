using System;
using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestLobbyViewer : TestGuiElement
{
    public GuiLobbyView lobbyView;
    
    public TextMeshProUGUI statusText;
    public Button backButton;
    public Button deleteButton;
    public Button refreshButton;
    public Button startButton;

    public event Action OnBackButton;
    public event Action OnDeleteButton;
    public event Action OnRefreshButton;
    public event Action OnStartButton;
    
    void Start()
    {
        backButton.onClick.AddListener(HandleBackButton);
        deleteButton.onClick.AddListener(HandleDeleteButton);
        refreshButton.onClick.AddListener(HandleRefreshButton);
        startButton.onClick.AddListener(HandleStartButton);
        StellarManagerTest.OnCurrentLobbyUpdated += OnLobbyUpdate;
    }
    
    public override void Initialize()
    {
        Refresh();
    }

    void OnLobbyUpdate(Lobby? lobby)
    {
        Refresh();
    }
    
    public override void Refresh()
    {
        Lobby? maybeLobby = StellarManagerTest.currentLobby;
        lobbyView.Refresh(maybeLobby);
        
        string status = "Lobby not found";
        startButton.interactable = false;
        if (maybeLobby.HasValue)
        {
            if (!string.IsNullOrEmpty(maybeLobby.Value.guest_address))
            {
                status = "Can start game";
                startButton.interactable = true;
            }
            else
            {
                status = "Waiting for a guest...";
            }
        }
        
        statusText.text = status;
    }

    void HandleBackButton()
    {
        OnBackButton?.Invoke();
    }

    void HandleDeleteButton()
    {
        OnDeleteButton?.Invoke();
    }
    
    void HandleRefreshButton()
    {
        OnRefreshButton?.Invoke();
    }

    void HandleStartButton()
    {
        OnStartButton?.Invoke();
    }
}
