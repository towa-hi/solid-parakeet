using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Contract;

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
        _ = SetMenuAsync(startMenuPrefab);
    }

    public async Task SetMenuAsync(GameObject prefab)
    {
        if (isTransitioning) return;
        isTransitioning = true;

        await ExecuteBusyAsync(async () =>
        {
            if (currentMenu != null)
            {
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
            currentMenu.SetMenuController(this);
            currentMenu.Display(true);
            await currentMenu.OpenAsync();
            isTransitioning = false;
        });
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

    // playaing offline means just sending a data with online = false
    public async Task ConnectToNetworkAsync(ModalConnectData data)
    {
        await ExecuteBusyAsync(async () =>
        {
            Result<bool> initializeResult = await StellarManager.Initialize(data);
            if (initializeResult.IsError)
            {
                StellarManager.Uninitialize();
                await ShowErrorAsync(initializeResult.Code, initializeResult.Message);
                return;
            }
            Result<bool> updateResult = await StellarManager.UpdateState();
            if (updateResult.IsError)
            {
                StellarManager.Uninitialize();
                await ShowErrorAsync(updateResult.Code, updateResult.Message);
                return;
            }
            await SetMenuAsync(mainMenuPrefab);
        });
    }

    public async Task CreateLobbyAsync(LobbyCreateData lobbyCreateData)
    {
        await ExecuteBusyAsync(async () =>
        {
            Result<LobbyParameters> lobbyParametersResult = lobbyCreateData.ToLobbyParameters();
            if (lobbyParametersResult.IsError)
            {
                await ShowErrorAsync(lobbyParametersResult.Code, lobbyParametersResult.Message);
                return;
            }
            LobbyParameters lobbyParameters = lobbyParametersResult.Value;
            Result<bool> result = await StellarManager.MakeLobbyRequest(lobbyParameters, lobbyCreateData.isMultiplayer);
            if (result.IsError)
            {
                StellarManager.Uninitialize();
                await ShowErrorAsync(result.Code, result.Message);
                return;
            }
            Result<bool> updateResult = await StellarManager.UpdateState();
            if (updateResult.IsError)
            {
                StellarManager.Uninitialize();
                await ShowErrorAsync(updateResult.Code, updateResult.Message);
                return;
            }
            await SetMenuAsync(lobbyViewMenuPrefab);
        });
    }

    public async Task JoinGameFromMenu(LobbyId lobbyId)
    {
        await ExecuteBusyAsync(async () =>
        {
            var result = await StellarManager.JoinLobbyRequest(lobbyId);
            if (result.IsError)
            {
                await ShowErrorAsync(result.Code, result.Message);
                return;
            }
            var update = await StellarManager.UpdateState();
            if (update.IsError)
            {
                await ShowErrorAsync(update.Code, update.Message);
                return;
            }
            await SetMenuAsync(lobbyViewMenuPrefab);
        });
    }

    public void RefreshData()
    {
    }

    public async Task LeaveLobbyForMenu()
    {
        await ExecuteBusyAsync(async () =>
        {
            var result = await StellarManager.LeaveLobbyRequest();
            if (result.IsError)
            {
                await ShowErrorAsync(result.Code, result.Message);
                return;
            }
            var update = await StellarManager.UpdateState();
            if (update.IsError)
            {
                await ShowErrorAsync(update.Code, update.Message);
                return;
            }
            await SetMenuAsync(mainMenuPrefab);
        });
    }

    public void SaveChange(WarmancerSettings settings)
    {
        SettingsManager.Save(settings);
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
        string body = string.IsNullOrEmpty(messageText) ? "No details provided." : messageText;
        modal.Initialize(title, body, () => CloseTopModal());
        modal.OnFocus(true);
        modalStack.Push(modal);
        await modal.AwaitCloseAsync();
        if (modalStack.Count == 0 && currentMenu != null && busyDepth == 0)
        {
            currentMenu.SetInteractable(true);
        }
    }

}
