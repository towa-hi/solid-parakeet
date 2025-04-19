using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiTestGame : TestGuiElement
{
    public GuiTestSetup setup;
    public GuiTestMovement movement;
    
    public CameraAnchor boardAnchor;
    
    public GameElement currentElement;
    
    public TestBoardManager bm;
    
    void Start()
    {
        GameManager.instance.testBoardManager.OnPhaseChanged += OnPhaseChanged;
        GameManager.instance.testBoardManager.OnStateChanged += OnStateChanged;
        setup.SetIsEnabled(false);
        movement.SetIsEnabled(false);
    }
    
    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        // TODO: make this not jank
        if (isEnabled && networkUpdated)
        {
            GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
            bm = GameManager.instance.testBoardManager;
            bm.StartBoardManager(networkUpdated);
        }
        
    }
    
    void SetCurrentElement(GameElement element)
    {
        currentElement?.SetIsEnabled(false);
        currentElement = element;
        currentElement.SetIsEnabled(true);
        currentElement.Initialize(GameManager.instance.testBoardManager);
    }
    
    void OnPhaseChanged()
    {
        switch (bm.currentPhase)
        {
            case SetupTestPhase setupTestPhase:
                SetCurrentElement(setup);
                break;
            case MovementTestPhase movementTestPhase:
                SetCurrentElement(movement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(bm.currentPhase));

        }
    }

    void OnStateChanged()
    {
        switch (bm.currentPhase)
        {
            case SetupTestPhase setupTestPhase:
                setup.Refresh(bm);
                break;
            case MovementTestPhase movementTestPhase:
                movement.Refresh(bm);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(bm.currentPhase));
        }
    }

}
