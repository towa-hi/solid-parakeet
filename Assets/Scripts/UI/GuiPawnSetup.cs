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
    }
    
    public void Initialize(SetupParameters setupParameters)
    {
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
        GameManager.instance.boardManager.AutoSetup();
    }
}
