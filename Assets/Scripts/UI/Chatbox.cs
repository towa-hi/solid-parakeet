using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Chatbox : MonoBehaviour
{
    public GameObject frame;
    public Button hideButton;
    public List<GuiMailEntry> entries;
    public TMP_InputField inputField;
    public Button sendButton;
    bool collapsed = false;
    bool updating = false;
    // void Start()
    // {
    //     hideButton.onClick.AddListener(OnHideButton);
    //     sendButton.onClick.AddListener(OnSendButton);
    //     inputField.onValueChanged.AddListener(OnInputChanged);
    // }
    //
    // bool inputValid = false;
    //
    // void OnInputChanged(string text)
    // {
    //     bool valid = true;
    //     if (string.IsNullOrEmpty(text))
    //     {
    //         valid = false;
    //     }
    //     if (text.Length > 20)
    //     {
    //         valid = false;
    //     }
    //     inputValid = valid;
    //     sendButton.interactable = valid;
    // }
    //
    // public string lobby;
    //
    // public void Initialize(bool online, string lobbyId)
    // {
    //     if (online)
    //     {
    //         lobby = lobbyId;
    //         updating = true;
    //         collapsed = false;
    //         frame.SetActive(true);
    //         CheckMail();
    //     }
    //     else
    //     {
    //         collapsed = true;
    //         frame.SetActive(false);
    //     }
    // }
    //
    // float time = 0f;
    // bool checkingMail = false;
    // void Update()
    // {
    //     // if (!updating) return;
    //     // if (checkingMail) return;
    //     // if (collapsed) return;
    //     // if (StellarManagerTest.currentTask != null) return;
    //     // time += Time.deltaTime;
    //     // if (time > 5f)
    //     // {
    //     //     time = 0f;
    //     //     CheckMail();
    //     // }
    // }
    //
    // async void CheckMail()
    // {
    //     // Debug.Log("Checking mail");
    //     // checkingMail = true;
    //     // Mailbox? mailbox = await StellarManagerTest.GetMail(lobby);
    //     // checkingMail = false;
    //     // UpdateMail(mailbox);
    // }
    //
    // void UpdateMail(Mailbox? mailbox)
    // {
    //     Debug.Log("Updating mail");
    //     foreach (var entry in entries)
    //     {
    //         entry.Display(false);
    //     }
    //     if (mailbox.HasValue)
    //     {
    //         // Lobby? currentLobby = StellarManagerTest.currentLobby;
    //         // if (currentLobby != null)
    //         // {
    //         //     for (int i = 0; i < mailbox.Value.mail.Length; i++)
    //         //     {
    //         //     
    //         //         entries[i].Initialize(mailbox.Value.mail[i], currentLobby.Value);
    //         //         entries[i].Display(true);
    //         //     }
    //         // }
    //         
    //     }
    // }
    //
    // async void OnSendButton()
    // {
    //     Debug.Log("Sending mail");
    //     // Mail mail = new Mail()
    //     // {
    //     //     mail_type = 0,
    //     //     message = inputField.text,
    //     //     sender = StellarManagerTest.GetUserAddress(),
    //     //     sent_ledger = 0,
    //     // };
    //     // if (inputValid)
    //     // {
    //     //     inputField.text = "";
    //     //     await StellarManagerTest.SendMail(mail);
    //     //     CheckMail();
    //     // }
    // }
    //
    // void OnHideButton()
    // {
    //     if (collapsed)
    //     {
    //         collapsed = false;
    //         frame.SetActive(true);
    //     }
    //     else
    //     {
    //         collapsed = true;
    //         frame.SetActive(false);
    //     }
    // }
}
