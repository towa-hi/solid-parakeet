using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiTestGame : TestGuiElement
{
    public Chatbox chatbox;
    
    public GuiTestSetup setup;
    public GuiTestMovement movement;
    
    public CameraAnchor boardAnchor;
    
    public GameElement currentElement;
    
    public TestBoardManager bm;
    
    void Start()
    {
        setup.SetIsEnabled(false);
        movement.SetIsEnabled(false);
    }
    
    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        // TODO: make this not jank
        if (isEnabled)
        {
            AudioManager.instance.PlayMusic(MusicTrack.BATTLE_MUSIC);
            GameManager.instance.cameraManager.enableCameraMovement = true;
            GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
            bm = GameManager.instance.testBoardManager;
            bm.StartBoardManager(networkUpdated);
            if (networkUpdated)
            {
                chatbox.gameObject.SetActive(true);
                chatbox.Initialize(true, bm.lobbyId);
            }
            else
            {
                chatbox.gameObject.SetActive(false);
            }
        }
        
    }
    
    public void SetCurrentElement(GameElement element, Lobby lobby)
    {
        currentElement?.SetIsEnabled(false);
        currentElement = element;
        currentElement.SetIsEnabled(true);
        currentElement.Initialize(GameManager.instance.testBoardManager, lobby);
    }

}
