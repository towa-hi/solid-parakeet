using System;
using UnityEngine;

public class GuiGame : MenuElement
{
    //public GamePhase gamePhase;
    public GuiPawnSetup pawnSetup;

    void Start()
    {
        //GameManager.instance.boardManager.OnPhaseChanged += OnPhaseChanged;
    }

    public void Initialize(SetupParameters setupParameters)
    {
        pawnSetup.gameObject.SetActive(true);
        pawnSetup.Initialize(setupParameters);
    }
    void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        switch (oldPhase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                pawnSetup.gameObject.SetActive(false);
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(oldPhase), oldPhase, null);
        }
        switch (newPhase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                pawnSetup.gameObject.SetActive(true);
                pawnSetup.Initialize(GameManager.instance.boardManager.setupParameters);
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newPhase), newPhase, null);
        }
    }
}
