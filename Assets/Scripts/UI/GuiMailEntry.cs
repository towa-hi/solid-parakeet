using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiMailEntry : MonoBehaviour
{
    public GameObject container;
    public Image senderBackground;
    public TextMeshProUGUI senderText;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI timeText;
    
    public void Initialize(Mail mail, Lobby lobby)
    {
        // Team team = Team.NONE;
        // string sender = "Unknown";
        // if (lobby.host_state.user_address == mail.sender)
        // {
        //     team = (Team)lobby.host_state.team;
        // }
        // else if (lobby.guest_state.user_address == mail.sender)
        // {
        //     team = (Team)lobby.guest_state.team;
        // }
        // Color color = new Color(1, 1, 1, 0.3f);
        // switch (team)
        // {
        //     case Team.NONE:
        //         break;
        //     case Team.RED:
        //         color = new Color(1, 0, 0, 0.3f);
        //         sender = "Red";
        //         break;
        //     case Team.BLUE:
        //         color = new Color(0, 0, 1, 0.3f);
        //         sender = "Blue";
        //         break;
        //     default:
        //         throw new ArgumentOutOfRangeException();
        // }
        // senderBackground.color = color;
        // senderText.text = sender;
        // messageText.text = mail.message;
        // timeText.text = mail.sent_ledger.ToString();
    }

    public void Display(bool display)
    {
        container.SetActive(display);
    }
}
