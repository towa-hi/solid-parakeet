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
    
    public void AttachSubscriptions()
    {
        ViewEventBus.OnResolveCheckpointChanged += HandleResolveCheckpointChanged;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnResolveCheckpointChanged -= HandleResolveCheckpointChanged;
    }
    
    void HandleResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        string prevLabel = null;
        string nextLabel = null;
        string status = null;
        switch (checkpoint)
        {
            case ResolveCheckpoint.Pre:
                ArenaController.instance.Close();
                status = "Resolve: start";
                prevLabel = "<- [start]";
                nextLabel = "[apply moves] ->";
                break;
            case ResolveCheckpoint.PostMoves:
                ArenaController.instance.Close();
                status = "Resolve: Applying Moves";
                prevLabel = "<- [start]";
                nextLabel = (tr.battles?.Length ?? 0) > 0 ? "[battle] ->" : "[final] ->";
                break;
            case ResolveCheckpoint.Battle:
                var battle = tr.battles[battleIndex];
                ArenaController.instance.StartBattle(battle, tr);
                int total = tr.battles?.Length ?? 0;
                string p0 = battle.participants != null && battle.participants.Length > 0 ? battle.participants[0].ToString() : "?";
                string p1 = battle.participants != null && battle.participants.Length > 1 ? battle.participants[1].ToString() : "?";
                status = $"Resolve: Battle {battleIndex + 1}/{Mathf.Max(1, total)} ({p0} vs {p1})";
                prevLabel = "<- [start]";
                nextLabel = (battleIndex + 1 < total) ? $"battle {battleIndex + 2}/{Mathf.Max(1, total)} ->" : "[final] ->";
                break;
            case ResolveCheckpoint.Final:
                ArenaController.instance.Close();
                status = "Resolve: Final";
                prevLabel = "<- [start]";
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
        OnMenuButton?.Invoke();
    }

    void HandlePrevButton()
    {
        Debug.Log("GuiResolve.HandlePrevButton");
        OnPrevButton?.Invoke();
    }

    void HandleNextButton()
    {
        Debug.Log("GuiResolve.HandleNextButton");
        OnNextButton?.Invoke();
    }

    void HandleSkipButton()
    {
        Debug.Log("GuiResolve.HandleSkipButton");
        OnSkipButton?.Invoke();
    }

    public override void InitializeFromState(GameNetworkState net, LocalUiState ui)
    {
        Initialize(net);
        // Seed from current checkpoint if any
        HandleResolveCheckpointChanged(ui.Checkpoint, ui.ResolveData, ui.BattleIndex, net);
    }
}
