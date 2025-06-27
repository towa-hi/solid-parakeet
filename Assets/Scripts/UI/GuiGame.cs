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
    
    public GameElement currentElement;
    
    
    void Start()
    {
        setup.ShowElement(false);
        movement.ShowElement(false);
        GameManager.instance.boardManager.OnClientGameStateChanged += OnClientGameStateChanged;
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

    void OnClientGameStateChanged(IPhase currentPhase, bool phaseChanged)
    {
        GameElement newElement;
        switch (currentPhase)
        {
            case SetupCommitPhase setupCommitPhase:
            case SetupProvePhase setupProvePhase:
                newElement = setup;
                break;
            case MoveCommitPhase moveCommitPhase:
            case MoveProvePhase moveProvePhase:
            case RankProvePhase rankProvePhase:
                newElement = movement;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));
        }
        currentElement = newElement;
        setup.Refresh(currentPhase);
        movement.Refresh(currentPhase);
    }
}
