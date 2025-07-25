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
    
    void Start()
    {
        Debug.Log("GuiGame.Start()");
    }
    
    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        setup.PhaseStateChanged(changes);
        movement.PhaseStateChanged(changes);
    }

    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        if (show)
        {
            AudioManager.instance.PlayMusic(MusicTrack.BATTLE_MUSIC);
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
