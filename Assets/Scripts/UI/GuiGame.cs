using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiGame : MenuElement
{
    public GuiSetup setup;
    public GuiMovement movement;
    
    public CameraAnchor boardAnchor;
    bool isUpdating;

    public event Action EscapePressed;
    
    void Start()
    {
        Debug.Log("GuiGame.Start()");
        isUpdating = false;
    }

    void Update()
    {
        if (!isUpdating) return;
        if (!Application.isFocused) return;
        bool pressed = Globals.InputActions.Game.Escape.WasPressedThisFrame();
        if (pressed)
        {
            EscapePressed?.Invoke();
        }
    }
    
    
    
    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        setup.PhaseStateChanged(changes);
        movement.PhaseStateChanged(changes);
    }

    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        isUpdating = show;
        if (show)
        {
            GameManager.instance.cameraManager.enableCameraMovement = true;
            GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
            setup.ShowElement(false);
            movement.ShowElement(false);
        }
    }

    public override void Refresh()
    {
        
    }
}
