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

    Action OnClearButton;
    Action OnAutoSetupButton;
    Action OnRefreshButton;
    Action OnSubmitButton;
    Action<Rank> OnEntryClicked;
    
    void Start()
    {
        clearButton.onClick.AddListener(() => OnClearButton?.Invoke());
        autoSetupButton.onClick.AddListener(() => OnAutoSetupButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }

    public void PhaseChanged(PhaseBase newPhase)
    {
        Initialize(newPhase.cachedNetworkState, true);
        Refresh(newPhase);
    }

    public void PhaseStateChanged(PhaseBase currentPhase)
    {
        Refresh(currentPhase);
    }
    
    void Initialize(GameNetworkState networkState, bool allInteractable)
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
                // TODO: make this not shit
                // update entry counts
                (Rank, int, int)[] ranksRemaining = setupCommitPhase.RanksRemaining();
                foreach ((Rank rank, int max, int committed) in ranksRemaining)
                {
                    bool entrySelected = rank == setupCommitPhase.selectedRank;
                    entries[rank].Refresh(max, committed, entrySelected);
                }
                switch (setupCommitPhase.cachedNetworkState.GetRelativeSubphase())
                {
                    case RelativeSubphase.MYSELF:
                    case RelativeSubphase.BOTH:
                        bool pawnsRemaining = setupCommitPhase.ArePawnsRemaining();
                        submitButton.interactable = !pawnsRemaining;
                        clearButton.interactable = true;
                        autoSetupButton.interactable = true;
                        status = "Commit your pawn setup";
                        break;
                    case RelativeSubphase.OPPONENT:
                        submitButton.interactable = false;
                        clearButton.interactable = false;
                        autoSetupButton.interactable = false;
                        status = "Awaiting opponent commit...";
                        break;
                    case RelativeSubphase.NONE:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                break;
            case SetupProvePhase setupProvePhase:
                show = true;
                submitButton.interactable = false;
                clearButton.interactable = false;
                autoSetupButton.interactable = false;
                switch (setupProvePhase.cachedNetworkState.GetRelativeSubphase())
                {
                    case RelativeSubphase.MYSELF:
                    case RelativeSubphase.BOTH:
                        status = "awaiting your setup proof";
                        break;
                    case RelativeSubphase.OPPONENT:
                        status = "awaiting opponent setup proof";
                        break;
                    case RelativeSubphase.NONE:
                    default:
                        throw new ArgumentOutOfRangeException();
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


    public void SetActions(
        Action onClear = null, 
        Action onAutoSetup = null, 
        Action onRefresh = null, 
        Action onSubmit = null, 
        Action<Rank> onEntryClicked = null)
    {
        OnClearButton = onClear;
        OnAutoSetupButton = onAutoSetup;
        OnRefreshButton = onRefresh;
        OnSubmitButton = onSubmit;
        OnEntryClicked = onEntryClicked;
    }
    
}
