using System.Collections.Generic;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiLobbyView : MonoBehaviour
{
    public TextMeshProUGUI contractAddressText;
    public Button copyContractAddressButton;
    public TextMeshProUGUI lobbyIdText;
    public Button copyLobbyIdButton;
    public TextMeshProUGUI hostAddressText;
    public Button copyHostAddressButton;
    public TextMeshProUGUI guestAddressText;
    public Button copyGuestAddressButton;
    public TextMeshProUGUI boardNameText;
    public Toggle mustFillAllSetupTilesToggle;
    public Toggle securityModeToggle;
    
    public Transform maxPawnListRoot;
    HashSet<GuiMaxPawnListEntry> entries = new HashSet<GuiMaxPawnListEntry>();
    public GameObject maxPawnEntryPrefab;

    void Start()
    {
        copyContractAddressButton.onClick.AddListener(() => CopyContractAddress());
        copyLobbyIdButton.onClick.AddListener(() => CopyLobbyId());
        copyHostAddressButton.onClick.AddListener(() => CopyHostAddress());
        copyGuestAddressButton.onClick.AddListener(() => CopyGuestAddress());
    }

    void CopyLobbyId()
    {
        if (!string.IsNullOrEmpty(lobbyIdText.text))
        {
            GUIUtility.systemCopyBuffer = lobbyIdText.text;
        }
    }

    void CopyContractAddress()
    {
        if (!string.IsNullOrEmpty(contractAddressText.text))
        {
            GUIUtility.systemCopyBuffer = contractAddressText.text;
        }
    }

    void CopyHostAddress()
    {
        if (!string.IsNullOrEmpty(hostAddressText.text))
        {
            GUIUtility.systemCopyBuffer = hostAddressText.text;
        }
    }

    void CopyGuestAddress()
    {
        if (!string.IsNullOrEmpty(guestAddressText.text))
        {
            GUIUtility.systemCopyBuffer = guestAddressText.text;
        }
    }
    
    public void Refresh(LobbyInfo? mLobbyInfo)
    {
        foreach (GuiMaxPawnListEntry entry in entries)
        {
            Destroy(entry.gameObject);
        }
        entries.Clear();
        contractAddressText.text = StellarManager.GetContractAddress();
        if (!mLobbyInfo.HasValue)
        {
            contractAddressText.text = "No lobby";
            lobbyIdText.text = "No lobby";
            hostAddressText.text = "No lobby";
            guestAddressText.text = "No lobby";
            boardNameText.text = "No lobby";
            mustFillAllSetupTilesToggle.SetIsOnWithoutNotify(false);
            securityModeToggle.SetIsOnWithoutNotify(false);
            return;
        }
        LobbyInfo lobbyInfo = mLobbyInfo.Value;
        lobbyIdText.text = lobbyInfo.index.ToString();
        if (lobbyInfo.host_address != null)
        {
            hostAddressText.text = lobbyInfo.host_address.Value;
        }
        else
        {
            hostAddressText.text = string.Empty;
        }

        if (lobbyInfo.guest_address != null)
        {
            guestAddressText.text = lobbyInfo.guest_address.Value;
        }
        else
        {
            guestAddressText.text = string.Empty;
        }
        //boardNameText.text = lobbyInfo.parameters.board_def_name;
        //mustFillAllSetupTilesToggle.SetIsOnWithoutNotify(lobby.parameters.must_fill_all_tiles);
        //securityModeToggle.SetIsOnWithoutNotify(lobby.parameters.security_mode);
        //
        // foreach (MaxPawns maxPawn in lobby.parameters.max_pawns)
        // {
        //     GameObject entryObject = Instantiate(maxPawnEntryPrefab, maxPawnListRoot);
        //     GuiMaxPawnListEntry entry = entryObject.GetComponent<GuiMaxPawnListEntry>();
        //     entries.Add(entry);
        //     entry.Initialize(maxPawn);
        // }

    }
}
