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
    
    public BoardManager bm;
    
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
            bm = GameManager.instance.boardManager;
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
    
    public void SetCurrentElement(GameElement element, GameNetworkState networkState)
    {
        currentElement?.SetIsEnabled(false);
        currentElement = element;
        currentElement.SetIsEnabled(true);
        currentElement.Initialize(GameManager.instance.boardManager, networkState);
    }

}
