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
    public Toggle autoSubmitToggle;
    public GuiGameOverModal gameOverModal;
    public PhaseInfoDisplay phaseInfoDisplay;
    
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
    
    public void PhaseStateChanged(PhaseChangeSet changes)
    {
        // what to do
        bool? setShowElement = null;
        GameNetworkState? setInitialize = null;
        GameNetworkState? setPhaseInfoDisplay = null;
        bool? setSubmitButton = null;
        bool? setRefreshButton = null;
        string setStatus = null;
        // figure out what to do based on what happened
        if (changes.GetNetStateUpdated() is NetStateUpdated netStateUpdated)
        {
            GameNetworkState cachedNetState = netStateUpdated.phase.cachedNetState;
            setInitialize = cachedNetState;
            setPhaseInfoDisplay = cachedNetState;
            switch (cachedNetState.lobbyInfo.phase)
            {
                case Phase.MoveCommit:
                    setShowElement = true;
                    setSubmitButton = false;
                    setRefreshButton = true;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your move";
                    }
                    else
                    {
                        setStatus = "Awaiting opponent move";
                    }
                    break;
                case Phase.MoveProve:
                    setShowElement = true;
                    setSubmitButton = false;
                    setRefreshButton = true;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your move proof (automatic)";
                    }
                    else
                    {
                        setStatus = "Awaiting opponent move proof";
                    }
                    break;
                case Phase.RankProve:
                    setShowElement = true;
                    setSubmitButton = false;
                    setRefreshButton = true;
                    if (cachedNetState.IsMySubphase())
                    {
                        setStatus = "Commit your rank proof (automatic)";
                    }
                    else
                    {
                        setStatus = "Awaiting opponent rank proof";
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
                case MovePosSelected(_, var selectedPos, _):
                    setSubmitButton = false;
                    if (selectedPos.HasValue)
                    {
                        setStatus = "Select a target position";
                    }
                    break;
                case MoveTargetSelected(_, var newTarget):
                    if (newTarget.HasValue)
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
        if (setShowElement is bool showElementVal)
        {
            ShowElement(showElementVal);
        }
        if (setInitialize is GameNetworkState initializeVal)
        {
            Initialize(initializeVal);
        }
        if (setPhaseInfoDisplay is GameNetworkState phaseInfoDisplayVal)
        {
            phaseInfoDisplay.Set(phaseInfoDisplayVal);
        }
        if (setSubmitButton is bool submitButtonVal)
        {
            submitMoveButton.interactable = submitButtonVal;
        }
        if (setRefreshButton is bool refreshButtonVal)
        {
            refreshButton.interactable = refreshButtonVal;
        }
        if (setStatus is not null)
        {
            statusText.text = setStatus;
        }
    }
}
