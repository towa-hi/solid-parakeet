using System;
using System.Collections.Generic;
using System.Linq;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiTestSetup : GameElement
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
    
    public override void Initialize(TestBoardManager inBoardManager, GameNetworkState networkState)
    {
        base.Initialize(inBoardManager, networkState);
        // Clear existing entries
        // foreach (Transform child in rankEntryListRoot.transform) { Destroy(child.gameObject); }
        // entries = new Dictionary<Rank, GuiRankListEntry>();
        //
        // MaxPawns[] maxPawns = lobby.parameters.max_pawns;
        // foreach (MaxPawns maxPawn in maxPawns)
        // {
        //     GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
        //     entries.Add((Rank)maxPawn.rank, rankListEntry);
        //     rankListEntry.Initialize(maxPawn);
        //     rankListEntry.SetButtonOnClick(OnEntryClicked);
        // }
    }
    
    public void Refresh(SetupClientState state)
    {
        Debug.Log("GuiTestSetup Refresh");
        Dictionary<Rank, List<PawnCommit>> orderedCommitments = new();
        foreach (Rank rank in Enum.GetValues(typeof(Rank)))
        {
            orderedCommitments.Add(rank, new List<PawnCommit>());
        }
        foreach (PawnCommit commitment in state.commitments.Values)
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
            foreach (PawnCommit commitment in orderedCommitments[rank])
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
            bool isSelected = state.selectedRank.HasValue && state.selectedRank.Value == rank;
            entries[rank].Refresh(rank, remainingCount, isSelected);
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
