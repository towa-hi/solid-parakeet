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
        foreach (Transform child in rankEntryListRoot.transform) { Destroy(child.gameObject); }
        entries = new Dictionary<Rank, GuiRankListEntry>();
        
        MaxRank[] maxRanks = networkState.lobbyParameters.max_ranks;
        foreach (MaxRank maxRank in maxRanks)
        {
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(maxRank.rank, rankListEntry);
            rankListEntry.Initialize(maxRank);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
        }
    }
    
    public void Refresh(SetupClientState state)
    {
        Debug.Log("GuiTestSetup Refresh");
        foreach (KeyValuePair<Rank, uint> kvp in state.maxRanks)
        {
            
        }
        bool pawnsRemaining = false;
        foreach (GuiRankListEntry entry in entries.Values)
        {
            int remaining = state.GetPendingRemainingCount(entry.rank);
            int lockedCommitsOfThisRank = state.lockedCommits.Count(c => CacheManager.LoadHiddenRank(c.hidden_rank_hash).rank == entry.rank);
            int remainingUncommitted = remaining - lockedCommitsOfThisRank;
            bool isSelected = state.selectedRank.HasValue && state.selectedRank.Value == entry.rank;
            entry.Refresh(entry.rank, remainingUncommitted, isSelected);
            if (remainingUncommitted > 0)
            {
                pawnsRemaining = true;
            }
        }
        submitButton.interactable = !state.committed && !pawnsRemaining;
        autoSetupButton.interactable = !state.committed;
        clearButton.interactable = !state.committed;
        string status;
        if (state.committed)
        {
            status = "Waiting for opponent... Click refresh to check";
        }
        else
        {
            if (pawnsRemaining)
            {
                status = "Please commit all pawns";
            }
            else
            {
                status = "Click submit to continue";
            }
        }
        statusText.text = status;
    }
    
    void OnEntryClicked(GuiRankListEntry clickedEntry)
    {
        Debug.Log("clicked");
        OnRankEntryClicked?.Invoke(clickedEntry.rank);
    }
}
