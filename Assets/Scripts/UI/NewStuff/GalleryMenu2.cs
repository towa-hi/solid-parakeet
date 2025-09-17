using UnityEngine;
using UnityEngine.UI;

public class GalleryMenu2 : MenuBase
{
    public Button backButton;

    private void Start()
    {
        backButton.onClick.AddListener(HandleBack);
    }

    public void HandleBack()
    {
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }
    public override void Refresh()
    {
    }
}


