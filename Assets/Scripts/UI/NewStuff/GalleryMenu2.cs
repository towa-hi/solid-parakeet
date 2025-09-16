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
        menuController.SetMenu(menuController.mainMenuPrefab);
    }
    public override void Refresh()
    {
    }
}


