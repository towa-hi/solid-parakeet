using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public string currentProcedure;
    
    void Start()
    {
        startMenuElement.makeInviteButton.onClick.AddListener(GotoInviteMenu);
        inviteMenuElement.backButton.onClick.AddListener(GotoStartMenu);
        inviteMenuElement.sendButton.onClick.AddListener(OnSendInviteButton);
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

    void GotoWaiting()
    {
        ShowElement(waitingElement);
    }
    
    async void OnSendInviteButton()
    {
        Blocker(true);
        InviteMenuParameters parameters = inviteMenuElement.GetInviteMenuParameters();
        bool success = await stellar.SendInvite(parameters);
        Blocker(false);
        if (success)
        {
            GotoWaiting();
        }
        else
        {
            Debug.LogError("SendInvite failed");
        }
    }
    
    void Blocker(bool isOn)
    {
        blocker.SetActive(isOn);
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