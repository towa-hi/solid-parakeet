
using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiMoveControls : MonoBehaviour
{
    public Button moveSubmitButton;
    
    void Start()
    {
        GameManager.instance.boardManager.OnPhaseChanged += OnPhaseChanged;
        moveSubmitButton.onClick.AddListener(OnSubmitButton);
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
                gameObject.SetActive(true);
                moveSubmitButton.interactable = true;
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

    public void OnSubmitButton()
    {
        if (GameManager.instance.boardManager.currentPhase is MovePhase movePhase)
        {
            movePhase.OnSubmitMove();
        }
    }
}
