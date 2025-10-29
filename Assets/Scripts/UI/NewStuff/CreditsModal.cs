using System;
using UnityEngine;

public class CreditsModal : ModalBase
{
    public ButtonExtended closeButton;
    public Action OnCloseButton;

    void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                OnCloseButton?.Invoke();
            });
        }
    }

    public void Initialize(Action inOnCloseButton)
    {
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
