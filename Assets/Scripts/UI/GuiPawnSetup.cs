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
        GameManager.instance.boardManager.OnSetupPawnViewsChanged += OnSetupPawnViewsChanged;

    }
    
    public void Initialize(SetupParameters setupParameters)
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
        // TODO: submit pieces 
        Debug.Log("Submitting pieces");
        GameManager.instance.boardManager.StartDemoGame();
    }
    
    void OnSetupPawnViewsChanged(List<SetupPawnView> setupPawnViews)
    {
        pawnSetupList.UpdateList(setupPawnViews);
    }
}
