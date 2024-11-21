
using System;
using UnityEngine;
using UnityEngine.UI;

public class GuiMoveControls : MonoBehaviour
{
    public Button moveSubmitButton;
    public event Action OnMoveSubmitButton;
    void Start()
    {
        moveSubmitButton.onClick.AddListener(HandleMoveSubmitButton);
    }

    void HandleMoveSubmitButton()
    {
        OnMoveSubmitButton?.Invoke();
    }

    public void OnMoveResponse(Response<bool> response)
    {
        if (response.data)
        {
            moveSubmitButton.interactable = false;
        }
    }
}
