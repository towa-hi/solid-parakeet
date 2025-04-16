using System;
using Contract;
using JetBrains.Annotations;
using UnityEngine;

public class GuiTestGame : TestGuiElement
{
    public GuiTestSetup setup;
    public CameraAnchor boardAnchor;

    void Start()
    {
        GameManager.instance.testBoardManager.OnPhaseChanged += OnPhaseChanged;
        GameManager.instance.testBoardManager.OnStateChanged += OnStateChanged;
    }
    
    public override void Initialize()
    {
        // TODO: make this not jank
        GameManager.instance.cameraManager.MoveCameraTo(boardAnchor, false);
    }

    void OnPhaseChanged(ITestPhase obj)
    {
        switch (obj)
        {
            case SetupTestPhase setupTestPhase:
                setup.Initialize();
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
            default:
                throw new ArgumentOutOfRangeException(nameof(boardManager.currentPhase));
        }
    }

}
