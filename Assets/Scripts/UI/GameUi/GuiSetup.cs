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
    Dictionary<Rank, int> usedCounts = new();
    Rank? selectedRank;
    GameNetworkState? lastNetState;

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

    // Subscriptions controlled by BoardManager lifecycle, not Unity enable/disable
    public void AttachSubscriptions()
    {
        ViewEventBus.OnSetupRankSelected += HandleSetupRankSelected;
        ViewEventBus.OnSetupPendingChanged += HandleSetupPendingChanged;
    }

    public void DetachSubscriptions()
    {
        setupScreen.Uninitialize();
        ViewEventBus.OnSetupRankSelected -= HandleSetupRankSelected;
        ViewEventBus.OnSetupPendingChanged -= HandleSetupPendingChanged;
    }

    void Initialize(GameNetworkState netState)
    {
        lastNetState = netState;
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
        if (setupScreen.isActiveAndEnabled)
        {
            setupScreen.PlayOpenAnimation();
        }
        // do rankrefresh on setupscreen from maxpawns
        List<(Rank, int, int)> rankList = new();
        for (int i = 0; i < maxRanks.Length; i++)
        {
            rankList.Add(((Rank)i, (int)maxRanks[i], 0));
        }
        setupScreen.RefreshFromRanks(rankList.ToArray());
        // reset local trackers
        usedCounts.Clear();
        selectedRank = null;
    }

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        Debug.Log("GuiSetup.InitializeFromState");
        if (!isActiveAndEnabled)
        {
            return;
        }
        Initialize(net);
        // Deterministic initial UI state
        bool isMyTurn = net.IsMySubphase();
        statusText.text = isMyTurn ? "Commit your pawn setup" : "Awaiting opponent setup";
        autoSetupButton.interactable = isMyTurn;
        submitButton.interactable = false;
        clearButton.interactable = false;
        // Apply initial UI state: selection and pending counts
        if (ui.SelectedRank is Rank sel)
        {
            HandleSetupRankSelected(null, sel);
        }
        if (ui.PendingCommits != null)
        {
            HandleSetupPendingChanged(new Dictionary<PawnId, Rank?>(), ui.PendingCommits);
            // auto-setup remains gated by turn; submit/clear toggled by handler
            autoSetupButton.interactable = isMyTurn;
        }
    }

    // Removed; setup is initialized via legacy PhaseStateChanged in non-flag builds,
    // and via event bus + board init in flagged builds.

    // Unsubscribe handled at game teardown
    public override void PhaseStateChanged(PhaseChangeSet changes)
    {
#if USE_GAME_STORE
        // In flagged builds, setup UI is driven by ViewEventBus and InitializeFromNet; ignore phase-driven updates
        return;
#endif
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
    
    void HandleSetupRankSelected(Rank? oldRank, Rank? newRank)
    {
        selectedRank = newRank;
        if (lastNetState is not GameNetworkState net) return;
        uint[] maxRanks = lastNetState.Value.lobbyParameters.max_ranks;
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rank = (Rank)i;
            int used = usedCounts.TryGetValue(rank, out int u) ? u : 0;
            if (entries != null && entries.TryGetValue(rank, out GuiRankListEntry entry))
            {
                bool interactable = used < maxRanks[i];
                entry.Refresh((int)maxRanks[i], used, newRank == rank, interactable);
            }
        }
    }

    void HandleSetupPendingChanged(Dictionary<PawnId, Rank?> oldMap, Dictionary<PawnId, Rank?> newMap)
    {
        if (lastNetState is not GameNetworkState net) return;
        // recompute used counts per rank
        usedCounts.Clear();
        foreach (Rank rank in Enum.GetValues(typeof(Rank)))
        {
            usedCounts[(Rank)rank] = 0;
        }
        foreach (var kv in newMap)
        {
            if (kv.Value is Rank r)
            {
                usedCounts[r] = usedCounts.TryGetValue(r, out int c) ? c + 1 : 1;
            }
        }
        uint[] maxRanks = net.lobbyParameters.max_ranks;
        var ranksArray = new (Rank, int, int)[maxRanks.Length];
        bool allFilled = true;
        bool anyUsed = false;
        for (int i = 0; i < maxRanks.Length; i++)
        {
            Rank rk = (Rank)i;
            int max = (int)maxRanks[i];
            int used = usedCounts.TryGetValue(rk, out int u) ? u : 0;
            ranksArray[i] = (rk, max, used);
            allFilled &= used == max;
            anyUsed |= used > 0;
            if (entries != null && entries.TryGetValue(rk, out GuiRankListEntry entry))
            {
                bool interactable = used < max;
                entry.Refresh(max, used, selectedRank == rk, interactable);
            }
        }
        setupScreen.RefreshFromRanks(ranksArray);
        submitButton.interactable = allFilled;
        clearButton.interactable = anyUsed;
    }

}
