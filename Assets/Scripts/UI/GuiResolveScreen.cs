using System;
using UnityEngine;

public class GuiResolveScreen : MonoBehaviour
{
    public GameObject battleScreen;
    public Arena arena;

    void Start()
    {
        GameManager.instance.boardManager.OnPhaseChanged += OnPhaseChanged;
    }

    void OnPhaseChanged(IPhase currentPhase)
    {
        battleScreen.SetActive(false);
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
                gameObject.SetActive(true);
                break;
            case EndPhase endPhase:
                gameObject.SetActive(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));

        }
    }

    public void Initialize(SPawn redPawn, SPawn bluePawn, bool redDies, bool blueDies, Action onFinish)
    {
        battleScreen.SetActive(true);
        arena.Initialize(this, redPawn, bluePawn, redDies, blueDies, onFinish);
    }

    public void HideBattleScreen()
    {
        battleScreen.SetActive(false);
    }
}
