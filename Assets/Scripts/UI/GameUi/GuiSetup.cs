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
    
    public SetupScreen setupScreen;

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
            entries[rank].Refresh((int)maxRanks[i], 0, false, true);
        }
        setupScreen.Initialize(netState);
        setupScreen.OnCardRankClicked = OnEntryClicked;
    }

    void OnDisable()
    {
        setupScreen.Uninitialize();
    }
    public override void PhaseStateChanged(PhaseChangeSet changes)
    {
        // Handle setup UI only during SetupCommitPhase; otherwise ensure uninitialized
        GameNetworkState? setInitialize = null;
        ((Rank, int, int)[], Rank?)? setRefreshEntries = null;
        string setStatus = "";
        bool? setAutoSetupButton = null;
        bool? setSubmitButton = null;
        bool? setClearButton = null;
        // figure out what to do based on what happened
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            bool isSetupPhase = netStateUpdated.phase is SetupCommitPhase;
            if (isSetupPhase)
            {
                setInitialize = cachedNetState;
            }
            else
            {
                // Leaving setup: make sure 3D setup is uninitialized and hidden
                if (setupScreen != null)
                {
                    setupScreen.Uninitialize();
                }
                // Early-out: no further setup UI changes needed in other phases
                return;
            }
            switch (netStateUpdated.phase)
            {
                case SetupCommitPhase:
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your pawn setup";
                        setAutoSetupButton = true;
                        setSubmitButton = false; 
                        setClearButton = false; 
                    }
                    else
                    {
                        setStatus = "Awaiting opponent setup";
                        setAutoSetupButton = false;
                        setSubmitButton = false;
                        setClearButton = false;
                    }
                    break;
                default:
                    break;
            }
        }
        // for local changes
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case SetupRankCommitted(_, var setupCommitPhase):
                    (Rank, int, int)[] ranksArray = setupCommitPhase.RanksRemaining();
                    setRefreshEntries = (ranksArray, setupCommitPhase.selectedRank);
                    setSubmitButton = ranksArray.All(rank => rank.Item3 == rank.Item2); 
                    setClearButton = ranksArray.Any(rank => rank.Item3 > 0); 
                    break;
                case SetupRankSelected(_,var setupCommitPhase):
                    (Rank, int, int)[] ranksArray2 = setupCommitPhase.RanksRemaining();
                    setRefreshEntries = (ranksArray2, setupCommitPhase.selectedRank);
                    setSubmitButton = ranksArray2.All(rank => rank.Item3 == rank.Item2); 
                    setClearButton = ranksArray2.Any(rank => rank.Item3 > 0); 
                    break;
            }
        }
        
        // now do the stuff
        // Visibility is handled centrally by GuiGame. Do not toggle here.
        if (setInitialize.HasValue)
        {
            Initialize(setInitialize.Value);
        }
        if (setRefreshEntries.HasValue)
        {
            setupScreen.RefreshFromRanks(setRefreshEntries.Value.Item1);
        }
        if (setStatus.Length != 0)
        {
            statusText.text = setStatus;
        }
        if (setAutoSetupButton.HasValue)
        {
            autoSetupButton.interactable = setAutoSetupButton.Value;
        }
        if (setSubmitButton.HasValue)
        {
            submitButton.interactable = setSubmitButton.Value;
        }
        if (setClearButton.HasValue)
        {
            clearButton.interactable = setClearButton.Value;
        }
    }
    
}
