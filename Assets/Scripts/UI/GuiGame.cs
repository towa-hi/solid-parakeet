using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiGame : TestGuiElement
{
    public GuiSetup setup;
    public GuiMovement movement;
    
    public CameraAnchor boardAnchor;
    
    void Start()
    {
        Debug.Log("GuiGame.Start()");
    }
    
    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        Debug.Log($"GuiGame.SetIsEnabled(inIsEnabled: {inIsEnabled}, networkUpdated: {networkUpdated})");
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        if (isEnabled)
        {
            AudioManager.instance.PlayMusic(MusicTrack.BATTLE_MUSIC);
            GameManager.instance.cameraManager.enableCameraMovement = true;
            GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
            setup.ShowElement(false);
            movement.ShowElement(false);
        }
    }

    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        setup.PhaseStateChanged(changes);
        movement.PhaseStateChanged(changes);
    }
}
