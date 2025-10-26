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
    public Button menuButton;
    public TextMeshProUGUI statusText;
    public GameObject rankEntryPrefab;
    public Dictionary<Rank, GuiRankListEntry> entries;

    public Action OnClearButton;
    public Action OnAutoSetupButton;
    public Action OnRefreshButton;
    public Action OnSubmitButton;
    public Action OnMenuButton;
    public Action<Rank> OnEntryClicked;
    
    public SetupScreen setupScreen;

    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
        menuButton.onClick.AddListener(() => OnMenuButton?.Invoke());
        entries = new();
    }

    // Subscriptions controlled by BoardManager lifecycle, not Unity enable/disable
    public override void AttachSubscriptions()
    {
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    public override void DetachSubscriptions()
    {
        ViewEventBus.OnStateUpdated -= HandleStateUpdated;
    }

    public override void OnClientModeChanged(GameSnapshot snapshot)
    {
        Reset(snapshot.Net);
    }

    public override void Reset(GameNetworkState net)
    {
        Debug.Log($"GuiSetup.Reset: net={net}");
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
    }

    public override void Refresh(GameSnapshot snapshot)
    {
        GameNetworkState net = snapshot.Net;
        Debug.Log($"GuiSetup.Refresh: snapshot={snapshot}");
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
        clearButton.interactable = canCommitSetup;
        statusText.text = statusMessage;

        uint[] maxRanks = net.lobbyParameters.max_ranks;

		// Filter out null ranks before grouping to avoid null keys in dictionary
		var usedCounts = ui.PendingCommits.Values
			.Where(v => v.HasValue)
			.Select(v => v.Value)
			.GroupBy(r => r)
			.ToDictionary(g => g.Key, g => g.Count());

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
        submitButton.interactable = canCommitSetup && allFilled;
        if (canCommitSetup && !allFilled)
        {
            statusText.text = "Assign all ranks before submitting";
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
