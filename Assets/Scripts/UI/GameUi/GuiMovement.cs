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
    public Button redeemWinButton;
    public TextMeshProUGUI redeemWinButtonText;
    public PhaseInfoDisplay phaseInfoDisplay;
    
    
    public Action OnSubmitMoveButton;
    public Action OnGraveyardButton;
    public Action<bool> OnAutoSubmitToggle;
    public Action OnRedeemWinButton;

    public GraveyardList graveyardList;

    void Start()
    {
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        if (redeemWinButton != null)
        {
            redeemWinButton.onClick.AddListener(() => OnRedeemWinButton?.Invoke());
        }
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
        if (redeemWinButton != null)
        {
            redeemWinButton.gameObject.SetActive(false);
        }
        if (redeemWinButtonText != null)
        {
            redeemWinButtonText.text = "Redeem Win";
        }
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

        Phase phase = net.lobbyInfo.phase;
        Subphase subphase = net.lobbyInfo.subphase;
        bool myTurn = net.IsMySubphase();
        bool secure = net.lobbyParameters.security_mode;
        bool waiting = snapshot.Ui.WaitingForResponse != null;

        string statusMessage = "Commit your move";
        string submitButtonMessage = $"Commit Move ({snapshot.Ui.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
        bool showRedeem = false;
        bool canRedeem = false;

        if (waiting)
        {
            GameAction action = snapshot.Ui.WaitingForResponse.Action;
            if (action is CommitMoveAndProve)
            {
                statusMessage = secure ? "Submitting move commit..." : "Submitting move...";
                submitButtonMessage = $"Sent move ({snapshot.Ui.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
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
            else if (action is RedeemWin)
            {
                statusMessage = "Redeeming win...";
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
                        submitButtonMessage = $"Commit Move ({snapshot.Ui.MovePairs.Count}/{net.GetMaxMovesThisTurn()})";
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
                case Phase.Finished:
                    // Show Redeem button if this player is winner
                    bool iAmWinner = net.IsMySubphase();
                    showRedeem = iAmWinner;
                    canRedeem = iAmWinner;
                    statusMessage = iAmWinner ? "You won! Redeem your win." : "Game over";
                    submitButtonMessage = "Game finished";
                    break;
                default:
                    statusMessage = myTurn ? "Your turn" : "Waiting for opponent";
                    submitButtonMessage = myTurn ? submitButtonMessage : "Please wait...";
                    break;
            }
        }

        bool canSubmit = phase == Phase.MoveCommit && myTurn && !waiting;

        statusText.text = statusMessage;
        submitMoveButton.interactable = canSubmit;
        submitMoveButtonText.text = submitButtonMessage;
        if (redeemWinButton != null)
        {
            // Hide while waiting on any network call
            redeemWinButton.gameObject.SetActive(showRedeem && !waiting);
            redeemWinButton.interactable = canRedeem && !waiting;
        }
        if (redeemWinButtonText != null && showRedeem)
        {
            redeemWinButtonText.text = "Redeem Win";
        }
        phaseInfoDisplay.Set(net);
    }

    void HandleStateUpdated(GameSnapshot snapshot)
    {
        Refresh(snapshot);
    }
}
