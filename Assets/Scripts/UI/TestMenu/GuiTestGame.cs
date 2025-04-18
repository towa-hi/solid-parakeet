using System;
using Contract;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class GuiTestGame : TestGuiElement
{
    public GuiTestSetup setup;
    public GuiTestMovement movement;
    
    public GameObject blocker;
    public TextMeshProUGUI blockerText;
    
    public CameraAnchor boardAnchor;
    public GameElement currentElement;
    
    void Start()
    {
        GameManager.instance.testBoardManager.OnPhaseChanged += OnPhaseChanged;
        GameManager.instance.testBoardManager.OnStateChanged += OnStateChanged;
        setup.SetIsEnabled(false);
        movement.SetIsEnabled(false);
    }

    public override void Initialize()
    {
        // TODO: make this not jank
        GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
        
    }

    public override void Refresh()
    {
        
    }

    void SetCurrentElement(GameElement element)
    {
        currentElement?.SetIsEnabled(false);
        currentElement = element;
        currentElement.SetIsEnabled(true);
        currentElement.Initialize(GameManager.instance.testBoardManager);
    }
    
    void OnPhaseChanged(ITestPhase obj)
    {
        switch (obj)
        {
            case SetupTestPhase setupTestPhase:
                SetCurrentElement(setup);
                break;
            case MovementTestPhase movementTestPhase:
                SetCurrentElement(movement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(obj));

        }
    }

    void OnStateChanged(TestBoardManager boardManager)
    {
        switch (boardManager.currentPhase)
        {
            case SetupTestPhase setupTestPhase:
                setup.Refresh(boardManager);
                break;
            case MovementTestPhase movementTestPhase:
                movement.Refresh(boardManager);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(boardManager.currentPhase));
        }
    }

}
