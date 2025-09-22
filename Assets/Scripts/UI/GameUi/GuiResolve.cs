using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiResolve : GameElement
{
    public ArenaController arenaController;
    public Button menuButton;
    public Button prevButton;
    public TextMeshProUGUI prevButtonLabel;
    public Button nextButton;
    public TextMeshProUGUI nextButtonLabel;
    public Button skipButton;
    public TextMeshProUGUI statusText;

    public Action OnMenuButton;
    public Action OnPrevButton;
    public Action OnNextButton;
    public Action OnSkipButton;

    void Start()
    {
        Debug.Log("GuiResolve.Start: wiring button listeners");
        menuButton.onClick.AddListener(HandleMenuButton);
        prevButton.onClick.AddListener(HandlePrevButton);
        nextButton.onClick.AddListener(HandleNextButton);
        skipButton.onClick.AddListener(HandleSkipButton);
    }

    public void Initialize(GameNetworkState gameNetworkState)
    {
        arenaController.Initialize(gameNetworkState.lobbyParameters.board.hex);
        statusText.text = "Resolve: start";
        prevButtonLabel.text = "<- [none]";
        nextButtonLabel.text = "[apply moves] ->";
    }
    
    // no phasestatechanged listen here

    public override void PhaseStateChanged(PhaseChangeSet changes)
    {
        // Reflect resolve sub-state in status text based on operations
        foreach (GameOperation op in changes.operations)
        {
            switch (op)
            {
                case ResolveCheckpointEntered(var checkpoint, var tr, var battleIndex, var phase):
                    ResolveCheckpointEntered(checkpoint, tr, battleIndex);
                    break;
            }
        }
        // Visibility is handled centrally by GuiGame. Do not toggle here.
    }
 void ResolveCheckpointEntered(ResolvePhase.Checkpoint checkpoint, TurnResolveDelta tr, int battleIndex) {    
        string prevLabel = null;
        string nextLabel = null;
        string status = null;
        switch (checkpoint)
        {
            case ResolvePhase.Checkpoint.Pre:
                ArenaController.instance.Close();
                status = "Resolve: start";
                prevLabel = "<- [none]";
                nextLabel = "[apply moves] ->";
                break;
            case ResolvePhase.Checkpoint.PostMoves:
                ArenaController.instance.Close();
                status = "Resolve: Applying Moves";
                prevLabel = "<- [start]";
                nextLabel = (tr.battles?.Length ?? 0) > 0 ? "[battle] ->" : "[final] ->";
                break;
            case ResolvePhase.Checkpoint.Battle:
                var battle = tr.battles[battleIndex];
                ArenaController.instance.StartBattle(battle, tr);
                int total = tr.battles?.Length ?? 0;
                string p0 = battle.participants != null && battle.participants.Length > 0 ? battle.participants[0].ToString() : "?";
                string p1 = battle.participants != null && battle.participants.Length > 1 ? battle.participants[1].ToString() : "?";
                status = $"Resolve: Battle {battleIndex + 1}/{Mathf.Max(1, total)} ({p0} vs {p1})";
                prevLabel = battleIndex > 0 ? $"<- battle {battleIndex}/{Mathf.Max(1, total)}" : "<- [start]";
                nextLabel = (battleIndex + 1 < total) ? $"battle {battleIndex + 2}/{Mathf.Max(1, total)} ->" : "[final] ->";
                break;
            case ResolvePhase.Checkpoint.Final:
                ArenaController.instance.Close();
                status = "Resolve: Final";
                prevLabel = "<- [post moves]";
                nextLabel = "[continue] ->";
                break;
        }
        statusText.text = status;
        prevButtonLabel.text = prevLabel;
        nextButtonLabel.text = nextLabel;
    }
    void HandleMenuButton()
    {
        //just play a mid button click and invoke the event in these functions
        Debug.Log("GuiResolve.HandleMenuButton");
        AudioManager.PlayMidButtonClick();
        OnMenuButton?.Invoke();
    }

    void HandlePrevButton()
    {
        Debug.Log("GuiResolve.HandlePrevButton");
        AudioManager.PlayMidButtonClick();
        OnPrevButton?.Invoke();
    }

    void HandleNextButton()
    {
        Debug.Log("GuiResolve.HandleNextButton");
        AudioManager.PlayMidButtonClick();
        OnNextButton?.Invoke();
    }

    void HandleSkipButton()
    {
        Debug.Log("GuiResolve.HandleSkipButton");
        AudioManager.PlayMidButtonClick();
        OnSkipButton?.Invoke();
    }

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        Initialize(net);
    }
}
