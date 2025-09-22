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
    
    void HandleEscapeMenuButton()
    {
        AudioManager.PlayMidButtonClick();
        OnMenuButton?.Invoke();
    }

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        lastNetState = net;
        bool isMyTurn = net.IsMySubphase();
        statusText.text = isMyTurn ? "Commit your move" : "Awaiting opponent move";
        refreshButton.interactable = true;
        submitMoveButton.interactable = false;
        submitMoveButtonText.text = $"Commit Move (0/{net.GetMaxMovesThisTurn()})";
        phaseInfoDisplay.Set(net);
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
