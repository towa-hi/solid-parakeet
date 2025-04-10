using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiInviteListEntry : MonoBehaviour
{
    public TextMeshProUGUI label;
    public Button acceptButton;
    public int index;
    public Invite invite;
    public event Action<Invite> OnClick;
    
    public void Initialize(int inIndex, Invite inInvite)
    {
        index = inIndex;
        inInvite = inInvite;
        string labelText = invite.host_address + " - " + invite.parameters.board_def.name;
        label.text = labelText;
        acceptButton.onClick.AddListener(HandleAccept);
    }

    void HandleAccept()
    {
        OnClick?.Invoke(invite);
    }
}
