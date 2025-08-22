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
        Debug.Log("GuiResolve.Start: wiring button listeners");
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
            // Switch based on the active PhaseBase instance, not the contract phase enum
            bool show = netStateUpdated.phase is ResolvePhase;
            setShowElement = show;
            Debug.Log($"GuiResolve.PhaseStateChanged: phase={netStateUpdated.phase.GetType().Name} -> ShowElement({show})");
        }
        // Visibility is handled centrally by GuiGame. Do not toggle here.
    }

    void HandleMenuButton()
    {
        //just play a mid button click and invoke the event in these functions
        Debug.Log("GuiResolve.HandleMenuButton");
        AudioManager.PlayMidButtonClick();
        OnMenuButton?.Invoke();
    }

    void HandlePrevButton()
    {
        Debug.Log("GuiResolve.HandlePrevButton");
        AudioManager.PlayMidButtonClick();
        OnPrevButton?.Invoke();
    }

    void HandleNextButton()
    {
        Debug.Log("GuiResolve.HandleNextButton");
        AudioManager.PlayMidButtonClick();
        OnNextButton?.Invoke();
    }

    void HandleSkipButton()
    {
        Debug.Log("GuiResolve.HandleSkipButton");
        AudioManager.PlayMidButtonClick();
        OnSkipButton?.Invoke();
    }
}
