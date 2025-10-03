using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessageModal : ModalBase
{
    public TextMeshProUGUI title;
    public TextMeshProUGUI message;
    public Button closeButton;
    public Action OnCloseButton;
    
    void Start()
    {
        Debug.Log("started");
        closeButton.onClick.AddListener(() =>
        {
            Debug.Log("MessageModal: close button clicked");
            OnCloseButton();
        });
    }
    
    public void Initialize(string messageText, Action inOnCloseButton)
    {
        if (message)
        {
            message.text = messageText;
        }
        OnCloseButton = inOnCloseButton;
        PrepareAwaitable();
    }

    public void Initialize(string titleText, string messageText, Action inOnCloseButton)
    {
        if (title)
        {
            title.text = titleText;
        }
        if (message)
        {
            message.text = messageText;
        }
        OnCloseButton = inOnCloseButton;
        PrepareAwaitable();
    }

    public override void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
        canvasGroup.blocksRaycasts = focused;
        // Ensure the modal captures input when focused
        if (focused)
        {
            transform.SetAsLastSibling();
        }
    }
}
