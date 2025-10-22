using System;
using UnityEngine;
using UnityEngine.UI;
using Contract;
public class    ModalEscape : ModalElement
{
    public GameObject mainLayer;
    public Button resignButton;
    public Button claimWinButton;
    public Button mainMenuButton;
    public Button backButton;

    public GameObject claimWinLayer;
    public ButtonExtended claimWinYes;
    public ButtonExtended claimWinNo;
    public GameObject resignLayer;
    public ButtonExtended resignYes;
    public ButtonExtended resignNo;

    public Action OnResignButton;
    public Action OnClaimWinButton;
    public Action OnMainMenuButton;
    public Action OnBackButton;

    void Start()
    {
        resignButton.onClick.AddListener(() =>
        {
            OnResignButton();
        });
        claimWinButton.onClick.AddListener(() =>
        {
            OnClaimWinButton();
        });
        mainMenuButton.onClick.AddListener(() =>
        {
            OnMainMenuButton();
        });
        backButton.onClick.AddListener(() =>
        {
            OnBackButton();
        });
        claimWinYes.onClick.AddListener(() =>
        {
            OnClaimWinYes();
        });
        claimWinNo.onClick.AddListener(() =>
        {
            GoToMainLayer();
        });
        resignYes.onClick.AddListener(() =>
        {
            OnResignYes();
        });
        resignNo.onClick.AddListener(() =>
        {
            GoToMainLayer();
        });
        GoToMainLayer();
    }

    async void OnClaimWinYes()
    {
        var result = await StellarManager.RedeemWinRequest(new RedeemWinReq { lobby_id = StellarManager.networkState.lobbyInfo.Value.index });
        GoToMainLayer();
    }
    async void OnResignYes()
    {
        GoToMainLayer();
    }
    void GoToMainLayer()
    {
        mainLayer.SetActive(true);
        claimWinLayer.SetActive(false);
        resignLayer.SetActive(false);
    }

    void GoToClaimWinLayer()
    {
        mainLayer.SetActive(false);
        claimWinLayer.SetActive(true);
        resignLayer.SetActive(false);
    }
    
    void GoToResignLayer()
    {
        mainLayer.SetActive(false);
        claimWinLayer.SetActive(false);
        resignLayer.SetActive(true);
    }

    public override void OnFocus(bool focused)
    {
        canvasGroup.interactable = focused;
    }
    
}
