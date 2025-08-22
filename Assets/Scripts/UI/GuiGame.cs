using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiGame : MenuElement
{
    public GuiSetup setup;
    public GuiMovement movement;
    public GuiResolve resolve;
    
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
        // Centralize which Game GUI panel is visible based on the active phase type
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            PhaseBase phase = netStateUpdated.phase;
            bool showSetup = phase is SetupCommitPhase;
            bool showMovement = phase is MoveCommitPhase || phase is MoveProvePhase || phase is RankProvePhase;
            bool showResolve = phase is ResolvePhase;
            if (setup.gameObject.activeSelf != showSetup) { setup.ShowElement(showSetup); }
            if (movement.gameObject.activeSelf != showMovement) { movement.ShowElement(showMovement); }
            if (resolve.gameObject.activeSelf != showResolve) { resolve.ShowElement(showResolve); }
        }
        // Forward phase updates for content/data changes
        setup.PhaseStateChanged(changes);
        movement.PhaseStateChanged(changes);
        resolve.PhaseStateChanged(changes);
    }

    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        isUpdating = show;
        if (show)
        {
            GameManager.instance.cameraManager.enableCameraMovement = true;
            GameManager.instance.cameraManager.MoveCameraTo(Area.BOARD, false);
            setup.ShowElement(false);
            movement.ShowElement(false);
            resolve.ShowElement(false);
            Debug.Log("GuiGame.ShowElement(true): hiding subpanels by default (resolve hidden)");
        }
    }

    public override void Refresh()
    {
        
    }
}
