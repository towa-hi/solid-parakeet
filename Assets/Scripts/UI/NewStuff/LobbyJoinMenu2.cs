using UnityEngine;
using UnityEngine.UI;

public class LobbyJoinMenu2 : MenuBase
{
    public Button joinGameButton;
    public Button backButton;

    private void Start()
    {
        joinGameButton.onClick.AddListener(HandleJoinGame);
        backButton.onClick.AddListener(HandleBack);
    }

    public void HandleJoinGame()
    {
        EmitAction(MenuAction.GotoLobbyView);
    }

    public void HandleBack()
    {
        EmitAction(MenuAction.GotoMainMenu);
    }

    public override void Refresh()
    {
    }
}


