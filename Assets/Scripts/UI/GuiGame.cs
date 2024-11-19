using System;
using System.Collections.Generic;
using UnityEngine;

public class GuiGame : MenuElement
{
    //public GamePhase gamePhase;
    public GuiPawnSetup pawnSetup;
    
    void Start()
    {
    }

    public void Initialize(SetupParameters setupParameters)
    {
        Debug.Log("GuiGame initialized");
        pawnSetup.gameObject.SetActive(true);
        pawnSetup.Initialize(setupParameters);
    }
}
