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
        menuButton.onClick.AddListener(() => OnMenuButton?.Invoke());
        extraButton.onClick.AddListener(() => OnExtraButton?.Invoke());
        submitMoveButton.onClick.AddListener(() => OnSubmitMoveButton?.Invoke());
        graveyardButton.onClick.AddListener(() => OnGraveyardButton?.Invoke());
        refreshButton.onClick.AddListener(() => OnRefreshButton?.Invoke());
        cheatButton.onClick.AddListener(() => OnCheatButton?.Invoke());
        badgeButton.onClick.AddListener(() => OnBadgeButton?.Invoke());
        autoSubmitToggle.onValueChanged.AddListener((autoSubmit) => OnAutoSubmitToggle?.Invoke(autoSubmit));
    }

    void Initialize(GameNetworkState netState)
    {
        
    }
    
    public void PhaseStateChanged(IPhaseChangeSet changes)
    {
        // what to do
        bool? setShowElement = null;
        GameNetworkState? setInitialize = null;
        bool? setSubmitButton = null;
        bool? setRefreshButton = null;
        string setStatus = "";
        // figure out what to do based on what happened
        // for net changes
        if (changes.NetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            setInitialize = cachedNetState;
            switch (cachedNetState.lobbyInfo.phase)
            {
                case Phase.MoveCommit:
                    setShowElement = true;
                    setSubmitButton = false;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your move";
                        setRefreshButton = false;
                    }
                    else
                    {
                        setStatus = "Awaiting opponent move";
                        setRefreshButton = true;
                    }
                    break;
                case Phase.MoveProve:
                    setShowElement = true;
                    setSubmitButton = false;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your move proof (automatic)";
                        setRefreshButton = false;
                    }
                    else
                    {
                        setStatus = "Awaiting opponent move proof";
                        setRefreshButton = true;
                    }
                    break;
                case Phase.RankProve:
                    setShowElement = true;
                    setSubmitButton = false;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your rank proof (automatic)";
                        setRefreshButton = false;
                    }
                    else
                    {
                        setStatus = "Awaiting opponent rank proof";
                        setRefreshButton = true;
                    }
                    break;
                case Phase.SetupCommit:
                case Phase.Finished:
                case Phase.Aborted:
                    setShowElement = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        // for local changes
        foreach (GameOperation operation in changes.operations)
        {
            switch (operation)
            {
                case MovePosSelected(_, var moveCommitPhase):
                    setSubmitButton = false;
                    setStatus = "Select a target position";
                    break;
                case MoveTargetSelected(_, var moveCommitPhase):
                    if (moveCommitPhase.targetPos.HasValue)
                    {
                        setStatus = "Submit move";
                        setSubmitButton = true;
                    }
                    else
                    {
                        setSubmitButton = false;
                    }
                    break;
            }
        }
        // now do the stuff
        if (setShowElement.HasValue)
        {
            ShowElement(setShowElement.Value);
        }
        if (setInitialize.HasValue)
        {
            Initialize(setInitialize.Value);
        }
        if (setSubmitButton.HasValue)
        {
            submitMoveButton.interactable = setSubmitButton.Value;
        }
        if (setRefreshButton.HasValue)
        {
            refreshButton.interactable = setRefreshButton.Value;
        }
        if (setStatus.Length > 0)
        {
            statusText.text = setStatus;
        }
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
