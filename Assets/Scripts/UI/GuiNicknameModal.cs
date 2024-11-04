using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiNicknameModal : ModalElement
{
    public TMP_InputField nicknameInputField;
    public TextMeshProUGUI confirmButtonLabel;
    public Button cancelButton;
    public Button confirmButton;

    // Define events for confirm and cancel actions
    public event Action OnCancel;
    public event Action OnConfirm;
    
    void Start()
    {
        nicknameInputField.onValueChanged.AddListener(OnNicknameInputChanged);
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

    void Reset()
    {
        nicknameInputField.text = string.Empty;
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

    void OnNicknameInputChanged(string input)
    {
        confirmButton.interactable = Globals.IsNicknameValid(input);
    }
}

