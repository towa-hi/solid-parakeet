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
    public Button graveyardButton;
    public Button refreshButton;
    public Image redCheck;
    public Image blueCheck;
    public Toggle autoSubmitToggle;
    public TextMeshProUGUI turnText;
    public GuiGameOverModal gameOverModal;
    
    public event Action OnMenuButton;
    public event Action OnExtraButton;
    
    public event Action OnCheatButton;
    public event Action OnBadgeButton;
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
        cheatButton.onClick.AddListener(() => OnCheatButton?.Invoke());
        badgeButton.onClick.AddListener(() => OnBadgeButton?.Invoke());
        autoSubmitToggle.onValueChanged.AddListener((autoSubmit) => OnAutoSubmitToggle?.Invoke(autoSubmit));
    }
    
    public void PhaseStateChanged(IPhaseChangeSet changes)
    {

        
    }
    
    void Refresh(PhaseBase currentPhase)
    {
        OnMenuButton = null;
        OnExtraButton = null;
        OnCheatButton = null;
        OnBadgeButton = null;
        OnSubmitMoveButton = null;
        OnGraveyardButton = null;
        OnRefreshButton = null;
        OnAutoSubmitToggle = null;
        
        bool show;
        switch (currentPhase)
        {
            case SetupCommitPhase setupCommitPhase:
                show = false;
                break;
            case MoveCommitPhase moveCommitPhase:
            case MoveProvePhase moveProvePhase:
            case RankProvePhase rankProvePhase:
                show = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currentPhase));
        }
        ShowElement(show);
    }
    
    //
    // public override void Initialize(BoardManager boardManager, GameNetworkState networkState)
    // {
    //     base.Initialize(boardManager, networkState);
    //     //boardManager.OnClientGameStateChanged += OnClientGameStateChanged;
    //     autoSubmitToggle.SetIsOnWithoutNotify(MovementClientState.autoSubmit);
    // }
    //
    // public void Refresh(MovementClientState state)
    // {
    //     autoSubmitToggle.SetIsOnWithoutNotify(MovementClientState.autoSubmit);
    //     switch (state.team)
    //     {
    //         // Update team checks
    //         case Team.RED:
    //             redCheck.enabled = state.myTurnMove.initialized;
    //             blueCheck.enabled = state.otherTurnMove.initialized;
    //             break;
    //         case Team.BLUE:
    //             redCheck.enabled = state.otherTurnMove.initialized;
    //             blueCheck.enabled = state.myTurnMove.initialized;
    //             break;
    //     }
    //     turnText.text = "Turn: " + state.myTurnMove.turn;
    //     cheatButtonText.text = PlayerPrefs.GetInt("CHEATMODE") == 1 ? "Hide Enemy Pieces" : "Reveal Enemy Pieces";
    //     badgeButtonText.text = PlayerPrefs.GetInt("DISPLAYBADGE") == 1 ? "Hide Display Rank Badges" : "Display Rank Badges";
    //     // Update UI based on current substate
    //     switch (state.subState)
    //     {
    //         case SelectingPawnMovementClientSubState selectingPawnSubState:
    //             submitMoveButton.interactable = selectingPawnSubState.selectedPawnId.HasValue;
    //             statusText.text = selectingPawnSubState.selectedPawnId.HasValue 
    //                 ? "Click submit to confirm move" 
    //                 : "Select a pawn to move";
    //             break;
    //         case SelectingPosMovementClientSubState:
    //             submitMoveButton.interactable = false;
    //             statusText.text = "Select a destination";
    //             break;
    //
    //         case WaitingUserHashMovementClientSubState:
    //             submitMoveButton.interactable = false;
    //             statusText.text = "Automatically sending move hash please wait warmly";
    //             break;
    //
    //         case WaitingOpponentMoveMovementClientSubState:
    //             submitMoveButton.interactable = false;
    //             statusText.text = $"You committed move {state.myTurnMove.pawn_id} to {state.myTurnMove.pos}, click refresh to check opponent status";
    //             break;
    //
    //         case WaitingOpponentHashMovementClientSubState:
    //             submitMoveButton.interactable = false;
    //             statusText.text = "Waiting for other user to send move hash... click refresh to check";
    //             break;
    //
    //         case ResolvingMovementClientSubState:
    //             submitMoveButton.interactable = false;
    //             statusText.text = "Turn is not valid. you shouldn't be seeing this";
    //             break;
    //         case GameOverMovementClientSubState gameOverSubState:
    //             statusText.text = gameOverSubState.EndStateMessage();
    //             gameOverModal.Initialize(gameOverSubState.endState, state.team);
    //             gameOverModal.gameObject.SetActive(true);
    //             break;
    //     }
    // }
}
