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
    public Button refreshButton;
    public Button submitButton;
    public TextMeshProUGUI statusText;
    public GameObject rankEntryPrefab;
    public Dictionary<Rank, GuiRankListEntry> entries;

    public Action OnClearButton;
    public Action OnAutoSetupButton;
    public Action OnRefreshButton;
    public Action OnSubmitButton;
    public Action<Rank> OnEntryClicked;
    
    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }

    void Initialize(GameNetworkState netState)
    {
        // Clear existing entries
        foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
        entries = new();
        uint[] maxRanks = netState.lobbyParameters.max_ranks;
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rank = (Rank)i;
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(rank, rankListEntry);
            rankListEntry.Initialize(rank);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
        }
        
    }


    public void PhaseStateChanged(PhaseBase phase, IPhaseChangeSet changes)
    {

        if (changes.NetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            string status = "";
            Initialize(netStateUpdated.netState);
            bool showElement = false;
            switch (phase)
            {
                case SetupCommitPhase setupCommitPhase:
                    showElement = true;
                    if (netStateUpdated.netState.IsMySubphase())
                    {
                        status = "Commit your pawn setup";
                    }
                    else
                    {
                        status = "Awaiting opponent setup";
                    }
                    break;
                case MoveCommitPhase moveCommitPhase:
                case MoveProvePhase moveProvePhase:
                case RankProvePhase rankProvePhase:
                    showElement = false;
                    break;
            }
            ShowElement(showElement);
        }

        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupRankCommitted(_, var setupCommitPhase) setupRankCommitted:
                    RefreshRankEntryList(setupCommitPhase.RanksRemaining(), setupCommitPhase.selectedRank);
                    break;
                case SetupRankSelected(_,var setupCommitPhase) setupRankSelected:
                    RefreshRankEntryList(setupCommitPhase.RanksRemaining(), setupCommitPhase.selectedRank);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));

            }
        }
        
    }
    
    void RefreshRankEntryList((Rank rank, int max, int committed)[] ranksRemaining, Rank? selectedRank)
    {
        foreach ((Rank rank, int max, int committed) in ranksRemaining)
        {
            bool entrySelected = rank == selectedRank;
            entries[rank].Refresh(max, committed, entrySelected, true);
        }
        bool pawnsComitted = ranksRemaining.Any(e => e.max - e.committed != 0);
        submitButton.interactable = pawnsComitted;
        clearButton.interactable = true;
        autoSetupButton.interactable = true;
    }
}
