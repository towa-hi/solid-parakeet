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

    public void Refresh(Lobby? lob)
    {
        foreach (GuiMaxPawnListEntry entry in entries)
        {
            Destroy(entry.gameObject);
        }
        entries.Clear();
        if (!lob.HasValue)
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
        Lobby lobby = lob.Value;
        contractAddressText.text = StellarManagerTest.GetContractAddress();
        lobbyIdText.text = lobby.index;
        hostAddressText.text = lobby.host_address;
        guestAddressText.text = lobby.guest_address;
        boardNameText.text = lobby.parameters.board_def_name;
        mustFillAllSetupTilesToggle.SetIsOnWithoutNotify(lobby.parameters.must_fill_all_tiles);
        securityModeToggle.SetIsOnWithoutNotify(lobby.parameters.security_mode);
        
        foreach (MaxPawns maxPawn in lobby.parameters.max_pawns)
        {
            GameObject entryObject = Instantiate(maxPawnEntryPrefab, maxPawnListRoot);
            GuiMaxPawnListEntry entry = entryObject.GetComponent<GuiMaxPawnListEntry>();
            entries.Add(entry);
            entry.Initialize(maxPawn);
        }

    }
}
