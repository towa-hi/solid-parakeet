using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiPawnSetupControls : MonoBehaviour
{
    public GuiPawnSetup master;
    
    public Button autoSetupButton;
    public Button submitButton;
    

    public void Initialize(GuiPawnSetup inMaster)
    {
        master = inMaster;
        autoSetupButton.onClick.AddListener(master.OnAutoSetupButton);
        submitButton.onClick.AddListener(master.OnSubmitButton);
    }
}
