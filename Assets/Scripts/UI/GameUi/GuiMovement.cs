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
    public ButtonExtended menuButton;
    public TextMeshProUGUI submitMoveButtonText;
    public Button graveyardButton;
    public PhaseInfoDisplay phaseInfoDisplay;
    
    
    public Action OnSubmitMoveButton;
    public Action OnGraveyardButton;
    public Action<bool> OnAutoSubmitToggle;
    public Action OnMenuButton;
    public GraveyardList graveyardList;

    void Start()
    {
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        menuButton.onClick.AddListener(() => OnMenuButton?.Invoke());
        // Show/hide graveyard list on hover over the button
        if (graveyardList != null)
        {
            EventTrigger trigger = graveyardButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = graveyardButton.gameObject.AddComponent<EventTrigger>();
            }
            AddEventTrigger(trigger, EventTriggerType.PointerEnter, () => graveyardList.gameObject.SetActive(true));
            AddEventTrigger(trigger, EventTriggerType.PointerExit, () => graveyardList.gameObject.SetActive(false));
            graveyardList.gameObject.SetActive(false);
        }
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

    public override void AttachSubscriptions()
    {
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    public override void DetachSubscriptions()
    {
        ViewEventBus.OnStateUpdated -= HandleStateUpdated;
    }
    

    public override void OnClientModeChanged(GameSnapshot snapshot)
    {
        Reset(snapshot.Net);
    }

    public override void Reset(GameNetworkState net)
    {
        // Clear any persisted local trackers
        // Ensure hover panel is hidden on entry
        if (graveyardList != null)
        {
            graveyardList.gameObject.SetActive(false);
        }
        graveyardList.Clear();
        // Set default UI copy and interactivity independent of UI store state
        statusText.text = "Commit your move";
        submitMoveButton.interactable = net.IsMySubphase();
        submitMoveButtonText.text = $"Commit Move (0/{net.GetMaxMovesThisTurn()})";
        // Initialize phase panel with structural network parameters
        if (phaseInfoDisplay != null)
        {
            phaseInfoDisplay.Set(net);
        }
    }
    public override void Refresh(GameSnapshot snapshot)
    {
        Debug.Log($"GuiMovement.HandleStateUpdated: snapshot={snapshot}");
        GameNetworkState net = snapshot.Net;
        LocalUiState uiState = snapshot.Ui ?? LocalUiState.Empty;

        Phase phase = net.lobbyInfo.phase;
        Subphase subphase = net.lobbyInfo.subphase;
        bool myTurn = net.IsMySubphase();
        bool secure = net.lobbyParameters.security_mode;
        bool waiting = uiState.WaitingForResponse != null;

        string statusMessage = "Commit your move";
        string submitButtonMessage = $"Commit Move ({uiState.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";

        if (waiting)
        {
            GameAction action = uiState.WaitingForResponse.Action;
            if (action is CommitMoveAndProve)
            {
                statusMessage = secure ? "Submitting move commit..." : "Submitting move...";
                submitButtonMessage = $"Sent move ({uiState.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
            }
            else if (action is ProveMove)
            {
                statusMessage = "Submitting move proof...";
                submitButtonMessage = "Submitting proof...";
            }
            else if (action is ProveRank)
            {
                statusMessage = "Submitting rank proof...";
                submitButtonMessage = "Submitting proof...";
            }
            else if (action is UpdateState)
            {
                statusMessage = "Updating game state...";
                submitButtonMessage = "Please wait...";
            }
        }
        else
        {
            switch (phase)
            {
                case Phase.MoveCommit:
                    if (myTurn)
                    {
                        statusMessage = secure ? "Your turn: commit your move" : "Your turn: make your move";
                    submitButtonMessage = $"Commit Move ({uiState.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
                    }
                    else
                    {
                        statusMessage = "Waiting for opponent to commit move";
                        submitButtonMessage = "Waiting for opponent...";
                    }
                    break;
                case Phase.MoveProve:
                    if (myTurn)
                    {
                        statusMessage = "Your turn: prove your move";
                    }
                    else if (subphase == Subphase.Both)
                    {
                        statusMessage = "Both players may prove moves";
                    }
                    else
                    {
                        statusMessage = "Waiting for opponent's move proof";
                    }
                    submitButtonMessage = "Proving phase";
                    break;
                case Phase.RankProve:
                    if (myTurn)
                    {
                        statusMessage = "Your turn: prove required ranks";
                    }
                    else if (subphase == Subphase.Both)
                    {
                        statusMessage = "Both players may prove ranks";
                    }
                    else
                    {
                        statusMessage = "Waiting for opponent's rank proof";
                    }
                    submitButtonMessage = "Rank proving phase";
                    break;
                default:
                    statusMessage = myTurn ? "Your turn" : "Waiting for opponent";
                    submitButtonMessage = myTurn ? submitButtonMessage : "Please wait...";
                    break;
            }
        }
        graveyardList.Refresh(net);
        bool canSubmit = phase == Phase.MoveCommit && myTurn && !waiting && uiState.MovePairs.Count > 0;

        statusText.text = statusMessage;
        submitMoveButton.interactable = canSubmit;
        submitMoveButtonText.text = submitButtonMessage;
        phaseInfoDisplay.Set(net);
    }

    void HandleStateUpdated(GameSnapshot snapshot)
    {
        Refresh(snapshot);
    }
}
