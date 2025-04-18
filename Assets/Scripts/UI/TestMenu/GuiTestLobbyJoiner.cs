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
        backButton.onClick.AddListener(delegate { OnBackButton?.Invoke(); });
        joinButton.onClick.AddListener(delegate { OnJoinButton?.Invoke(lobbyIdInputField.text); });
        lobbyIdInputField.onValueChanged.AddListener(OnLobbyIdValueChanged);
        StellarManagerTest.OnContractAddressUpdated += OnContractAddressUpdated;
        StellarManagerTest.OnSneedUpdated += OnSneedUpdated;
        StellarManagerTest.OnCurrentUserUpdated += OnCurrentUserUpdated;
    }

    void OnLobbyIdValueChanged(string arg0)
    {
        Refresh();
    }

    void OnCurrentUserUpdated(User? obj)
    {
        Refresh();
    }

    void OnSneedUpdated(string obj)
    {
        Refresh();
    }

    void OnContractAddressUpdated(string obj)
    {
        Refresh();
    }

    public override void Initialize()
    {
        lobbyIdInputField.text = string.Empty;
        Refresh();
    }

    public override void Refresh()
    {
        contractAddressText.text = StellarManagerTest.GetContractAddress();
        joinButton.interactable = Guid.TryParse(lobbyIdInputField.text, out _);
    }

    public string GetLobbyId()
    {
        return lobbyIdInputField.text;
    }
}
