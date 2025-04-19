using System;
using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestLobbyJoiner : TestGuiElement
{

    public TextMeshProUGUI statusText;
    public TextMeshProUGUI contractAddressText;
    public TMP_InputField lobbyIdInputField;

    public Button backButton;
    public Button joinButton;

    public event Action<string> OnJoinButton;
    public event Action OnBackButton;

    void Start()
    {
        backButton.onClick.AddListener(() => { OnBackButton?.Invoke(); });
        joinButton.onClick.AddListener(() => { OnJoinButton?.Invoke(lobbyIdInputField.text); });
        lobbyIdInputField.onValueChanged.AddListener(OnLobbyIdInputFieldChanged);
        StellarManagerTest.OnNetworkStateUpdated += OnNetworkStateUpdated;
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
        contractAddressText.text = StellarManagerTest.GetContractAddress();
        joinButton.interactable = Guid.TryParse(lobbyIdInputField.text, out _);
    }

    void OnLobbyIdInputFieldChanged(string input)
    {
        Refresh();
    }
}
