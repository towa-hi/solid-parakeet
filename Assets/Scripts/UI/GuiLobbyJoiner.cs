using System;
using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyJoiner : MenuElement
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
            AudioManager.PlaySmallButtonClick();
            OnBackButton?.Invoke();
        });
        joinButton.onClick.AddListener(() =>
        {
            AudioManager.PlayMidButtonClick();
            OnJoinButton?.Invoke(new LobbyId(uint.Parse(lobbyIdInputField.text)));
        });
        lobbyIdInputField.onValueChanged.AddListener(OnLobbyIdInputFieldChanged);
    }

    void OnNetworkStateUpdated()
    {
        if (!gameObject.activeSelf) return;
        Refresh();
    }

    public override void Refresh()
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
