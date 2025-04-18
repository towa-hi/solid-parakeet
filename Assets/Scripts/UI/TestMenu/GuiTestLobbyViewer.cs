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
    public Button enterGameButton;
    
    public event Action OnBackButton;
    public event Action OnDeleteButton;
    public event Action OnRefreshButton;
    public event Action OnStartButton;
    public event Action OnEnterGameButton;
    
    void Start()
    {
        backButton.onClick.AddListener(HandleBackButton);
        deleteButton.onClick.AddListener(HandleDeleteButton);
        refreshButton.onClick.AddListener(HandleRefreshButton);
        startButton.onClick.AddListener(HandleStartButton);
        enterGameButton.onClick.AddListener(HandleEnterGameButton);
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
        startButton.interactable = false;
        enterGameButton.interactable = false;
        List<string> problems = new List<string>();
        bool lobbyStartable = true;
        if (maybeLobby.HasValue)
        {
            Lobby lobby = maybeLobby.Value;
            if (string.IsNullOrEmpty(lobby.host_address))
            {
                problems.Add("lobby.host_address is empty");
                lobbyStartable = false;
            }
            if (string.IsNullOrEmpty(lobby.guest_address))
            {
                problems.Add("lobby.guest_address is empty");
                lobbyStartable = false;
            }

            if (lobby.game_end_state != 3)
            {
                problems.Add($"lobby.game_end_state is {lobby.game_end_state}");
                lobbyStartable = false;
            }
        }
        else
        {
            problems.Add("lobby not found...");
            lobbyStartable = false;
        }
        if (problems.Count > 0)
        {
            string problemsString = string.Join("", problems);
            statusText.text = problemsString;
        }
        if (lobbyStartable)
        {
            startButton.interactable = true;
            statusText.text = "lobby startable";
        }
        startButton.interactable = lobbyStartable;
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

    void HandleEnterGameButton()
    {
        OnEnterGameButton?.Invoke();
    }
}
