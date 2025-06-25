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
    public event Action<Rank> OnRankEntryClicked;
    
    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }
    
    public override void Initialize(BoardManager inBoardManager, GameNetworkState networkState)
    {
        base.Initialize(inBoardManager, networkState);
        // Clear existing entries
        foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
        entries = new Dictionary<Rank, GuiRankListEntry>();
        
        uint[] maxRanks = networkState.lobbyParameters.max_ranks;
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
        OnRankEntryClicked?.Invoke(clickedEntry.rank);
    }
}
