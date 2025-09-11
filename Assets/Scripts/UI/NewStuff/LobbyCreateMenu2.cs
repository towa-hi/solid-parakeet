using UnityEngine;
using UnityEngine.UI;

public class LobbyCreateMenu2 : MenuBase
{
    public Button createLobbyButton;
    public Button backButton;

    private void Start()
    {
        createLobbyButton.onClick.AddListener(HandleCreateLobby);
        backButton.onClick.AddListener(HandleBack);
    }

    public void HandleCreateLobby()
    {
        EmitAction(MenuAction.CreateLobby);
    }

    public void HandleBack()
    {
        EmitAction(MenuAction.GotoMainMenu);
    }
    public override void Refresh()
    {
    }
}


