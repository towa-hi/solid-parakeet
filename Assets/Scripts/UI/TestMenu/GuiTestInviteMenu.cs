using System.Collections.Generic;
using Stellar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestInviteMenu : TestGuiElement
{
    public TMP_Dropdown boardDropdown;
    public Toggle mustFillAllSetupTilesToggle;
    public Toggle securityModeToggle;
    public TMP_InputField hostAddressField;
    public TMP_InputField recipientAddressField;
    public TextMeshProUGUI statusText;
    public Button backButton;
    public Button sendButton;
    
    BoardDef[] boardDefs;

    void Start()
    {
        Debug.Log("started");
        Debug.Log(boardDropdown == null);
        boardDropdown.onValueChanged.AddListener(OnBoardDropdownValueChanged);
        recipientAddressField.onValueChanged.AddListener(OnRecipientAddressFieldValueChanged);
    }
    public override void Initialize()
    {
        ResetBoardDropdown();
        Refresh();
    }
    
    public override void Refresh()
    {
        hostAddressField.text = StrKey.EncodeStellarAccountId(StellarManagerTest.stellar.userAccount.PublicKey);
        bool isBoardValid = false;
        if (boardDropdown.value >= 0 && boardDropdown.value < boardDefs.Length)
        {
            isBoardValid = true;
        }
        if (!isBoardValid)
        {
            statusText.text = "Please select a board";
            sendButton.interactable = false;
            return;
        }
        bool isGuestValid = StellarDotnet.IsValidStellarAddress(recipientAddressField.text);
        if (!isGuestValid)
        {
            statusText.text = "Please enter a valid Guest Address";
            sendButton.interactable = false;
            return;
        }
        sendButton.interactable = true;

    }

    void ResetBoardDropdown()
    {
        boardDropdown.ClearOptions();
        boardDefs = Resources.LoadAll<BoardDef>("Boards");
        List<string> options = new List<string>();
        foreach (BoardDef board in boardDefs)
        {
            options.Add(board.name);
        }
        boardDropdown.AddOptions(options);
        boardDropdown.RefreshShownValue();
    }
    
    void OnBoardDropdownValueChanged(int index)
    {
        Refresh();
    }
    
    void OnRecipientAddressFieldValueChanged(string input)
    {
        Refresh();
    }

    void OnBackButton()
    {
        
    }

    void OnSendButton()
    {
        
    }

    public InviteMenuParameters GetInviteMenuParameters()
    {
        return new InviteMenuParameters
        {
            boardDef = boardDefs[boardDropdown.value],
            mustFillAllSetupTiles = mustFillAllSetupTilesToggle.isOn,
            securityMode = securityModeToggle.isOn,
            hostAddress = hostAddressField.text,
            guestAddress = recipientAddressField.text,
        };
    }
}

public struct InviteMenuParameters
{
    public BoardDef boardDef;
    public bool mustFillAllSetupTiles;
    public bool securityMode;
    public string hostAddress;
    public string guestAddress;
}