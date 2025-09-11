using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    public Transform menuRoot;
    public Transform modalRoot;
    public GameObject startMenuPrefab;
    public GameObject networkMenuPrefab;
    public GameObject mainMenuPrefab;
    public GameObject settingsMenuPrefab;
    public GameObject galleryMenuPrefab;
    public GameObject lobbyCreateMenuPrefab;
    public GameObject lobbyViewMenuPrefab;
    public GameObject lobbyJoinMenuPrefab;
    public GameObject gameMenuPrefab;

    
    public GameObject messageModalPrefab;
    // state machine
    public MenuBase currentMenu;
    // modal stack stuff
    public Stack<ModalBase> modalStack;
    bool isTransitioning;

    void Start()
    {
        modalStack = new();
        if (startMenuPrefab)
        {
            SetMenu(startMenuPrefab);
        }
    }

    public void SetMenu(GameObject prefab)
    {
        if (prefab == null) return;
        _ = SetMenuAsync(prefab);
    }

    async Task SetMenuAsync(GameObject prefab)
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (currentMenu != null)
        {
            currentMenu.ActionInvoked -= HandleMenuAction;
            await currentMenu.CloseAsync();
            Destroy(currentMenu.gameObject);
            currentMenu = null;
            await Task.Yield();
        }

        GameObject instance = Instantiate(prefab, menuRoot);
        MenuBase menuBase = instance.GetComponent<MenuBase>();
        if (menuBase == null)
        {
            Debug.LogError("MenuController: Instantiated prefab has no MenuBase component.");
            isTransitioning = false;
            return;
        }
        currentMenu = menuBase;
        currentMenu.ActionInvoked += HandleMenuAction;
        currentMenu.Display(true);
        await currentMenu.OpenAsync();

        isTransitioning = false;
    }

    public void OpenMessageModal(string messageText)
    {
        foreach (ModalBase element in modalStack)
        {
            element.OnFocus(false);
        }
        if (currentMenu != null)
        {
            currentMenu.SetInteractable(false);
        }
        GameObject instance = Instantiate(messageModalPrefab, modalRoot);
        MessageModal modal = instance.GetComponent<MessageModal>();
        if (modal == null)
        {
            Debug.LogError("MenuController: messageModalPrefab is missing MessageModal component");
            Destroy(instance);
            return;
        }
        modal.Initialize(messageText, () => CloseTopModal());
        modal.OnFocus(true);
        modalStack.Push(modal);
    }

    void CloseTopModal()
    {
        if (modalStack == null || modalStack.Count == 0) return;
        ModalBase top = modalStack.Pop();
        Destroy(top.gameObject);
        if (modalStack.Count > 0)
        {
            modalStack.Peek().OnFocus(true);
        }
        else if (currentMenu != null)
        {
            // ensure base menu is interactive again
            currentMenu.SetInteractable(true);
        }
    }

    void HandleMenuAction(MenuAction action)
    {
        Debug.Log($"MenuController received action: {action}");
        switch (action)
        {
            case MenuAction.GotoStartMenu: SetMenu(startMenuPrefab); break;
            case MenuAction.GotoNetwork: SetMenu(networkMenuPrefab); break;
            case MenuAction.GotoMainMenu: SetMenu(mainMenuPrefab); break;
            case MenuAction.GotoLobbyCreate: SetMenu(lobbyCreateMenuPrefab); break;
            case MenuAction.GotoLobbyView: SetMenu(lobbyViewMenuPrefab); break;
            case MenuAction.GotoLobbyJoin: SetMenu(lobbyJoinMenuPrefab); break;
            case MenuAction.GotoSettings: SetMenu(settingsMenuPrefab); break;
            case MenuAction.GotoGallery: SetMenu(galleryMenuPrefab); break;
            case MenuAction.GotoGame: SetMenu(gameMenuPrefab); break;

            case MenuAction.ConnectToNetwork:
                _ = ConnectToNetworkAsync();
                break;
            case MenuAction.CreateLobby:
                _ = CreateLobbyAsync();
                break;
            case MenuAction.JoinGame:
                _ = JoinGameAsync();
                break;
            case MenuAction.GoOffline:
                GoOffline();
                break;
            case MenuAction.Refresh:
                RefreshData();
                break;
            case MenuAction.SaveChanges:
                _ = SaveChangesAsync();
                break;
            case MenuAction.None:
            default:
                break;
        }
    }

    // Operation stubs (no-op for now)
    async Task ConnectToNetworkAsync()
    {
        // Simulate a failed connection attempt
        await Task.Yield();
        bool success = false;
        if (success)
        {
            SetMenu(mainMenuPrefab);
        }
        else
        {
            await ShowMessageAsync("Failed to connect to the network.");
        }
    }

    async Task CreateLobbyAsync()
    {
        await Task.Yield();
        bool success = false;
        if (success)
        {
            SetMenu(lobbyViewMenuPrefab);
        }
        else
        {
            await ShowMessageAsync("Failed to create lobby.");
        }
    }

    async Task JoinGameAsync()
    {
        await Task.Yield();
        bool success = false;
        if (success)
        {
            SetMenu(lobbyViewMenuPrefab);
        }
        else
        {
            await ShowMessageAsync("Failed to join game.");
        }
    }

    void RefreshData()
    {
    }

    async Task SaveChangesAsync()
    {
        await Task.Yield();
    }

    // Awaitable modal helper
    async Task ShowMessageAsync(string messageText)
    {
        foreach (ModalBase element in modalStack)
        {
            element.OnFocus(false);
        }
        if (currentMenu != null)
        {
            currentMenu.SetInteractable(false);
        }
        GameObject instance = Instantiate(messageModalPrefab, modalRoot);
        instance.transform.SetAsLastSibling();
        MessageModal modal = instance.GetComponent<MessageModal>();
        if (modal == null)
        {
            Debug.LogError("MenuController: messageModalPrefab is missing MessageModal component");
            Destroy(instance);
            return;
        }
        modal.Initialize(messageText, () => CloseTopModal());
        modal.OnFocus(true);
        modalStack.Push(modal);
        await modal.AwaitCloseAsync();
        if (modalStack.Count == 0 && currentMenu != null)
        {
            currentMenu.SetInteractable(true);
        }
    }

    void GoOffline()
    {
        SetMenu(mainMenuPrefab);
    }
}
