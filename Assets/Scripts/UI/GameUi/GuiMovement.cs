using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GuiMovement : GameElement
{
    public TextMeshProUGUI statusText;
    public Button submitMoveButton;
    public TextMeshProUGUI submitMoveButtonText;
    public Button graveyardButton;
    public GuiGameOverModal gameOverModal;
    public PhaseInfoDisplay phaseInfoDisplay;
    GameNetworkState? lastNetState;
    
    public Action OnSubmitMoveButton;
    public Action OnGraveyardButton;
    public Action<bool> OnAutoSubmitToggle;

    public GraveyardList graveyardList;

    void Awake()
    {
        if (submitMoveButton != null)
        {
            submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        }
        // if (graveyardButton != null)
        // {
        //     graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
		// 	// Show/hide graveyard list on hover over the button
		// 	if (graveyardList != null)
		// 	{
		// 		EventTrigger trigger = graveyardButton.gameObject.GetComponent<EventTrigger>();
		// 		if (trigger == null)
		// 		{
		// 			trigger = graveyardButton.gameObject.AddComponent<EventTrigger>();
		// 		}
		// 		AddEventTrigger(trigger, EventTriggerType.PointerEnter, () => graveyardList.gameObject.SetActive(true));
		// 		AddEventTrigger(trigger, EventTriggerType.PointerExit, () => graveyardList.gameObject.SetActive(false));
		// 		graveyardList.gameObject.SetActive(false);
		// 	}
        // }
    }

    void Start()
    {
        // no-op; initialization occurs in InitializeFromState on mode enter
    }

	void AddEventTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
	{
		if (trigger.triggers == null)
		{
			trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
		}
		var entry = new EventTrigger.Entry { eventID = type };
		entry.callback.AddListener(_ => action());
		trigger.triggers.Add(entry);
	}

    public void AttachSubscriptions()
    {
        ViewEventBus.OnMoveHoverChanged += HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged += HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged += HandleMovePairsChanged;
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnMoveHoverChanged -= HandleMoveHoverChanged;
        ViewEventBus.OnMoveSelectionChanged -= HandleMoveSelectionChanged;
        ViewEventBus.OnMovePairsChanged -= HandleMovePairsChanged;
        ViewEventBus.OnStateUpdated -= HandleStateUpdated;
    }
    

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        lastNetState = net;
        bool isMyTurn = net.IsMySubphase();
        statusText.text = isMyTurn ? "Commit your move" : "Awaiting opponent move";
        submitMoveButton.interactable = false;
        submitMoveButtonText.text = $"Commit Move (0/{net.GetMaxMovesThisTurn()})";
		if (phaseInfoDisplay != null) phaseInfoDisplay.Set(net);
		//if (graveyardList != null) graveyardList.Refresh(net);
    }

    void HandleMoveHoverChanged(Vector2Int pos, bool isMyTurn, System.Collections.Generic.HashSet<Vector2Int> _)
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


    void HandleStateUpdated(GameSnapshot snapshot)
    {
        Debug.Log($"GuiMovement.HandleStateUpdated: snapshot={snapshot}");
        // if (snapshot == null)
        // {
        //     return;
        // }
        // if (snapshot.Mode != ClientMode.Move)
        // {
        //     return;
        // }
		// // React to phase/subphase or turn changes within Move (e.g., Resolve->Move same phase but new turn)
        // if (!lastNetState.HasValue)
        // {
        //     InitializeFromState(snapshot.Net, snapshot.Ui ?? LocalUiState.Empty);
        //     return;
        // }
        // var prev = lastNetState.Value;
        // var next = snapshot.Net;
		// if (prev.lobbyInfo.phase != next.lobbyInfo.phase
		// 	|| prev.lobbyInfo.subphase != next.lobbyInfo.subphase
		// 	|| prev.gameState.turn != next.gameState.turn)
        // {
        //     InitializeFromState(snapshot.Net, snapshot.Ui ?? LocalUiState.Empty);
        // }
    }
}
