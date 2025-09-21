using UnityEngine;
using UnityEngine.UI;

public class GameMenu : MenuBase
{
    public Button backButton;
    public GuiGame guiGame;
    
    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
        // Inject MenuController into GuiGame for game-over exit path
        if (guiGame != null)
        {
            guiGame.SetMenuController(menuController);
        }
    }
    public override void Refresh()
    {
    }

    void HandleBack()
    {
        menuController.ExitGame();
    }
}
