using System.Collections.Generic;
using UnityEngine;

public class GuiPawnSetup : MonoBehaviour
{
    public GuiPawnSetupList pawnSetupList;
    public GuiPawnSetupControls pawnSetupControls;

    void Start()
    {
        pawnSetupControls.OnUndoButton += OnUndoButton;
        pawnSetupControls.OnStartButton += OnStartButton;
        pawnSetupControls.OnAutoSetupButton += OnAutoSetupButton;
        pawnSetupControls.OnSubmitButton += OnSubmitButton;

    }
    
    public void Initialize(SSetupParameters setupParameters)
    {
        //Debug.Log("GuiPawnSetup Initialization");
        pawnSetupList.Initialize(setupParameters);
        pawnSetupControls.Initialize();
    }

    void OnUndoButton()
    {
        
    }

    void OnStartButton()
    {
        
    }

    void OnAutoSetupButton()
    {
        GameManager.instance.boardManager.AutoSetup(GameManager.instance.boardManager.player);
    }

    void OnSubmitButton()
    {
        GameManager.instance.boardManager.SubmitSetup();
    }
}
