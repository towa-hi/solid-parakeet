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


    public void AttachSubscriptions()
    {
        
    }

    public void DetachSubscriptions()
    {
        
    }

    public override void OnClientModeChanged(GameSnapshot snapshot)
    {
        Reset(snapshot.Net);
    }

    public override void Reset(GameNetworkState net)
    {
        string msg = BuildMessage(net);
        messageText.text = msg;
    }

    public override void Refresh(GameSnapshot snapshot)
    {

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


}


