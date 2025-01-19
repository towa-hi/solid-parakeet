using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyMenu : MenuElement
{
    public Button cancelButton;
    public Button readyButton;
    public Button demoButton;
    public TextMeshProUGUI readyButtonText;
    public TextMeshProUGUI passwordText;
    public TextMeshProUGUI boardNameText;
    [SerializeField] bool readyButtonState;
    
    // chat is handled in it's own component
    
    public event Action OnCancelButton;
    public event Action<bool> OnReadyButton;

    public event Action OnDemoButton;

    void Start()
    {
        cancelButton.onClick.AddListener(HandleCancelButton);
        readyButton.onClick.AddListener(HandleReadyButton);
        demoButton.onClick.AddListener(HandleDemoButton);
    }

    public override void ShowElement(bool enable)
    {
        base.ShowElement(enable);
        cancelButton.interactable = enable;
        readyButton.interactable = enable;
        demoButton.interactable = enable;
    }

    public void SetLobby(SLobby lobby)
    {
        passwordText.text = $"Password: {lobby.password}";
        boardNameText.text = lobby.lobbyParameters.board.boardName;
        SetReadyButtonState(false);

    }

    void SetReadyButtonState(bool state)
    {
        readyButtonState = state;
        if (readyButtonState)
        {
            readyButton.image.color = Color.red;
            readyButtonText.text = "Click to unready";
        }
        else
        {
            readyButton.image.color = Color.green;
            readyButtonText.text = "Click to ready";
        }
        
    }
    
    void HandleCancelButton()
    {
        OnCancelButton?.Invoke();
    }

    void HandleReadyButton()
    {
        OnReadyButton?.Invoke(!readyButtonState);
    }

    void HandleDemoButton()
    {
        OnDemoButton?.Invoke();
    }
}
