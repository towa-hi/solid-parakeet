using System;
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
    int busyDepth;

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

        await ExecuteBusyAsync(async () =>
        {
            if (currentMenu != null)
            {
                currentMenu.ActionInvoked -= HandleMenuAction;
                currentMenu.CommandInvoked -= HandleMenuCommand;
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
                return;
            }
            currentMenu = menuBase;
            currentMenu.ActionInvoked += HandleMenuAction;
            currentMenu.CommandInvoked += HandleMenuCommand;
            currentMenu.Display(true);
            await currentMenu.OpenAsync();
        });

        isTransitioning = false;
    }

    void PushBusy()
    {
        busyDepth++;
        foreach (ModalBase element in modalStack)
        {
            element.OnFocus(false);
        }
        if (currentMenu != null)
        {
            currentMenu.SetInteractable(false);
        }
    }

    void PopBusy()
    {
        busyDepth = Mathf.Max(0, busyDepth - 1);
        if (busyDepth == 0)
        {
            if (modalStack != null && modalStack.Count > 0)
            {
                modalStack.Peek().OnFocus(true);
            }
            else if (currentMenu != null)
            {
                currentMenu.SetInteractable(true);
            }
        }
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
            if (busyDepth == 0)
            {
                modalStack.Peek().OnFocus(true);
            }
        }
        else if (currentMenu != null)
        {
            // ensure base menu is interactive again
            if (busyDepth == 0)
            {
                currentMenu.SetInteractable(true);
            }
        }
    }

    void HandleMenuAction(MenuAction action, object payload)
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
                if (payload is ModalConnectData connectData)
                {
                    _ = ConnectToNetworkAsync(connectData);
                }
                else
                {
                    Debug.LogError("MenuController: ConnectToNetwork action missing ModalConnectData payload");
                }
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

    void HandleMenuCommand(IMenuCommand command)
    {
        switch (command)
        {
            case ConnectToNetworkCommand connect:
                var data = new ModalConnectData { isTestnet = connect.isTestnet, contract = connect.contract, sneed = connect.sneed, isWallet = connect.isWallet };
                _ = ConnectToNetworkAsync(data);
                break;
            default:
                Debug.LogWarning($"MenuController: Unhandled command type {command.GetType().Name}");
                break;
        }
    }

    // Operation stubs (no-op for now)
    async Task ConnectToNetworkAsync(ModalConnectData data)
    {
        Result<bool> result = Result<bool>.Err(StatusCode.OTHER_ERROR, "Unknown error");
        await ExecuteBusyAsync(async () =>
        {
            result = await GameManager.instance.ConnectToNetwork(data);
        });
        if (result.IsOk)
        {
            SetMenu(mainMenuPrefab);
        }
        else
        {
            string message = string.IsNullOrEmpty(result.Message) ? "No details provided." : result.Message;
            await ShowErrorAsync(result.Code, message);
        }
    }

    async Task CreateLobbyAsync()
    {
        bool success = false;
        await ExecuteBusyAsync(async () =>
        {
            await Task.Yield();
            // perform create lobby request here
        });
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
        bool success = false;
        await ExecuteBusyAsync(async () =>
        {
            await Task.Yield();
            // perform join game request here
        });
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
        await ExecuteBusyAsync(async () =>
        {
            await Task.Yield();
            // perform save request here
        });
    }

    async Task ExecuteBusyAsync(Func<Task> work)
    {
        PushBusy();
        try
        {
            await work();
        }
        finally
        {
            PopBusy();
        }
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
        if (modalStack.Count == 0 && currentMenu != null && busyDepth == 0)
        {
            currentMenu.SetInteractable(true);
        }
    }

    async Task ShowErrorAsync(StatusCode code, string messageText)
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
        string title = $"{code} ({(int)code})";
        modal.Initialize(title, messageText, () => CloseTopModal());
        modal.OnFocus(true);
        modalStack.Push(modal);
        await modal.AwaitCloseAsync();
        if (modalStack.Count == 0 && currentMenu != null && busyDepth == 0)
        {
            currentMenu.SetInteractable(true);
        }
    }

    void GoOffline()
    {
        SetMenu(mainMenuPrefab);
    }
}
