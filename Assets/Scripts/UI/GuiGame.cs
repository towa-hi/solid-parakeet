using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiGame : TestGuiElement
{
    public Chatbox chatbox;
    
    public GuiSetup setup;
    public GuiMovement movement;
    
    public CameraAnchor boardAnchor;
    
    void Start()
    {
        setup.ShowElement(false);
        movement.ShowElement(false);
    }
    
    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        if (isEnabled)
        {
            AudioManager.instance.PlayMusic(MusicTrack.BATTLE_MUSIC);
            GameManager.instance.cameraManager.enableCameraMovement = true;
            GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
            // TODO: get rid of chatbox
            chatbox.gameObject.SetActive(false);
        }
    }

    public void PhaseStateChanged(IPhaseChangeSet changes)
    {
        setup.PhaseStateChanged(changes);
        movement.PhaseStateChanged(changes);
    }
}
