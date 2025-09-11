using UnityEngine;
using UnityEngine.UI;

public class NetworkMenu2 : MenuBase
{
    public Button connectButton;
    public Button offlineButton;

    private void Start()
    {
        connectButton.onClick.AddListener(HandleConnect);
        offlineButton.onClick.AddListener(HandleOffline);
    }

    public void HandleConnect()
    {
        EmitAction(MenuAction.ConnectToNetwork);
    }

    public void HandleOffline()
    {
        EmitAction(MenuAction.GoOffline);
    }
    public override void Refresh()
    {
    }
}


