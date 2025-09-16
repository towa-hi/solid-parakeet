using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Contract;

public class LobbyViewMenu2 : MenuBase
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
    public GameObject maxPawnEntryPrefab;
    HashSet<GuiMaxPawnListEntry> entries = new HashSet<GuiMaxPawnListEntry>();

    public Button enterGameButton;
    public Button backButton;
    public Button refreshButton;

    private void Start()
    {
        enterGameButton.onClick.AddListener(HandleEnterGame);
        backButton.onClick.AddListener(HandleBack);
        refreshButton.onClick.AddListener(HandleRefresh);

        if (copyContractAddressButton != null) copyContractAddressButton.onClick.AddListener(CopyContractAddress);
        if (copyLobbyIdButton != null) copyLobbyIdButton.onClick.AddListener(CopyLobbyId);
        if (copyHostAddressButton != null) copyHostAddressButton.onClick.AddListener(CopyHostAddress);
        if (copyGuestAddressButton != null) copyGuestAddressButton.onClick.AddListener(CopyGuestAddress);
    }

    public void HandleEnterGame()
    {
        EmitAction(MenuAction.GotoGame);
    }

    public void HandleBack()
    {
        EmitAction(MenuAction.GotoMainMenu);
    }

    public void HandleRefresh()
    {
        EmitAction(MenuAction.Refresh);
    }
    public override void Refresh()
    {
        // Clear previous entries
        foreach (var entry in entries)
        {
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }
        entries.Clear();

        if (contractAddressText != null)
        {
            contractAddressText.text = StellarManager.networkContext.contractAddress;
        }

        LobbyInfo? mLobbyInfo = StellarManager.networkState.lobbyInfo;
        LobbyParameters? mLobbyParams = StellarManager.networkState.lobbyParameters;

        if (!mLobbyInfo.HasValue)
        {
            if (contractAddressText != null) contractAddressText.text = "No lobby";
            if (lobbyIdText != null) lobbyIdText.text = "No lobby";
            if (hostAddressText != null) hostAddressText.text = "No lobby";
            if (guestAddressText != null) guestAddressText.text = "No lobby";
            if (boardNameText != null) boardNameText.text = "No lobby";
            if (mustFillAllSetupTilesToggle != null) mustFillAllSetupTilesToggle.SetIsOnWithoutNotify(false);
            if (securityModeToggle != null) securityModeToggle.SetIsOnWithoutNotify(false);
            return;
        }

        LobbyInfo lobbyInfo = mLobbyInfo.Value;
        if (lobbyIdText != null) lobbyIdText.text = lobbyInfo.index.ToString();
        if (hostAddressText != null)
        {
            if (lobbyInfo.host_address != null)
            {
                hostAddressText.text = lobbyInfo.host_address.Value;
            }
            else hostAddressText.text = string.Empty;
        }
        if (guestAddressText != null)
        {
            if (lobbyInfo.guest_address != null)
            {
                guestAddressText.text = lobbyInfo.guest_address.Value;
            }
            else guestAddressText.text = string.Empty;
        }

        if (mLobbyParams.HasValue)
        {
            LobbyParameters lobbyParams = mLobbyParams.Value;
            if (boardNameText != null) boardNameText.text = lobbyParams.board.name;
            if (mustFillAllSetupTilesToggle != null) mustFillAllSetupTilesToggle.SetIsOnWithoutNotify(lobbyParams.must_fill_all_tiles);
            if (securityModeToggle != null) securityModeToggle.SetIsOnWithoutNotify(lobbyParams.security_mode);

            // Rebuild max pawns list
            if (maxPawnListRoot != null && maxPawnEntryPrefab != null && lobbyParams.max_ranks != null)
            {
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
                {
                    int idx = (int)rank;
                    if (idx < 0 || idx >= lobbyParams.max_ranks.Length) continue;
                    uint max = lobbyParams.max_ranks[idx];
                    GameObject entryObj = Instantiate(maxPawnEntryPrefab, maxPawnListRoot);
                    GuiMaxPawnListEntry entry = entryObj.GetComponent<GuiMaxPawnListEntry>();
                    if (entry != null)
                    {
                        entry.Initialize(rank, max);
                        entries.Add(entry);
                    }
                }
            }
        }
    }

    void CopyLobbyId()
    {
        if (lobbyIdText != null && !string.IsNullOrEmpty(lobbyIdText.text))
        {
            GUIUtility.systemCopyBuffer = lobbyIdText.text;
        }
    }

    void CopyContractAddress()
    {
        if (contractAddressText != null && !string.IsNullOrEmpty(contractAddressText.text))
        {
            GUIUtility.systemCopyBuffer = contractAddressText.text;
        }
    }

    void CopyHostAddress()
    {
        if (hostAddressText != null && !string.IsNullOrEmpty(hostAddressText.text))
        {
            GUIUtility.systemCopyBuffer = hostAddressText.text;
        }
    }

    void CopyGuestAddress()
    {
        if (guestAddressText != null && !string.IsNullOrEmpty(guestAddressText.text))
        {
            GUIUtility.systemCopyBuffer = guestAddressText.text;
        }
    }
}


