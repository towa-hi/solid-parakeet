using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiPawnSetupControls : MonoBehaviour
{

    public event Action OnUndoButton;
    public event Action OnStartButton;
    public event Action OnAutoSetupButton;
    
    
    public Button undoButton;
    public Button startButton;
    public Button autoSetupButton;
    
    void Awake()
    {
        undoButton.onClick.AddListener(HandleUndoButton);
        startButton.onClick.AddListener(HandleStartButton);
        autoSetupButton.onClick.AddListener(HandleAutoSetupButton);
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
}
