using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModalError : ModalElement
{
	public TextMeshProUGUI titleText;
	public TextMeshProUGUI messageText;
	public Button closeButton;

	public Action OnCloseButton;

	void Start()
	{
		if (closeButton != null)
		{
			closeButton.onClick.AddListener(HandleCloseButton);
		}
	}

	public override void OnFocus(bool focused)
	{
		canvasGroup.interactable = focused;
	}

	public void SetContent(string title, string message)
	{
		if (titleText != null)
		{
			titleText.text = title;
		}
		if (messageText != null)
		{
			messageText.text = message;
		}
	}

	void HandleCloseButton()
	{
		AudioManager.PlaySmallButtonClick();
		OnCloseButton?.Invoke();
	}
}


