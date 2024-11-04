using UnityEngine;

public class GuiManager : MonoBehaviour
{
    public static GuiManager instance;
    public GuiMainMenu mainMenu;
    public GuiNicknameModal nicknameModal;
    public GameObject modalPanel;

    GuiElement currentMenu;
    ModalElement currentModal;
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            
            // main menu events
            mainMenu.OnChangeNicknameButton += OnChangeNicknameButton;
            mainMenu.OnNewLobbyButton += OnNewLobbyButton;
            mainMenu.OnJoinLobbyButton += OnJoinLobbyButton;
            mainMenu.OnSettingsButton += OnSettingsButton;
            mainMenu.OnExitButton += OnExitButton;
            
            // nickname modal events
            nicknameModal.OnCancel += OnNicknameModalCancel;
            nicknameModal.OnConfirm += OnNicknameModalConfirm;
        }
        else
        {
            Debug.LogWarning("MORE THAN ONE SINGLETON");
        }
    }

    void OnChangeNicknameButton()
    {
        Debug.Log("OnChangeNicknameButton");
        ShowModal(nicknameModal);
    }

    void OnNewLobbyButton()
    {
        Debug.Log("OnNewLobbyButton");
        return;
    }

    void OnJoinLobbyButton()
    {
        Debug.Log("OnJoinLobbyButton");
        return;
    }

    void OnSettingsButton()
    {
        Debug.Log("OnSettingsButton");
        return;
    }

    void OnExitButton()
    {
        Debug.Log("OnExitButton");
        return;
    }

    void OnNicknameModalCancel()
    {
        Debug.Log("OnNicknameModalCancel");
        if (currentModal == nicknameModal)
        {
            CloseCurrentModal();
        }
    }
    
    void OnNicknameModalConfirm()
    {
        Debug.Log("OnNicknameModalConfirm");
        if (currentModal == nicknameModal)
        {
            CloseCurrentModal();
        }
    }

    void ShowModal(ModalElement modal)
    {
        if (currentModal != null && currentModal != modal)
        {
            CloseCurrentModal();
        }
        modalPanel.SetActive(true);
        modal.ShowElement(true);
        currentModal = modal;
    }

    void CloseCurrentModal()
    {
        if (currentModal == null) return;
        currentModal.ShowElement(false);
        modalPanel.SetActive(false);
        currentModal = null;
    }
}
