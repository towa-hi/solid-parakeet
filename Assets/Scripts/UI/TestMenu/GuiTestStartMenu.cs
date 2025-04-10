using System;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestStartMenu : TestGuiElement
{

    public TMP_InputField contractField;
    public Button setContractButton;
    public TextMeshProUGUI currentContractText;
    public TMP_InputField sneedField;
    public Button setSneedButton;
    public TextMeshProUGUI currentSneedText;
    public TextMeshProUGUI currentAddressText;

    public Button checkInboxButton;
    public Button makeInviteButton;
    public Button cancelButton;
    public Button checkSentInviteButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        contractField.onValueChanged.AddListener(OnContractFieldChanged);
        setContractButton.onClick.AddListener(OnSetContractButton);
        sneedField.onValueChanged.AddListener(OnSneedFieldChanged);
        setSneedButton.onClick.AddListener(OnSetSneedButton);
        checkInboxButton.onClick.AddListener(OnCheckInboxButton);
        makeInviteButton.onClick.AddListener(OnMakeInviteButton);
        cancelButton.onClick.AddListener(OnCancelButton);
        checkSentInviteButton.onClick.AddListener(OnCheckSentInviteButton);
        StellarManagerTest.OnContractIdChanged += OnContractIdChanged;
        StellarManagerTest.OnAccountIdChanged += OnAccountIdChanged;
    }

    public override void Initialize()
    {
        contractField.text = string.Empty;
        sneedField.text = string.Empty;
        Refresh();
    }
    public override void Refresh()
    {
        setContractButton.interactable = StellarManagerTest.stellar.contractId != contractField.text && StrKey.IsValidContractId(contractField.text);
        setSneedButton.interactable = StellarManagerTest.stellar.sneed != sneedField.text && StrKey.IsValidContractId(sneedField.text);
        currentContractText.text = StellarManagerTest.stellar.contractId;
        currentSneedText.text = StellarManagerTest.stellar.sneed;
        currentAddressText.text = StrKey.EncodeStellarAccountId(StellarManagerTest.stellar.userAccount.PublicKey);
    }

    void OnContractFieldChanged(string input)
    {
        Refresh();
    }
    
    void OnSetContractButton()
    {
        StellarManagerTest.SetContractId(contractField.text);
        contractField.text = string.Empty;
    }

    void OnSneedFieldChanged(string input)
    {
        Refresh();
    }
    
    void OnSetSneedButton()
    {
        StellarManagerTest.SetAccountId(sneedField.text);
        sneedField.text = string.Empty;
    }

    void OnCheckInboxButton()
    {
        
    }

    void OnMakeInviteButton()
    {
        
    }

    void OnCancelButton()
    {
        
    }

    void OnCheckSentInviteButton()
    {
        
    }

    void OnContractIdChanged(string contractId)
    {
        
    }

    void OnAccountIdChanged(string accountId)
    {
        
    }
}
