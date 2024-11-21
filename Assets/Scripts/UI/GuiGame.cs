using System;
using System.Collections.Generic;
using UnityEngine;

public class GuiGame : MenuElement
{
    //public GamePhase gamePhase;
    public GuiPawnSetup pawnSetup;
    public GuiMoveWaiting waiting;
    public GuiMoveControls moveControls;
    
    void Start()
    {
        moveControls.OnMoveSubmitButton += OnMoveSubmitButton;
    }

    public void Initialize(SetupParameters setupParameters)
    {
        Debug.Log("GuiGame initialized");
        pawnSetup.gameObject.SetActive(true);
        pawnSetup.Initialize(setupParameters);
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

    public void OnMoveSubmitButton()
    {
        GameManager.instance.OnMoveSubmitButton();
    }

    public void OnMoveResponse(Response<bool> response)
    {
        moveControls.OnMoveResponse(response);
        // TODO: tell the user that the move is submitted and we're waiting for a response
    }
}
