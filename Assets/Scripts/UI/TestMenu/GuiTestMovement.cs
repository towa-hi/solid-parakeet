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
    public TextMeshProUGUI turnText;
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
        autoSubmitToggle.onValueChanged.AddListener((autoSubmit) => OnAutoSubmitToggle?.Invoke(autoSubmit));
    }

    public override void Initialize(TestBoardManager boardManager, Lobby lobby)
    {
        base.Initialize(boardManager, lobby);
        boardManager.OnClientGameStateChanged += OnClientGameStateChanged;
        autoSubmitToggle.SetIsOnWithoutNotify(MovementClientState.autoSubmit);
    }

    void OnClientGameStateChanged(Lobby lobby, ITestPhase phase)
    {
        if (phase is not MovementTestPhase movementPhase) return;
        MovementClientState state = movementPhase.clientState;
        autoSubmitToggle.SetIsOnWithoutNotify(MovementClientState.autoSubmit);
        switch (state.team)
        {
            // Update team checks
            case Team.RED:
                redCheck.enabled = state.myTurnMove.initialized;
                blueCheck.enabled = state.otherTurnMove.initialized;
                break;
            case Team.BLUE:
                redCheck.enabled = state.otherTurnMove.initialized;
                blueCheck.enabled = state.myTurnMove.initialized;
                break;
        }
        turnText.text = "Turn: " + state.myTurnMove.turn;
        // Update UI based on current substate
        switch (state.subState)
        {
            case SelectingPawnMovementClientSubState selectingPawnSubState:
                submitMoveButton.interactable = selectingPawnSubState.selectedPawnId.HasValue;
                statusText.text = selectingPawnSubState.selectedPawnId.HasValue 
                    ? "Click submit to confirm move" 
                    : "Select a pawn to move";
                break;
            case SelectingPosMovementClientSubState:
                submitMoveButton.interactable = false;
                statusText.text = "Select a destination";
                break;

            case WaitingUserHashMovementClientSubState:
                submitMoveButton.interactable = false;
                statusText.text = "Automatically sending move hash please wait warmly";
                break;

            case WaitingOpponentMoveMovementClientSubState:
                submitMoveButton.interactable = false;
                statusText.text = $"You committed move {state.myTurnMove.pawn_id} to {state.myTurnMove.pos}, click refresh to check opponent status";
                break;

            case WaitingOpponentHashMovementClientSubState:
                submitMoveButton.interactable = false;
                statusText.text = "Waiting for other user to send move hash... click refresh to check";
                break;

            case ResolvingMovementClientSubState:
                submitMoveButton.interactable = false;
                statusText.text = "Turn is not valid. you shouldn't be seeing this";
                break;
        }
    }
}
