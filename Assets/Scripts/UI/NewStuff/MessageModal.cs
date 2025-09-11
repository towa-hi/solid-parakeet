using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessageModal : ModalBase
{
    public TextMeshProUGUI message;
    public Button closeButton;
    public Action OnCloseButton;
    
    void Start()
    {
        closeButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySmallButtonClick();
            OnCloseButton();
        });
    }
    
    public void Initialize(string messageText, Action inOnCloseButton)
    {
        OnCloseButton = inOnCloseButton;
    }

    public override void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
    }
}
