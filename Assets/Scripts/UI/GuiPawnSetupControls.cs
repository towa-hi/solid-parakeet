using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiPawnSetupControls : MonoBehaviour
{

    public event Action OnUndoButton;
    public event Action OnStartButton;
    public event Action OnAutoSetupButton;
    public event Action OnSubmitButton;
    
    public Button undoButton;
    public Button startButton;
    public Button autoSetupButton;
    public Button submitButton;
    
    void Awake()
    {
        undoButton.onClick.AddListener(HandleUndoButton);
        startButton.onClick.AddListener(HandleStartButton);
        autoSetupButton.onClick.AddListener(HandleAutoSetupButton);
        submitButton.onClick.AddListener(HandleSubmitButton);
    }

    public void Initialize()
    {
        
    }
    
    void HandleUndoButton()
    {
        OnUndoButton?.Invoke();
    }

    void HandleStartButton()
    {
        OnStartButton?.Invoke();
    }

    void HandleAutoSetupButton()
    {
        OnAutoSetupButton?.Invoke();
    }

    void HandleSubmitButton()
    {
        OnSubmitButton?.Invoke();
    }
}
