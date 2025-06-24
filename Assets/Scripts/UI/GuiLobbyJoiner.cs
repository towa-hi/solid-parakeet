using System;
using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyJoiner : TestGuiElement
{

    public TextMeshProUGUI statusText;
    public TextMeshProUGUI contractAddressText;
    public TMP_InputField lobbyIdInputField;

    public Button backButton;
    public Button joinButton;

    public event Action<LobbyId> OnJoinButton;
    public event Action OnBackButton;

    void Start()
    {
        backButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        joinButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlayMidButtonClick();
            OnJoinButton?.Invoke(new LobbyId(uint.Parse(lobbyIdInputField.text)));
        });
        lobbyIdInputField.onValueChanged.AddListener(OnLobbyIdInputFieldChanged);
        StellarManager.OnNetworkStateUpdated += OnNetworkStateUpdated;
    }

    public override void SetIsEnabled(bool inIsEnabled, bool networkUpdated)
    {
        base.SetIsEnabled(inIsEnabled, networkUpdated);
        if (isEnabled && networkUpdated)
        {
            OnNetworkStateUpdated();
        }
    }

    void OnNetworkStateUpdated()
    {
        if (!isEnabled) return;
        Refresh();
    }

    void Refresh()
    {
        contractAddressText.text = StellarManager.GetContractAddress();
        joinButton.interactable = lobbyIdInputField.text.Length > 0;
        string status = "Enter a valid lobby ID";
        if (joinButton.interactable)
        {
            status = "click Join Lobby";
        }
        statusText.text = status;
    }

    void OnLobbyIdInputFieldChanged(string input)
    {
        Refresh();
    }
}
