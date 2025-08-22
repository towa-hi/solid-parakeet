using System;
using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyViewer : MenuElement
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
        backButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        deleteButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnDeleteButton?.Invoke();
        });
        refreshButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnRefreshButton?.Invoke();
        });
        startButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnStartButton?.Invoke();
        });
    }
    
    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        StellarManager.SetPolling(show);
    }

    public override void Refresh()
    {
        lobbyView.Refresh(StellarManager.networkState.lobbyInfo);
        startButton.interactable = false;
        List<string> problems = new();
        bool lobbyStartable = true;
        if (StellarManager.networkState.lobbyInfo is LobbyInfo lobbyInfo && StellarManager.networkState.lobbyParameters is LobbyParameters lobbyParameters)
        {
            if (lobbyInfo.host_address == null)
            {
                problems.Add("lobby.host_address is empty");
                lobbyStartable = false;
            }
            if (lobbyInfo.guest_address == null)
            {
                problems.Add("lobby.guest_address is empty");
                lobbyStartable = false;
            }
            if (lobbyParameters.security_mode && lobbyInfo.phase != Phase.SetupCommit && !CacheManager.RankProofsCacheExists(StellarManager.networkState.address, lobbyInfo.index))
            {
                // TODO: more thurough cache check here
                statusText.text = "this client does not have the required cached data to play this lobby";
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
}
