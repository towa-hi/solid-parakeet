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
        menuController.SetMenu(menuController.lobbyCreateMenuPrefab);
    }

    public void HandleViewLobby()
    {
        menuController.SetMenu(menuController.lobbyViewMenuPrefab);
    }

    public void HandleSettings()
    {
        menuController.SetMenu(menuController.settingsMenuPrefab);
    }

    public void HandleNetwork()
    {
        menuController.SetMenu(menuController.networkMenuPrefab);
    }

    public void HandleGallery()
    {
        menuController.SetMenu(menuController.galleryMenuPrefab);
    }

    public void HandleJoinLobby()
    {
        menuController.SetMenu(menuController.lobbyJoinMenuPrefab);
    }

    public void HandleQuit()
    {
        Application.Quit();
    }

    public override void Refresh()
    {
        bool isOnline = StellarManager.networkContext.online;
        bool isInLobby = StellarManager.networkState.inLobby;
        topText.text = isOnline ? "Online" : "Offline";
        createLobbyButton.text.text = isOnline ? "CREATE LOBBY" : "SINGLEPLAYER";
        createLobbyButton.interactable = !isInLobby;
        joinLobbyButton.interactable = isOnline && !isInLobby;
        viewLobbyButton.interactable = isInLobby;
        
    }
}


