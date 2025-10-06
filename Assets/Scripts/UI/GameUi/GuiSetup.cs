using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiSetup : GameElement
{
    public Transform rankEntryListRoot;

    public Button clearButton;
    public Button autoSetupButton;
    public Button submitButton;
    public TextMeshProUGUI statusText;
    public GameObject rankEntryPrefab;
    public Dictionary<Rank, GuiRankListEntry> entries;
    Dictionary<Rank, int> usedCounts = new();

    public Action OnClearButton;
    public Action OnAutoSetupButton;
    public Action OnRefreshButton;
    public Action OnSubmitButton;
    public Action<Rank> OnEntryClicked;
    
    public SetupScreen setupScreen;

    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
        entries = new();
    }

    // Subscriptions controlled by BoardManager lifecycle, not Unity enable/disable
    public void AttachSubscriptions()
    {
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnStateUpdated -= HandleStateUpdated;
    }

    public override void OnClientModeChanged(GameSnapshot snapshot)
    {
        if (snapshot.Mode != ClientMode.Setup) {
            return;
        };
        Reset(snapshot.Net);
    }

    public override void Reset(GameNetworkState net)
    {
        setupScreen.Uninitialize();
        foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
        entries = new();
        uint[] maxRanks = net.lobbyParameters.max_ranks;
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rank = (Rank)i;
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(rank, rankListEntry);
            rankListEntry.Initialize(rank);
            entries[rank].Refresh((int)maxRanks[i], 0, false, true);
        }
        setupScreen.Initialize(net);
        setupScreen.OnCardRankClicked = (rank) => OnCardClicked(rank);
        if (setupScreen.isActiveAndEnabled)
        {
            setupScreen.PlayOpenAnimation();
        }
        // do rankrefresh on setupscreen from maxpawns
        List<(Rank, int, int)> rankList = new();
        for (int i = 0; i < maxRanks.Length; i++)
        {
            rankList.Add(((Rank)i, (int)maxRanks[i], 0));
        }
        setupScreen.RefreshFromRanks(rankList.ToArray());
        // reset local trackers
        usedCounts.Clear();
    }

    public override void Refresh(GameSnapshot snapshot)
    {
        GameNetworkState net = snapshot.Net;
        LocalUiState ui = snapshot.Ui;
        bool isMyTurn = net.IsMySubphase();
        bool waitingForResponse = ui.WaitingForResponse is not null;
        bool canCommitSetup = isMyTurn && !waitingForResponse;

        string statusMessage = "Commit your setup";
        if (waitingForResponse)
        {
            statusMessage = "Submitting setup...";
        }
        else if (!isMyTurn)
        {
            statusMessage = "Awaiting opponent setup commitment";
        }
        autoSetupButton.interactable = canCommitSetup;
        submitButton.interactable = canCommitSetup;
        clearButton.interactable = canCommitSetup;
        statusText.text = statusMessage;

        uint[] maxRanks = net.lobbyParameters.max_ranks;


        var ranksArray = new (Rank, int, int)[maxRanks.Length];
        bool allFilled = true;
        bool anyUsed = false;
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rk = (Rank)i;
            int max = (int)maxRanks[i];
            int used = usedCounts.TryGetValue(rk, out int u) ? u : 0;
            ranksArray[i] = (rk, max, used);
            allFilled &= used == max;
            anyUsed |= used > 0;
            if (entries != null && entries.TryGetValue(rk, out GuiRankListEntry entry))
            {
                bool interactable = canCommitSetup && used < max;
                bool selected = ui.SelectedRank == rk;
                entry.Refresh(max, used, selected, interactable);
            }
        }
        setupScreen.RefreshFromRanks(ranksArray);
    }

    
    void OnCardClicked(Rank rank)
    {
        OnEntryClicked?.Invoke(rank);
    }

    void HandleStateUpdated(GameSnapshot snapshot)
    {
        Refresh(snapshot);
    }

}
