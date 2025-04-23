using System;
using System.Linq;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class GuiTestMovement : GameElement
{
    public TextMeshProUGUI statusText;
    public Button menuButton;
    public Button extraButton;
    public Button submitMoveButton;
    public Button graveyardButton;
    public Button refreshButton;
    public Image redCheck;
    public Image blueCheck;
    public Toggle autoSubmitToggle;
    
    public event Action OnMenuButton;
    public event Action OnExtraButton;
    public event Action OnSubmitMoveButton;
    public event Action OnGraveyardButton;
    public event Action OnRefreshButton;

    public event Action<bool> OnAutoSubmitToggle;

    void Start()
    {
        menuButton.onClick.AddListener(() => OnMenuButton?.Invoke());
        extraButton.onClick.AddListener(() => OnExtraButton?.Invoke());
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        autoSubmitToggle.onValueChanged.AddListener((autoSubmit) =>  OnAutoSubmitToggle?.Invoke(autoSubmit));
        autoSubmitToggle.SetIsOnWithoutNotify(MovementClientState.autoSubmit);
    }

    public void Refresh(MovementClientState state)
    {
        autoSubmitToggle.SetIsOnWithoutNotify(MovementClientState.autoSubmit);
        bool canSubmit = state.queuedMove != null && !state.myTurnMove.initialized;
        submitMoveButton.interactable = canSubmit;
        string status = "please designate a move and click submit";
        if (state.myTurnMove.initialized)
        {
            status = $"you commited move {state.myTurnMove.pawn_id} to {state.myTurnMove.pos}, click refresh to check opponent  status";
            if (state.otherTurnMove.initialized)
            {
                status = "both players have submitted moves";
                if (string.IsNullOrEmpty(state.myEventsHash))
                {
                    status = "automatically sending move hash please wait warmly";
                    
                } else if (string.IsNullOrEmpty(state.otherEventsHash))
                {
                    status = "waiting for other user to send move hash... click refresh to check";
                }
                else
                {
                    status = "turn is not valid. you shouldn't be seeing this";
                }
            }
        }
        statusText.text = status;
        if (state.team == Team.RED)
        {
            redCheck.enabled = state.myTurnMove.initialized;
            blueCheck.enabled = state.otherTurnMove.initialized;
        }
        else if (state.team == Team.BLUE)
        {
            redCheck.enabled = state.otherTurnMove.initialized;
            blueCheck.enabled = state.myTurnMove.initialized;
        }
    }
    
}
