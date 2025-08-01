using System;
using UnityEngine;
using UnityEngine.UI;

public class ModalEscape : ModalElement
{
    public Button settingsButton;
    public Button resignButton;
    public Button mainMenuButton;
    public Button backButton;

    public Action OnSettingsButton;
    public Action OnResignButton;
    public Action OnMainMenuButton;
    public Action OnBackButton;

    void Start()
    {
        settingsButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnSettingsButton();
        });
        resignButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnResignButton();
        });
        mainMenuButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnMainMenuButton();
        });
        backButton.onClick.AddListener(() =>
        {
            AudioManager.instance.PlaySmallButtonClick();
            OnBackButton();
        });
    }
    public override void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
    }
    
}
