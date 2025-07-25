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
            AudioManager.instance.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        deleteButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnDeleteButton?.Invoke();
        });
        refreshButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnRefreshButton?.Invoke();
        });
        startButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnStartButton?.Invoke();
        });
    }

    void OnNetworkStateUpdated()
    {
        if (!gameObject.activeSelf) return;
        Refresh();
        
    }
    
    public override void Refresh()
    {
        lobbyView.Refresh(StellarManager.networkState.lobbyInfo);
        startButton.interactable = false;
        List<string> problems = new List<string>();
        bool lobbyStartable = true;
        if (StellarManager.networkState.lobbyInfo.HasValue)
        {
            LobbyInfo lobbyInfo = StellarManager.networkState.lobbyInfo.Value;
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
            if (lobbyInfo.phase != Phase.SetupCommit && !CacheManager.RankProofsCacheExists(StellarManager.networkState.address, lobbyInfo.index))
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
