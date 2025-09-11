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
        EmitAction(MenuAction.GotoNetwork);
    }

    public void HandleOffline()
    {
        EmitAction(MenuAction.GoOffline);
    }
    public override void Refresh()
    {
    }
}


