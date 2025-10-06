using System;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuiResolve : GameElement
{
    public ArenaController arenaController;
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
        prevButton.onClick.AddListener(HandlePrevButton);
        nextButton.onClick.AddListener(HandleNextButton);
        skipButton.onClick.AddListener(HandleSkipButton);
    }
    
    public void AttachSubscriptions()
    {
        ViewEventBus.OnResolveCheckpointChanged += HandleResolveCheckpointChanged;
    }

    public void DetachSubscriptions()
    {
        ViewEventBus.OnResolveCheckpointChanged -= HandleResolveCheckpointChanged;
    }
    
    public override void OnClientModeChanged(GameSnapshot snapshot)
    {
        //Reset(snapshot.Net);
    }

    public override void Reset(GameNetworkState net)
    {

    }

    public override void Refresh(GameSnapshot snapshot)
    {
        HandleResolveCheckpointChanged(snapshot.Ui.Checkpoint, snapshot.Ui.ResolveData, snapshot.Ui.BattleIndex, snapshot.Net);
    }
    void HandleResolveCheckpointChanged(ResolveCheckpoint checkpoint, TurnResolveDelta tr, int battleIndex, GameNetworkState net)
    {
        Debug.Log($"[GuiResolve] Begin HandleResolveCheckpointChanged checkpoint={checkpoint} index={battleIndex} moves={(tr.moves?.Count ?? 0)} battles={(tr.battles?.Length ?? 0)}");
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
        Debug.Log($"[GuiResolve] End HandleResolveCheckpointChanged checkpoint={checkpoint} index={battleIndex}");
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
}
