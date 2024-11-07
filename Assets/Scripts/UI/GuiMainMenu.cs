using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiMainMenu : MenuElement
{
    public TextMeshProUGUI nicknameText;
    public Button changeNicknameButton;
    public Button newLobbyButton;
    public Button joinLobbyButton;
    public Button settingsButton;
    public Button exitButton;

    public event Action OnChangeNicknameButton;
    public event Action OnNewLobbyButton;
    public event Action OnJoinLobbyButton;
    public event Action OnSettingsButton;
    public event Action OnExitButton;
    
    void Start()
    {
        changeNicknameButton.onClick.AddListener(HandleChangeNicknameButton);
        newLobbyButton.onClick.AddListener(HandleNewLobbyButton);
        joinLobbyButton.onClick.AddListener(HandleJoinLobbyButton);
        settingsButton.onClick.AddListener(HandleSettingsButton);
        exitButton.onClick.AddListener(HandleExitButton);
        RefreshNicknameText();
    }

    public void RefreshNicknameText()
    {
        nicknameText.text = GameManager.instance.client.isNicknameRegistered ? Globals.GetNickname() : "SET NICKNAME";
    }
    
    void HandleChangeNicknameButton()
    {
        OnChangeNicknameButton?.Invoke();
    }
    
    void HandleNewLobbyButton()
    {
        OnNewLobbyButton?.Invoke();
    }

    void HandleJoinLobbyButton()
    {
        OnJoinLobbyButton?.Invoke();
    }
    
    void HandleSettingsButton()
    {
        OnSettingsButton?.Invoke();
    }

    void HandleExitButton()
    {
        OnExitButton?.Invoke();
    }
    
}
