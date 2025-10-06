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
            OnBackButton?.Invoke();
        });
        deleteButton.onClick.AddListener(() =>
        {
            OnDeleteButton?.Invoke();
        });
        refreshButton.onClick.AddListener(() =>
        {
            OnRefreshButton?.Invoke();
        });
        startButton.onClick.AddListener(() =>
        {
            OnStartButton?.Invoke();
        });
    }
    
    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
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
            if (lobbyInfo.phase == Phase.Aborted)
            {
                string winner = lobbyInfo.subphase switch
                {
                    Subphase.Guest when lobbyParameters.host_team == Team.RED => "guest (blue)",
                    Subphase.Guest => "guest (red)",
                    Subphase.Host when lobbyParameters.host_team == Team.RED => "host (red)",
                    Subphase.Host => "host (blue)",
                    Subphase.None => "tie",
                    _ => "inconclusive",
                };
                problems.Add($"lobby is aborted. winner : {winner}");
                lobbyStartable = false;
            }

            if (lobbyInfo.phase == Phase.Finished)
            {

                string winner = lobbyInfo.subphase switch
                {
                    Subphase.Guest when lobbyParameters.host_team == Team.RED => "guest (blue)",
                    Subphase.Guest => "guest (red)",
                    Subphase.Host when lobbyParameters.host_team == Team.RED => "host (red)",
                    Subphase.Host => "host (blue)",
                    Subphase.None => "tie",
                    _ => "inconclusive",
                };

                problems.Add($"lobby has ended. winner: {winner}");
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
