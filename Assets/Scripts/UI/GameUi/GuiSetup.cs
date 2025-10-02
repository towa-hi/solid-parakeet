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
    public Button submitButton;
    public TextMeshProUGUI statusText;
    public GameObject rankEntryPrefab;
    public Dictionary<Rank, GuiRankListEntry> entries;
    Dictionary<Rank, int> usedCounts = new();
    Rank? selectedRank;
    GameNetworkState? lastNetState;
    bool canInteract = true;

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
        submitButton.onClick.AddListener(() => OnSubmitButton?.Invoke());
    }

    // Subscriptions controlled by BoardManager lifecycle, not Unity enable/disable
    public void AttachSubscriptions()
    {
        ViewEventBus.OnSetupRankSelected += HandleSetupRankSelected;
        ViewEventBus.OnSetupPendingChanged += HandleSetupPendingChanged;
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnSetupRankSelected -= HandleSetupRankSelected;
        ViewEventBus.OnSetupPendingChanged -= HandleSetupPendingChanged;
        ViewEventBus.OnStateUpdated -= HandleStateUpdated;
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
            entries[rank].Refresh((int)maxRanks[i], 0, false, true);
        }
        setupScreen.Initialize(netState);
        setupScreen.OnCardRankClicked = (rank) => { if (canInteract) OnEntryClicked?.Invoke(rank); };
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
        bool waiting = ui?.WaitingForResponse != null;
        canInteract = isMyTurn && !waiting;
        if (waiting)
        {
            statusText.text = "Submitting setup...";
        }
        else
        {
            statusText.text = isMyTurn ? "Commit your pawn setup" : "Awaiting opponent setup";
        }
        autoSetupButton.interactable = canInteract;
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
            autoSetupButton.interactable = canInteract;
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
                bool interactable = canInteract && used < maxRanks[i];
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
                bool interactable = canInteract && used < max;
                entry.Refresh(max, used, selectedRank == rk, interactable);
            }
        }
        if (canInteract)
        {
            string message = "Place all pawns on the board";
            if (allFilled)
            {
                message = "Commit your pawn setup";
            }
            statusText.text = message;
        }
        setupScreen.RefreshFromRanks(ranksArray);
        submitButton.interactable = allFilled && canInteract;
        clearButton.interactable = anyUsed && canInteract;
    }

    void HandleStateUpdated(GameSnapshot snapshot)
    {
        if (!isActiveAndEnabled) return;
        if (snapshot == null) return;
        lastNetState = snapshot.Net;
        bool isMyTurn = snapshot.Net.IsMySubphase();
        bool waiting = snapshot.Ui?.WaitingForResponse != null;
        canInteract = isMyTurn && !waiting;
        if (waiting)
        {
            statusText.text = "Submitting setup...";
            autoSetupButton.interactable = false;
            submitButton.interactable = false;
            clearButton.interactable = false;
            return;
        }
        if (!isMyTurn)
        {
            statusText.text = "Awaiting opponent setup";
            autoSetupButton.interactable = false;
            submitButton.interactable = false;
            clearButton.interactable = false;
            return;
        }
        // My turn and not waiting: allow auto-setup; submit/clear are governed by pending handler
        autoSetupButton.interactable = true;
    }

}
