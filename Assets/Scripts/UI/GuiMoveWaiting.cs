using System;
using TMPro;
using UnityEngine;

public class GuiMoveWaiting : MonoBehaviour
{

    public TextMeshProUGUI header;
    public string waitingForServer = "Waiting for other player...";
    
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
                gameObject.SetActive(true);
                break;
            case MovePhase movePhase:
                gameObject.SetActive(false);
                break;
            case ResolvePhase resolvePhase:
                gameObject.SetActive(false);
                break;
            case EndPhase endPhase:
                gameObject.SetActive(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));

        }
    }
}
