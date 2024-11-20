using System;
using System.Collections.Generic;
using UnityEngine;

public class GuiGame : MenuElement
{
    //public GamePhase gamePhase;
    public GuiPawnSetup pawnSetup;
    public GameObject waiting;
    
    void Start()
    {
    }

    public void Initialize(SetupParameters setupParameters)
    {
        Debug.Log("GuiGame initialized");
        pawnSetup.gameObject.SetActive(true);
        pawnSetup.Initialize(setupParameters);
        waiting.gameObject.SetActive(false);
    }

    public void OnSetupSubmittedResponse(Response<bool> response)
    {
        if (response.data)
        {
            pawnSetup.gameObject.SetActive(false);
            waiting.gameObject.SetActive(true);
        }
    }

    public void OnSetupFinishedResponse(Response<SInitialGameState> response)
    {
        waiting.gameObject.SetActive(false);
    }
}
