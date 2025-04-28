using System;
using TMPro;
using UnityEngine;

public class GuiGameOverModal : MonoBehaviour
{
    public TextMeshProUGUI text;

    public void Initialize(uint endState, Team team)
    {
        string message = "";
        switch (endState)
        {
            case 0:
                message = "Game ended with a tie";
                break;
            case 1:
                message = team == Team.RED ? "Red team is victorious! You prevailed!" : "Blue team is victorious! You were defeated!";
                break;
            case 2:
                message = team == Team.BLUE ? "Blue team is victorious! You prevailed!" : "Red team is victorious! You were defeated!";
                break;
            case 3:
                message = "Game is in progress. you shouldn't see this";
                Debug.LogError("Game is in progress. you shouldn't see this!");
                break;
            case 4:
                message = "The game has ended inconclusively";
                break;
            default:
                throw new ArgumentOutOfRangeException();
                break;
        }
        text.text = message;
    }
}
