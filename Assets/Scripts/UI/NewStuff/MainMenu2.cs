using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu2 : MenuBase
{
    public TextMeshProUGUI topText;
    public ButtonExtended createLobbyButton;
    public ButtonExtended viewLobbyButton;
    public ButtonExtended settingsButton;
    public ButtonExtended networkButton;
    public ButtonExtended galleryButton;
    public ButtonExtended joinLobbyButton;
    public ButtonExtended quitButton;

    private void Start()
    {
        createLobbyButton.onClick.AddListener(HandleCreateLobby);
        viewLobbyButton.onClick.AddListener(HandleViewLobby);
        settingsButton.onClick.AddListener(HandleSettings);
        networkButton.onClick.AddListener(HandleNetwork);
        galleryButton.onClick.AddListener(HandleGallery);
        joinLobbyButton.onClick.AddListener(HandleJoinLobby);
        quitButton.onClick.AddListener(HandleQuit);
        Refresh();
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

    public void HandleQuit()
    {
        EmitAction(MenuAction.Quit);
    }

    public override void Refresh()
    {
        bool isOnline = GameManager.instance.IsOnline();
        bool isInLobby = StellarManager.networkState.inLobby;
        topText.text = isOnline ? "Online" : "Offline";
        createLobbyButton.text.text = isOnline ? "CREATE LOBBY" : "SINGLEPLAYER";
        joinLobbyButton.interactable = isOnline && !isInLobby;
        viewLobbyButton.interactable = isOnline && isInLobby;
        
    }
}


