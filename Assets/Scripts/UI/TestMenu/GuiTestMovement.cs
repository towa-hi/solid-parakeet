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
    
    public event Action OnMenuButton;
    public event Action OnExtraButton;
    public event Action OnSubmitMoveButton;
    public event Action OnGraveyardButton;
    public event Action OnRefreshButton;

    void Start()
    {
        menuButton.onClick.AddListener(() => OnMenuButton?.Invoke());
        extraButton.onClick.AddListener(() => OnExtraButton?.Invoke());
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
    }
    
    public void Refresh(MovementTestPhase phase)
    {
        submitMoveButton.interactable = phase.queuedMove != null;
        Debug.Log(phase.committedMove.initialized);
        if (phase.committedMove.initialized)
        {
            submitMoveButton.interactable = false;
            statusText.text = $"you commited move {phase.committedMove.pawn_id} to {phase.committedMove.pos}";
        }

        redCheck.enabled = phase.cachedTurn.host_turn.initialized;
        blueCheck.enabled = phase.cachedTurn.guest_turn.initialized;
    }
    
}
