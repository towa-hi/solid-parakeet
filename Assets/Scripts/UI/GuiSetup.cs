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

    public event Action OnClearButton;
    public event Action OnAutoSetupButton;
    public event Action OnRefreshButton;
    public event Action OnSubmitButton;
    public event Action<Rank?> OnRankSelected;
    
    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }

    public void Refresh(IPhase currentPhase)
    {
        bool show;
        switch (currentPhase)
        {
            case SetupCommitPhase setupCommitPhase:
                show = true;
                if (!isVisible)
                {
                    Initialize(setupCommitPhase);
                }
                break;
            case SetupProvePhase setupProvePhase:
                show = true;
                if (!isVisible)
                {
                    Initialize(setupProvePhase);
                }
                break;
            case MoveCommitPhase moveCommitPhase:
            case MoveProvePhase moveProvePhase:
            case RankProvePhase rankProvePhase:
                show = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));
        }

        
        ShowElement(show);
    }

    void Initialize(SetupCommitPhase setupCommitPhase)
    {
        SetRankEntries(setupCommitPhase.setupCommitData.lobbyParameters.max_ranks);
        // Clear existing entries
        foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
        entries = new Dictionary<Rank, GuiRankListEntry>();
        
    }

    void Initialize(SetupProvePhase setupProvePhase)
    {
        // SetRankEntries()
    }

    void SetRankEntries(uint[] maxRanks)
    {
        foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
        entries = new Dictionary<Rank, GuiRankListEntry>();
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rank = (Rank)i;
            uint max = maxRanks[i];
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(rank, rankListEntry);
            rankListEntry.Initialize(rank, max);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
        }
    }
    public void SetActions(
        Action onClear = null, 
        Action onAutoSetup = null, 
        Action onRefresh = null, 
        Action onSubmit = null, 
        Action<Rank?> onRankSelected = null)
    {
        OnClearButton = onClear;
        OnAutoSetupButton = onAutoSetup;
        OnRefreshButton = onRefresh;
        OnSubmitButton = onSubmit;
        OnRankSelected = onRankSelected;
    }
    
    // public override void Initialize(BoardManager inBoardManager, GameNetworkState networkState)
    // {
    //     base.Initialize(inBoardManager, networkState);
    //     // Clear existing entries
    //     foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
    //     entries = new Dictionary<Rank, GuiRankListEntry>();
    //     
    //     uint[] maxRanks = networkState.lobbyParameters.max_ranks;
    //     for (int i = 0; i < maxRanks.Length; i++)
    //     {
    //         Rank rank = (Rank)i;
    //         uint max = maxRanks[i];
    //         GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
    //         entries.Add(rank, rankListEntry);
    //         rankListEntry.Initialize(rank, max);
    //         rankListEntry.SetButtonOnClick(OnEntryClicked);
    //     }
    // }

    // public void Refresh(SetupClientState state)
    // {
    //     Debug.Log("GuiTestSetup Refresh");
    //     bool pawnsRemaining = false;
    //     foreach (GuiRankListEntry entry in entries.Values)
    //     {
    //         int remaining = state.GetPendingRemainingCount(entry.rank);
    //         int lockedCommitsOfThisRank = state.lockedCommits.Count(c => CacheManager.LoadHiddenRank(c.hidden_rank_hash).rank == entry.rank);
    //         int remainingUncommitted = remaining - lockedCommitsOfThisRank;
    //         bool isSelected = state.selectedRank.HasValue && state.selectedRank.Value == entry.rank;
    //         entry.Refresh(entry.rank, remainingUncommitted, isSelected);
    //         if (remainingUncommitted > 0)
    //         {
    //             pawnsRemaining = true;
    //         }
    //     }
    //     submitButton.interactable = !state.committed && !pawnsRemaining;
    //     autoSetupButton.interactable = !state.committed;
    //     clearButton.interactable = !state.committed;
    //     string status;
    //     if (state.committed)
    //     {
    //         status = "Waiting for opponent... Click refresh to check";
    //     }
    //     else
    //     {
    //         if (pawnsRemaining)
    //         {
    //             status = "Please commit all pawns";
    //         }
    //         else
    //         {
    //             status = "Click submit to continue";
    //         }
    //     }
    //     statusText.text = status;
    // }
    
    void OnEntryClicked(GuiRankListEntry clickedEntry)
    {
        Debug.Log("clicked");
        // OnRankEntryClicked?.Invoke(clickedEntry.rank);
    }
}
