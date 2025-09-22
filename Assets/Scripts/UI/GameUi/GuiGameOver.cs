using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiGameOver : GameElement
{
    public TextMeshProUGUI messageText;
    public Button returnButton;

    public event Action OnReturnClicked;

    void Start()
    {
        returnButton.onClick.AddListener(OnReturnClicked.Invoke);
    }

    // New system: drive from client mode change
    void HandleClientModeChanged(ClientMode mode, GameNetworkState net, LocalUiState ui)
    {
        bool isFinished = mode == ClientMode.Finished || mode == ClientMode.Aborted;
        ShowElement(isFinished);
        if (!isFinished) return;
        string msg = BuildMessage(net);
        if (messageText != null) messageText.text = msg;
    }

    string BuildMessage(GameNetworkState netState)
    {
        if (netState.lobbyInfo.phase == Phase.Aborted)
        {
            return "The game has ended inconclusively";
        }
        // Phase.Finished
        Team hostTeam = netState.lobbyParameters.host_team;
        Team guestTeam = hostTeam == Team.RED ? Team.BLUE : Team.RED;
        switch (netState.lobbyInfo.subphase)
        {
            case Subphase.None:
                return "Game ended with a tie";
            case Subphase.Host:
            {
                bool redWon = hostTeam == Team.RED;
                return redWon
                    ? "Red team is victorious! You prevailed!"
                    : "Blue team is victorious! You were defeated!";
            }
            case Subphase.Guest:
            {
                bool redWon = guestTeam == Team.RED;
                return redWon
                    ? "Red team is victorious! You prevailed!"
                    : "Blue team is victorious! You were defeated!";
            }
            default:
                return "The game has ended";
        }
    }

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        HandleClientModeChanged(ModeDecider.DecideClientMode(net, default), net, ui);
    }

    public void AttachSubscriptions()
    {
        ViewEventBus.OnClientModeChanged += HandleClientModeChanged;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnClientModeChanged -= HandleClientModeChanged;
    }

}


