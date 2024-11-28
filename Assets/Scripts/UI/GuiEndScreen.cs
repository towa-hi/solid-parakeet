using System;
using TMPro;
using UnityEngine;

public class GuiEndScreen : MonoBehaviour
{
    public TextMeshProUGUI winnerText;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.instance.boardManager.OnPhaseChanged += OnPhaseChanged;
    }

    void OnPhaseChanged(IPhase currentPhase)
    {
        switch (currentPhase)
        {
            case UninitializedPhase uninitializedPhase:
                gameObject.SetActive(false);
                break;
            case SetupPhase setupPhase:
                gameObject.SetActive(false);
                break;
            case WaitingPhase waitingPhase:
                gameObject.SetActive(false);
                break;
            case MovePhase movePhase:
                gameObject.SetActive(false);
                break;
            case ResolvePhase resolvePhase:
                gameObject.SetActive(false);
                break;
            case EndPhase endPhase:
                gameObject.SetActive(true);
                Initialize((int)endPhase.winner);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));

        }
    }


    public void Initialize(int winnerPlayer)
    {
        if (winnerPlayer == 0)
        {
            Debug.Log("The game isn't over yet!");
        }
        else if (winnerPlayer == 1)
        {
            Debug.Log("Red player won!");
        }
        else if (winnerPlayer == 2)
        {
            Debug.Log("Blue player won!");
        }
        else if (winnerPlayer == 3)
        {
            Debug.Log("Both players lost!");
        }
    }
}
