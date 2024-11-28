using System;
using System.Collections.Generic;
using UnityEngine;

public class GuiGame : MenuElement
{
    public GuiPawnSetup pawnSetup;
    public GuiMoveWaiting waiting;
    public GuiMoveControls moveControls;
    public GuiResolveScreen resolveScreen;
    public GuiEndScreen endScreen;
    
    void Start()
    {
        pawnSetup.gameObject.SetActive(false);
        waiting.gameObject.SetActive(false);
        moveControls.gameObject.SetActive(false);
        resolveScreen.gameObject.SetActive(false);
        endScreen.gameObject.SetActive(false);
        
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
            case GamePhase.WAITING:
                waiting.gameObject.SetActive(false);
                break;
            case GamePhase.MOVE:
                moveControls.gameObject.SetActive(false);
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                endScreen.gameObject.SetActive(false);
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
                //pawnSetup.Initialize(GameManager.instance.boardManager.serverSetupParameters);
                break;
            case GamePhase.WAITING:
                waiting.gameObject.SetActive(true);
                break;
            case GamePhase.MOVE:
                moveControls.gameObject.SetActive(true);
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                endScreen.gameObject.SetActive(true);
                endScreen.Initialize(GameManager.instance.boardManager.serverGameState.winnerPlayer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newPhase), newPhase, null);
        }
    }

    public void Initialize(SSetupParameters setupParameters)
    {
        Debug.Log("GuiGame initialized");
        pawnSetup.gameObject.SetActive(true);
        //pawnSetup.Initialize(setupParameters);
        waiting.gameObject.SetActive(false);
        moveControls.gameObject.SetActive(false);
    }

    public void OnSetupSubmittedResponse(Response<bool> response)
    {
        if (response.data)
        {
            pawnSetup.gameObject.SetActive(false);
            waiting.gameObject.SetActive(true);
            moveControls.gameObject.SetActive(false);
        }
    }

    public void OnSetupFinishedResponse(Response<SGameState> response)
    {
        waiting.gameObject.SetActive(false);
        moveControls.gameObject.SetActive(true);
    }


    
}
