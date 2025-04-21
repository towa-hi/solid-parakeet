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
    public Dictionary<Rank, GuiRankListEntry> entries;

    public event Action OnClearButton;
    public event Action OnAutoSetupButton;
    public event Action OnDeleteButton;
    public event Action OnSubmitButton;
    public event Action<Rank> OnRankEntryClicked;
    
    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        deleteButton.onClick.AddListener(() => OnDeleteButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }
    
    public override void Initialize(TestBoardManager inBoardManager, Lobby lobby)
    {
        base.Initialize(inBoardManager, lobby);
        // Clear existing entries
        foreach (Transform child in rankEntryListRoot.transform) { Destroy(child.gameObject); }
        entries = new Dictionary<Rank, GuiRankListEntry>();
        
        MaxPawns[] maxPawns = lobby.parameters.max_pawns;
        foreach (MaxPawns maxPawn in maxPawns)
        {
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add((Rank)maxPawn.rank, rankListEntry);
            rankListEntry.Initialize(maxPawn);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
        }
    }
    
    public void Refresh(SetupTestPhase phase)
    {
        Dictionary<Rank, List<PawnCommitment>> orderedCommitments = new();
        foreach (Rank rank in Enum.GetValues(typeof(Rank)))
        {
            orderedCommitments.Add(rank, new List<PawnCommitment>());
        }
        foreach (PawnCommitment commitment in phase.commitments.Values)
        {
            PawnDef def = Globals.FakeHashToPawnDef(commitment.pawn_def_hash);
            Rank rank = def.rank;
            orderedCommitments[rank].Add(commitment);
        }

        bool pawnsRemaining = false;
        foreach (Rank rank in orderedCommitments.Keys)
        {
            int max = 0;
            int usedCount = 0;
            foreach (PawnCommitment commitment in orderedCommitments[rank])
            {
                if (commitment.starting_pos.ToVector2Int() != Globals.Purgatory)
                {
                    usedCount++;
                }
                max++;
            }
            int remainingCount = max - usedCount;
            if (remainingCount > 0)
            {
                pawnsRemaining = true;
            }
            bool isSelected = phase.selectedRank.HasValue && phase.selectedRank.Value == rank;
            entries[rank].Refresh(rank, remainingCount, isSelected);
        }
        submitButton.interactable = !pawnsRemaining;
    }
    
    void OnEntryClicked(GuiRankListEntry clickedEntry)
    {
        Debug.Log("clicked");
        OnRankEntryClicked?.Invoke(clickedEntry.rank);
    }
}
