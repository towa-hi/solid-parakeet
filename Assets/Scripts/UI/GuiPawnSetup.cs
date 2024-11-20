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
        if (GameManager.instance.boardManager.IsSetupValid(GameManager.instance.boardManager.player))
        {
            Debug.Log("Submitting pieces");
            GameManager.instance.client.SendSetupSubmissionRequest(GameManager.instance.boardManager.GetSPawnListForSetup());
            //GameManager.instance.boardManager.StartDemoGame();
        }
    }
}
