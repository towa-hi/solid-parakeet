using System;
using System.Collections.Generic;
using UnityEngine;

public class GuiPawnSetup : MonoBehaviour
{
    public GuiPawnSetupList pawnSetupList;
    public GuiPawnSetupControls pawnSetupControls;

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
                gameObject.SetActive(true);
                pawnSetupList.Initialize(this, setupPhase.setupParameters);
                pawnSetupControls.Initialize(this);
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
                gameObject.SetActive(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));

        }
    }

    public void OnAutoSetupButton()
    {
        if (GameManager.instance.boardManager.currentPhase is SetupPhase setupPhase)
        {
            setupPhase.OnAutoSetup();
        }
    }

    public void OnSetupPawnDefSelected(PawnDef pawnDef)
    {
        if (GameManager.instance.boardManager.currentPhase is SetupPhase setupPhase)
        {
            setupPhase.OnPawnDefSelected(pawnDef);
        }
    }

    public void OnSubmitButton()
    {
        if (GameManager.instance.boardManager.currentPhase is SetupPhase setupPhase)
        {
            setupPhase.OnSubmitSetup();
        }
    }
}
