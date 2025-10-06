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
    

    public override void OnClientModeChanged(GameSnapshot snapshot)
    {
        Reset(snapshot.Net);
    }

    public override void Reset(GameNetworkState net)
    {

    }
    public override void Refresh(GameSnapshot snapshot)
    {
        HandleStateUpdated(snapshot);
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
        GameNetworkState net = snapshot.Net;

        string statusMessage = "Commit your move";
        bool waiting = snapshot.Ui.WaitingForResponse?.Action is CommitMoveAndProve;
        if (waiting)
        {
            statusMessage = "Committed move...";
        }
        else if (!net.IsMySubphase())
        {
            statusMessage = "Awaiting opponent move";
        }
        bool canSubmit = net.IsMySubphase() && !waiting;

        string submitButtonMessage = $"Commit Move ({snapshot.Ui.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
        if (waiting)
        {
            submitButtonMessage = $"Sent move ({snapshot.Ui.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
        }
        statusText.text = statusMessage;
        submitMoveButton.interactable = canSubmit;
        submitMoveButtonText.text = submitButtonMessage;
        phaseInfoDisplay.Set(net);
    }
}
