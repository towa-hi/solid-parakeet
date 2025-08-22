using System;
using Contract;
using UnityEngine;
using UnityEngine.UI;

public class GuiResolve : GameElement
{
    public Button menuButton;
    public Button prevButton;
    public Button nextButton;
    public Button skipButton;

    public Action OnMenuButton;
    public Action OnPrevButton;
    public Action OnNextButton;
    public Action OnSkipButton;

    void Start()
    {
        menuButton.onClick.AddListener(HandleMenuButton);
        prevButton.onClick.AddListener(HandlePrevButton);
        nextButton.onClick.AddListener(HandleNextButton);
        skipButton.onClick.AddListener(HandleSkipButton);
    }

    void Initialize()
    {
        
    }
    
    // no phasestatechanged listen here

    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        bool? setShowElement = null;
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            switch (cachedNetState.lobbyInfo.phase)
            {
                case Phase.MoveProve:
                case Phase.RankProve:
                    setShowElement = true;
                    break;
                case Phase.SetupCommit:
                case Phase.MoveCommit:
                case Phase.Finished:
                case Phase.Aborted:
                    setShowElement = false;
                    break;
                default:
                    setShowElement = false;
                    break;
            }
        }
        if (setShowElement is bool show)
        {
            ShowElement(show);
        }
    }

    void HandleMenuButton()
    {
        //just play a mid button click and invoke the event in these functions
        AudioManager.PlayMidButtonClick();
        OnMenuButton?.Invoke();
    }

    void HandlePrevButton()
    {
        AudioManager.PlayMidButtonClick();
        OnPrevButton?.Invoke();
    }

    void HandleNextButton()
    {
        AudioManager.PlayMidButtonClick();
        OnNextButton?.Invoke();
    }

    void HandleSkipButton()
    {
        AudioManager.PlayMidButtonClick();
        OnSkipButton?.Invoke();
    }
}
