using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestSetup : GameElement
{
    public Transform rankEntryListRoot;

    public Button clearButton;
    public Button autoSetupButton;
    public Button deleteButton;
    public Button submitButton;
    
    public GameObject rankEntryPrefab;
    public List<GuiRankListEntry> entries;
    public GuiRankListEntry selectedRankEntry;

    public event Action OnClearButton;
    public event Action OnAutoSetupButton;
    public event Action OnDeleteButton;
    public event Action OnSubmitButton;

    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        deleteButton.onClick.AddListener(() => OnDeleteButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }
    
    public override void Initialize(TestBoardManager boardManager)
    {
        // Clear existing entries
        foreach (Transform child in rankEntryListRoot.transform) { Destroy(child.gameObject); }
        entries.Clear();
        selectedRankEntry = null;
        
        // Get unique ranks from pawns that belong to the user's team
        HashSet<Rank> userTeamRanks = new HashSet<Rank>();
        List<Pawn> myPawns = boardManager.GetMyPawns();
        foreach (var pawn in myPawns)
        {
            userTeamRanks.Add(pawn.def.rank);
        }
        
        // Create entries for each rank that belongs to the user's team
        foreach (var rank in userTeamRanks)
        {
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(rankListEntry);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
            rankListEntry.rank = rank;
        }
        
        // Refresh to update counts
        Refresh(boardManager);
    }

    public override void Refresh(TestBoardManager boardManager)
    {
        // Count dead pawns of each rank that belong to the user's team
        Dictionary<Rank, int> deadPawnCounts = new Dictionary<Rank, int>();
        
        // Initialize counts for all ranks to 0
        foreach (var entry in entries)
        {
            deadPawnCounts[entry.rank] = 0;
        }
        
        // Count pawns that are not alive (dead) for the user's team
        List<Pawn> myPawns = boardManager.GetMyPawns();
        foreach (var pawn in myPawns)
        {
            if (!pawn.isAlive)
            {
                Rank rank = pawn.def.rank;
                if (deadPawnCounts.ContainsKey(rank))
                {
                    deadPawnCounts[rank]++;
                }
            }
        }
        
        // Update UI entries
        foreach (var entry in entries)
        {
            int deadCount = deadPawnCounts.ContainsKey(entry.rank) ? deadPawnCounts[entry.rank] : 0;
            bool isSelected = selectedRankEntry != null && selectedRankEntry.rank == entry.rank;
            entry.Refresh(entry.rank, deadCount, isSelected);
        }
    }
    
    public void OnEntryClicked(GuiRankListEntry clickedEntry)
    {
        // Toggle the selected rank
        if (selectedRankEntry == clickedEntry)
        {
            // If clicking the same rank again, deselect it
            selectedRankEntry = null;
        }
        else
        {
            // Select this rank
            selectedRankEntry = clickedEntry;
        }
        
        // Update the UI to reflect the selection
        Refresh(GameManager.instance.testBoardManager);
    }
}
