using System;
using System.Linq;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class GuiMovement : GameElement
{
    public TextMeshProUGUI statusText;
    public Button menuButton;
    public Button extraButton;
    public Button cheatButton;
    public TextMeshProUGUI cheatButtonText;
    public Button badgeButton;
    public TextMeshProUGUI badgeButtonText;
    public Button submitMoveButton;
    public TextMeshProUGUI submitMoveButtonText;
    public Button graveyardButton;
    public Button refreshButton;
    public Toggle autoSubmitToggle;
    public GuiGameOverModal gameOverModal;
    public PhaseInfoDisplay phaseInfoDisplay;
    GameNetworkState? lastNetState;
    
    public Action OnMenuButton;
    public Action OnExtraButton;
    
    public Action OnCheatButton;
    public Action OnBadgeButton;
    public Action OnSubmitMoveButton;
    public Action OnGraveyardButton;
    public Action OnRefreshButton;
    public Action<bool> OnAutoSubmitToggle;
    void Start()
    {
        menuButton.onClick.AddListener(HandleEscapeMenuButton);
        extraButton.onClick.AddListener(() => OnExtraButton?.Invoke());
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        cheatButton.onClick.AddListener(() => OnCheatButton?.Invoke());
        badgeButton.onClick.AddListener(() => OnBadgeButton?.Invoke());
        autoSubmitToggle.onValueChanged.AddListener((autoSubmit) => OnAutoSubmitToggle?.Invoke(autoSubmit));
    }

    void OnEnable()
    {
        // no-op; subscriptions managed by board lifecycle
    }

    void OnDisable()
    {
        // no-op; subscriptions managed by board lifecycle
    }

    void Initialize(GameNetworkState netState)
    {
        lastNetState = netState;
        bool isMyTurn = netState.IsMySubphase();
        statusText.text = isMyTurn ? "Commit your move" : "Awaiting opponent move";
        refreshButton.interactable = true;
        submitMoveButton.interactable = false;
        submitMoveButtonText.text = $"Commit Move (0/{netState.GetMaxMovesThisTurn()})";
        phaseInfoDisplay.Set(netState);
    }
    public void AttachSubscriptions()
    {
        ViewEventBus.OnMoveHoverChanged += HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged += HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged += HandleMovePairsChanged;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnMoveHoverChanged -= HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged -= HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged -= HandleMovePairsChanged;
    }
    
    public override void PhaseStateChanged(PhaseChangeSet changes)
    {
#if USE_GAME_STORE
        // In flagged builds, movement UI is driven by ViewEventBus and InitializeFromState; ignore legacy updates
        return;
#endif
        // what to do
        GameNetworkState? setInitialize = null;
        GameNetworkState? setPhaseInfoDisplay = null;
        bool? setSubmitButton = null;
        bool? setRefreshButton = null;
        string setStatus = null;
        string setSubmitMoveLabel = null;
        // figure out what to do based on what happened
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            setInitialize = cachedNetState;
            setPhaseInfoDisplay = cachedNetState;
            // Switch strictly off the active phase object
            switch (netStateUpdated.phase)
            {
                case MoveCommitPhase moveCommitPhase:
                    setSubmitButton = false;
                    setRefreshButton = true;
                    // Update submit label with planned/allowed counts
                    int planned = cachedNetState.IsMySubphase() ? moveCommitPhase.movePairs.Count : moveCommitPhase.turnHiddenMoves.Count;
                    int allowed = cachedNetState.GetMaxMovesThisTurn();
                    setSubmitMoveLabel = $"Commit Move ({planned}/{allowed})";
                    // Update status text
                    setStatus = cachedNetState.IsMySubphase() ? "Commit your move" : "Awaiting opponent move";
                    break;
                case MoveProvePhase:
                    setSubmitButton = false;
                    setRefreshButton = true;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your move proof (automatic)";
                    }
                    else
                    {
                        setStatus = "Awaiting opponent move proof";
                    }
                    break;
                case RankProvePhase:
                    setSubmitButton = false;
                    setRefreshButton = true;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your rank proof (automatic)";
                    }
                    else
                    {
                        setStatus = "Awaiting opponent rank proof";
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
                case MovePosSelected(var selectedPos, _, _):
                    setSubmitButton = false;
                    if (selectedPos.HasValue)
                    {
                        setStatus = "Select a target position";
                    }
                    break;
                case MovePairUpdated(var movePairsSnapshot, var changedPawnId, var phaseRef):
                    // Enable submit if we have at least one planned move
                    setSubmitButton = movePairsSnapshot.Count > 0;
                    setStatus = setSubmitButton.Value ? "Submit move" : "Select a pawn";
                    // Update submit label with snapshot count
                    int allowed = phaseRef.cachedNetState.GetMaxMovesThisTurn();
                    setSubmitMoveLabel = $"Commit Move ({movePairsSnapshot.Count}/{allowed})";
                    break;
            }
        }
        // now do the stuff
        // Visibility is handled centrally by GuiGame. Do not toggle here.
        if (setInitialize is GameNetworkState initializeVal)
        {
            Initialize(initializeVal);
        }
        if (setPhaseInfoDisplay is GameNetworkState phaseInfoDisplayVal)
        {
            phaseInfoDisplay.Set(phaseInfoDisplayVal);
        }
        if (setSubmitButton is bool submitButtonVal)
        {
            submitMoveButton.interactable = submitButtonVal;
        }
        if (setRefreshButton is bool refreshButtonVal)
        {
            refreshButton.interactable = refreshButtonVal;
        }
        if (setStatus is not null)
        {
            statusText.text = setStatus;
        }
        if (setSubmitMoveLabel is not null)
        {
            submitMoveButtonText.text = setSubmitMoveLabel;
        }
    }
    
    
    void HandleEscapeMenuButton()
    {
        AudioManager.PlayMidButtonClick();
        OnMenuButton?.Invoke();
    }

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        Initialize(net);
    }

    void HandleMoveHoverChanged(Vector2Int pos, bool isMyTurn, MoveInputTool tool, System.Collections.Generic.HashSet<Vector2Int> _)
    {
        // Only cursor/selection visuals handled by tiles; GUI updates not needed here
    }

    void HandleMoveSelectionChanged(Vector2Int? selectedPos, System.Collections.Generic.HashSet<Vector2Int> validTargets)
    {
        if (selectedPos.HasValue)
        {
            statusText.text = "Select a target position";
        }
        else
        {
            // selection cleared; leave status untouched
        }
    }

    void HandleMovePairsChanged(System.Collections.Generic.Dictionary<PawnId, (Vector2Int start, Vector2Int target)> oldPairs, System.Collections.Generic.Dictionary<PawnId, (Vector2Int start, Vector2Int target)> newPairs)
    {
        int planned = newPairs.Count;
        int allowed = lastNetState is GameNetworkState net ? net.GetMaxMovesThisTurn() : 1;
        submitMoveButton.interactable = planned > 0;
        submitMoveButtonText.text = $"Commit Move ({planned}/{allowed})";
        statusText.text = planned > 0 ? "Submit move" : "Select a pawn";
    }
}
