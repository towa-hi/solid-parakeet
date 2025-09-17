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
        _ = menuController.SetMenuAsync(menuController.lobbyCreateMenuPrefab);
    }

    public void HandleViewLobby()
    {
        _ = menuController.SetMenuAsync(menuController.lobbyViewMenuPrefab);
    }

    public void HandleSettings()
    {
        _ = menuController.SetMenuAsync(menuController.settingsMenuPrefab);
    }

    public void HandleNetwork()
    {
        _ = menuController.SetMenuAsync(menuController.networkMenuPrefab);
    }

    public void HandleGallery()
    {
        _ = menuController.SetMenuAsync(menuController.galleryMenuPrefab);
    }

    public void HandleJoinLobby()
    {
        _ = menuController.SetMenuAsync(menuController.lobbyJoinMenuPrefab);
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


