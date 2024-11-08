using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiPawnSetupControls : MonoBehaviour
{

    public event Action OnUndoButton;
    public event Action OnStartButton;
    
    public Button undoButton;
    public Button startButton;

    void Start()
    {
        undoButton.onClick.AddListener(HandleUndoButton);
        startButton.onClick.AddListener(HandleStartButton);
    }

    void HandleUndoButton()
    {
        OnUndoButton?.Invoke();
    }

    void HandleStartButton()
    {
        OnStartButton?.Invoke();
    }
}
