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
    
    
    public Action OnSubmitMoveButton;
    public Action OnGraveyardButton;
    public Action<bool> OnAutoSubmitToggle;

    public GraveyardList graveyardList;

    void Start()
    {
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
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

    public void AttachSubscriptions()
    {
        ViewEventBus.OnStateUpdated += HandleStateUpdated;
    }

    public void DetachSubscriptions()
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

    void HandleStateUpdated(GameSnapshot snapshot)
    {
        Refresh(snapshot);
    }
}
