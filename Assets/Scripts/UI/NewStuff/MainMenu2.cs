using UnityEngine;
using UnityEngine.UI;

public class MainMenu2 : MenuBase
{
    public Button createLobbyButton;
    public Button viewLobbyButton;
    public Button settingsButton;
    public Button networkButton;
    public Button galleryButton;
    public Button joinLobbyButton;

    private void Start()
    {
        createLobbyButton.onClick.AddListener(HandleCreateLobby);
        viewLobbyButton.onClick.AddListener(HandleViewLobby);
        settingsButton.onClick.AddListener(HandleSettings);
        networkButton.onClick.AddListener(HandleNetwork);
        galleryButton.onClick.AddListener(HandleGallery);
        joinLobbyButton.onClick.AddListener(HandleJoinLobby);
    }

    public void HandleCreateLobby()
    {
        EmitAction(MenuAction.GotoLobbyCreate);
    }

    public void HandleViewLobby()
    {
        EmitAction(MenuAction.GotoLobbyView);
    }

    public void HandleSettings()
    {
        EmitAction(MenuAction.GotoSettings);
    }

    public void HandleNetwork()
    {
        EmitAction(MenuAction.GotoNetwork);
    }

    public void HandleGallery()
    {
        EmitAction(MenuAction.GotoGallery);
    }

    public void HandleJoinLobby()
    {
        EmitAction(MenuAction.GotoLobbyJoin);
    }
    public override void Refresh()
    {
    }
}


