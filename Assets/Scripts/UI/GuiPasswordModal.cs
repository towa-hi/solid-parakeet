using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiPasswordModal : ModalElement
{
    public TMP_InputField passwordInputField;
    public TextMeshProUGUI confirmButtonLabel;
    public Button cancelButton;
    public Button confirmButton;

    public event Action OnCancel;
    public event Action OnConfirm;

    void Start()
    {
        passwordInputField.onValueChanged.AddListener(OnPasswordInputChanged);
        cancelButton.onClick.AddListener(HandleCancelButton);
        confirmButton.onClick.AddListener(HandleConfirmButton);
    }

    public override void ShowElement(bool show)
    {
        base.ShowElement(show);
        if (show)
        {
            Reset();
        }
    }

    public string GetPassword()
    {
        return passwordInputField.text;
    }
    
    void Reset()
    {
        passwordInputField.text = String.Empty;
        confirmButton.interactable = false;
    }

    void HandleCancelButton()
    {
        OnCancel?.Invoke();
    }

    void HandleConfirmButton()
    {
        OnConfirm?.Invoke();
    }

    void OnPasswordInputChanged(string input)
    {
        confirmButton.interactable = Shared.IsPasswordValid(input);
    }
}
