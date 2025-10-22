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


    public override void AttachSubscriptions()
    {
        
    }

    public override void DetachSubscriptions()
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
        string msg = BuildMessage(snapshot.Net);
        messageText.text = msg;
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
        Team myTeam = netState.userTeam;
        Team winnerTeam = Team.NONE;
        switch (netState.lobbyInfo.subphase)
        {
            case Subphase.None:
            {winnerTeam = Team.NONE; break;}
            case Subphase.Host:
            {winnerTeam = hostTeam; break;}
            case Subphase.Guest:
            {winnerTeam = guestTeam; break;}
        }
        switch (winnerTeam)
        {
            case Team.NONE:
            {
                return "Game ended with a tie";
            }
            case Team.RED:
            {
                if (myTeam == Team.RED)
                {
                    return "Red team is victorious! You prevailed!";
                }
                else
                {
                    return "Red team is victorious! You were defeated!";
                }
            }
            case Team.BLUE:
            {
                if (myTeam == Team.BLUE)
                {
                    return "Blue team is victorious! You prevailed!";
                }
                else
                {
                    return "Blue team is victorious! You were defeated!";
                }
            }
            default:
            {
                throw new Exception("Invalid winner team");
            }
        }
    }


}


