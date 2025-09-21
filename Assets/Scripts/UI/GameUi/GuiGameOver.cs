using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiGameOver : GameElement
{
    public TextMeshProUGUI messageText;
    public Button returnButton;

    MenuController menuController;

    void Start()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnClicked);
        }
    }

    public void SetMenuController(MenuController controller)
    {
        menuController = controller;
    }

    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState netState = netStateUpdated.phase.cachedNetState;
            bool isFinished = netState.lobbyInfo.phase is Phase.Finished or Phase.Aborted;
            ShowElement(isFinished);
            if (!isFinished)
            {
                return;
            }
            string msg = BuildMessage(netState);
            if (messageText != null)
            {
                messageText.text = msg;
            }
        }
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

    void OnReturnClicked()
    {
        if (menuController == null)
        {
            Debug.LogError("GuiGameOver: MenuController not set");
            return;
        }
        menuController.ExitGame();
    }
}


