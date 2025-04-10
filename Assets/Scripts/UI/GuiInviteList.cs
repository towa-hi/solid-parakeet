using System;
using System.Collections.Generic;
using Contract;
using UnityEngine;

public class GuiInviteList : MonoBehaviour
{
    public GameObject root;
    public GameObject inviteEntryPrefab;
    public event Action<Invite> OnInviteAccepted;
    
    public void Initialize(List<Invite> invites)
    {
        // clear all children
        foreach (GameObject ob in root.transform)
        {
            GameObject.Destroy(ob);
        }

        int index = 0;
        foreach (Invite invite in invites)
        {
            GameObject inviteEntry = Instantiate(inviteEntryPrefab, root.transform);
            GuiInviteListEntry entry = inviteEntry.GetComponent<GuiInviteListEntry>();
            entry.Initialize(index, invite);
            entry.OnClick += OnInviteClicked;
            index++;
        }
    }

    void OnInviteClicked(Invite invite)
    {
        OnInviteAccepted?.Invoke(invite);
    }
}
