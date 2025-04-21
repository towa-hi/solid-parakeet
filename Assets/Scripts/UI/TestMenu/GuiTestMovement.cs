using System;
using System.Linq;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using Debug = System.Diagnostics.Debug;

public class GuiTestMovement : GameElement
{
    public TextMeshProUGUI statusText;
    public Button menuButton;
    public Button extraButton;
    public Button submitMoveButton;
    public Button graveyardButton;
    public Button refreshButton;

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
    
    public void Refresh(TestBoardManager bm)
    {
        if (bm.currentPhase is not MovementTestPhase movementTestPhase)
        {
            Debug.Fail("Current phase is not MovementTestPhase");
            return;
        }
        submitMoveButton.interactable = movementTestPhase.queuedMove != null;
        User? maybeUser = StellarManagerTest.currentUser;
        Lobby? maybeLobby = StellarManagerTest.currentLobby;
        if (!maybeLobby.HasValue) return;
        if (!maybeUser.HasValue) return;
        User user = maybeUser.Value;
        Lobby lobby = maybeLobby.Value;
        
        Turn currentTurn = lobby.GetLatestTurn();
        TurnMove myMove = lobby.GetLatestTurnMove(bm.userTeam);
        if (myMove.initialized)
        {
            submitMoveButton.interactable = false;
            statusText.text = $"you commited move {myMove.pawn_id} to {myMove.pos}";
        }
    }
}
