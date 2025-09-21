using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiGameOverModal : MonoBehaviour
{
    public TextMeshProUGUI text;
    public Button closeButton;

    Action onClose;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => onClose?.Invoke());
        }
    }

    public void Initialize(uint endState, Team team, Action onCloseCallback)
    {
        onClose = onCloseCallback;
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
        }
        text.text = message;
    }
}
