using UnityEngine;
using UnityEngine.UI;

public class StartMenu2 : MenuBase
{
    public Button connectToNetworkButton;
    public Button offlineButton;

    private void Start()
    {
        connectToNetworkButton.onClick.AddListener(HandleConnectToNetwork);
        offlineButton.onClick.AddListener(HandleOffline);
    }

    public void HandleConnectToNetwork()
    {
        _ = menuController.SetMenuAsync(menuController.networkMenuPrefab);
    }

    public void HandleOffline()
    {
        // GoOffline means just go back to main menu in this flow
        _ = menuController.SetMenuAsync(menuController.mainMenuPrefab);
    }
    public override void Refresh()
    {
    }
}


