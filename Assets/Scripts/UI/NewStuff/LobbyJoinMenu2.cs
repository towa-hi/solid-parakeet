using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Contract;

public class LobbyJoinMenu2 : MenuBase
{
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI contractAddressText;
    public TMP_InputField lobbyIdInputField;
    public Button joinGameButton;
    public Button backButton;

    private void Start()
    {
        joinGameButton.onClick.AddListener(HandleJoinGame);
        backButton.onClick.AddListener(HandleBack);
        if (lobbyIdInputField != null)
        {
            lobbyIdInputField.onValueChanged.AddListener(_ => Refresh());
        }
        Refresh();
    }

    public void HandleJoinGame()
    {
        if (lobbyIdInputField == null) return;
        string input = lobbyIdInputField.text;
        if (!IsValidLobbyId(input)) return;
        uint value = uint.Parse(input);
        // Directly ask controller to join and navigate
        _ = menuController.JoinGameFromMenu(new LobbyId(value));
    }

    public void HandleBack()
    {
        menuController.SetMenu(menuController.mainMenuPrefab);
    }

    public override void Refresh()
    {
        if (contractAddressText != null)
        {
            contractAddressText.text = StellarManager.networkContext.contractAddress;
        }
        if (joinGameButton != null && lobbyIdInputField != null)
        {
            bool valid = IsValidLobbyId(lobbyIdInputField.text);
            joinGameButton.interactable = valid;
            if (statusText != null)
            {
                statusText.text = valid ? "Click Join Lobby" : "Enter a valid lobby ID";
            }
        }
    }

    bool IsValidLobbyId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        for (int i = 0; i < input.Length; i++)
        {
            if (!char.IsDigit(input[i])) return false;
        }
        if (input.Length != 6) return false;
        if (!uint.TryParse(input, out uint value) || value == 0) return false;
        return true;
    }
}


