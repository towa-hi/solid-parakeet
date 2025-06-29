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

    public void PhaseChanged(PhaseBase newPhase)
    {
        Initialize(newPhase.cachedNetworkState);
        Refresh(newPhase);
    }

    public void PhaseStateChanged(PhaseBase currentPhase)
    {
        Refresh(currentPhase);
    }
    
    void Initialize(GameNetworkState networkState)
    {
        // Clear existing entries
        foreach (Transform child in rankEntryListRoot) { Destroy(child.gameObject); }
        entries = new Dictionary<Rank, GuiRankListEntry>();
        uint[] maxRanks = networkState.lobbyParameters.max_ranks;
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rank = (Rank)i;
            GuiRankListEntry rankListEntry = Instantiate(rankEntryPrefab, rankEntryListRoot).GetComponent<GuiRankListEntry>();
            entries.Add(rank, rankListEntry);
            rankListEntry.Initialize(rank);
            rankListEntry.SetButtonOnClick(OnEntryClicked);
        }
    }
    
    void Refresh(PhaseBase currentPhase)
    {
        bool show;
        string status = "";
        switch (currentPhase)
        {
            case SetupCommitPhase setupCommitPhase:
                show = true;
                if (setupCommitPhase.cachedNetworkState.IsMySubphase())
                {
                    // update entry counts
                    (Rank, int, int)[] ranksRemaining = setupCommitPhase.RanksRemaining();
                    foreach ((Rank rank, int max, int committed) in ranksRemaining)
                    {
                        bool entrySelected = rank == setupCommitPhase.selectedRank;
                        entries[rank].Refresh(max, committed, entrySelected, true);
                    }
                    bool pawnsComitted = setupCommitPhase.AreAllPawnsComitted();
                    submitButton.interactable = pawnsComitted;
                    clearButton.interactable = true;
                    autoSetupButton.interactable = true;
                    status = "Commit your pawn setup";
                }
                else
                {
                    submitButton.interactable = false;
                    clearButton.interactable = false;
                    autoSetupButton.interactable = false;
                    status = "Awaiting opponent commit...";
                }
                break;
            case SetupProvePhase setupProvePhase:
                show = true;
                submitButton.interactable = false;
                clearButton.interactable = false;
                autoSetupButton.interactable = false;
                if (setupProvePhase.cachedNetworkState.IsMySubphase())
                {
                    status = "awaiting your setup proof";
                }
                else
                {
                    status = "awaiting opponent setup proof";
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
        statusText.text = status;
        ShowElement(show);
    }
}
