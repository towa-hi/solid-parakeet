using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Contract;

public class LobbyJoinMenu2 : MenuBase
{
    public TextMeshProUGUI contractAddressText;
    public TextMeshProUGUI networkText;
    public TextMeshProUGUI addressText;

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
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }

    public override void Refresh()
    {
        contractAddressText.text = StellarManager.networkContext.contractAddress;
        addressText.text = StellarManager.networkContext.userAccount.AccountId;
        networkText.text = StellarManager.networkContext.serverUri.ToString();
        bool valid = IsValidLobbyId(lobbyIdInputField.text);
        joinGameButton.interactable = valid;
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


