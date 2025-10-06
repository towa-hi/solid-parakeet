using System;
using System.Security.Cryptography.X509Certificates;
using Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseInfoDisplay : MonoBehaviour
{
    public TextMeshProUGUI turnTitleText;
    public SubphaseIndicator setupCommitSubphaseIndicator;
    public SubphaseIndicator moveCommitSubphaseIndicator;

    public SubphaseIndicator moveProveSubphaseIndicator;

    public SubphaseIndicator rankProveSubphaseIndicator;

    public void Set(GameNetworkState netState)
    {
        string turnText = null;
        bool displaySetupCommit = false;
        bool displayMoveCommit = false;
        bool displayMoveProve = false;
        bool displayRankProve = false;
        Team hostTeam = netState.lobbyParameters.host_team;
        switch (netState.lobbyInfo.phase)
        {
            case Phase.Lobby:
            case Phase.Finished:
            case Phase.Aborted:
                break;
            case Phase.SetupCommit:
                displaySetupCommit = true;
                setupCommitSubphaseIndicator.Set(netState.lobbyInfo.subphase, hostTeam);
                turnText = "SETUP PHASE";
                break;
            case Phase.MoveCommit:
                displayMoveCommit = true;
                moveCommitSubphaseIndicator.Set(netState.lobbyInfo.subphase, hostTeam);
                displayMoveProve = true;
                moveProveSubphaseIndicator.Set(Subphase.Both, hostTeam);
                displayRankProve = true;
                rankProveSubphaseIndicator.Set(Subphase.Both, hostTeam);
                turnText = "TURN " + netState.gameState.turn.ToString();
                break;
            case Phase.MoveProve:
                displayMoveCommit = true;
                moveCommitSubphaseIndicator.Set(Subphase.None, hostTeam);
                displayMoveProve = true;
                moveProveSubphaseIndicator.Set(netState.lobbyInfo.subphase, hostTeam);
                displayRankProve = true;
                rankProveSubphaseIndicator.Set(Subphase.Both, hostTeam);
                turnText = "TURN " + netState.gameState.turn.ToString();
                break;
            case Phase.RankProve:
                displayMoveCommit = true;
                moveCommitSubphaseIndicator.Set(Subphase.None, hostTeam);
                displayMoveProve = true;
                moveProveSubphaseIndicator.Set(Subphase.None, hostTeam);
                displayRankProve = true;
                rankProveSubphaseIndicator.Set(netState.lobbyInfo.subphase, hostTeam);
                turnText = "TURN " + netState.gameState.turn.ToString();
                break;
            default:
                // Unknown/unsupported phase: leave indicators hidden without crashing
                break;
        }

        if (!netState.lobbyParameters.security_mode)
        {
            displayMoveProve = false;
            displayRankProve = false;
        }
        if (turnText is not null)
        {
            turnTitleText.text = turnText;
        }
        setupCommitSubphaseIndicator.gameObject.SetActive(displaySetupCommit);
        moveCommitSubphaseIndicator.gameObject.SetActive(displayMoveCommit);
        moveProveSubphaseIndicator.gameObject.SetActive(displayMoveProve);
        rankProveSubphaseIndicator.gameObject.SetActive(displayRankProve);
    }
}
