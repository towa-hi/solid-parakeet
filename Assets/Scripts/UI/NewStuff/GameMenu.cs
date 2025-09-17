using UnityEngine;
using UnityEngine.UI;

public class GameMenu : MenuBase
{
    public Button backButton;
    
    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
    }
    public override void Refresh()
    {
    }

    void HandleBack()
    {
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }
}
