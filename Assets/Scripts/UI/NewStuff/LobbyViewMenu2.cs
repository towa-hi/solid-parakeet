using UnityEngine;
using UnityEngine.UI;

public class LobbyViewMenu2 : MenuBase
{
    public Button enterGameButton;
    public Button backButton;
    public Button refreshButton;

    private void Start()
    {
        enterGameButton.onClick.AddListener(HandleEnterGame);
        backButton.onClick.AddListener(HandleBack);
        refreshButton.onClick.AddListener(HandleRefresh);
    }

    public void HandleEnterGame()
    {
        EmitAction(MenuAction.GotoGame);
    }

    public void HandleBack()
    {
        EmitAction(MenuAction.GotoMainMenu);
    }

    public void HandleRefresh()
    {
        EmitAction(MenuAction.Refresh);
    }
    public override void Refresh()
    {
    }
}


