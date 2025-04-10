using System.Collections.Generic;
using UnityEngine;

public class GuiTestMenuController : MenuElement
{
    public StellarManagerTest stellar;
    
    public GuiTestStartMenu startMenuElement;
    public GuiTestInviteMenu inviteMenuElement;
    public GuiTestInviteList inviteListElement;
    public GuiTestWaiting waitingElement;
    public GameObject blocker;
    // state
    public TestGuiElement currentElement;
    public bool blocked;
    
    void Start()
    {
        startMenuElement.makeInviteButton.onClick.AddListener(GotoInviteMenu);
        inviteMenuElement.backButton.onClick.AddListener(GotoStartMenu);
        inviteMenuElement.sendButton.onClick.AddListener(OnSendInviteButton);
        StellarManagerTest.OnWaiting += Blocker;
        stellar.Initialize();
        Initialize();
    }
    
    public void Initialize()
    {
        startMenuElement.SetIsEnabled(false);
        inviteMenuElement.SetIsEnabled(false);
        inviteListElement.SetIsEnabled(false);
        waitingElement.SetIsEnabled(false);
        
        ShowElement(startMenuElement);
    }

    void ShowElement(TestGuiElement element)
    {
        if (currentElement != null)
        {
            currentElement.SetIsEnabled(false);
        }
        currentElement = element;
        currentElement.SetIsEnabled(true);
        currentElement.Initialize();
    }

    void GotoInviteMenu()
    {
        ShowElement(inviteMenuElement);
    }

    void GotoStartMenu()
    {
        ShowElement(startMenuElement);
    }

    void OnSendInviteButton()
    {
        InviteMenuParameters parameters = inviteMenuElement.GetInviteMenuParameters();
        _ = stellar.SendInvite(parameters);
    }
    
    public void Blocker(bool inBlocked)
    {
        // TODO: make this into a actual await manager
        blocked = inBlocked;
        blocker.SetActive(inBlocked);
    }
}

public class TestGuiElement: MonoBehaviour
{
    bool isEnabled;
    
    public void SetIsEnabled(bool inIsEnabled)
    {
        isEnabled = inIsEnabled;
        gameObject.SetActive(inIsEnabled);
    }

    public virtual void Refresh()
    {
        
    }

    public virtual void Initialize()
    {
        
    }
}