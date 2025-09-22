using System.Collections.Generic;
using System.Threading.Tasks;
using Contract;
using UnityEngine;

public sealed class NetworkEffects : IGameEffect
{
    GameStore store;

    public void Initialize(GameStore s)
    {
        store = s;
    }

    public async void OnActionAndEvents(GameAction action, IReadOnlyList<GameEvent> events, GameSnapshot state)
    {
        switch (action)
        {
            case RefreshRequested:
            {
                await StellarManager.UpdateState();
                break;
            }
            case CommitSetupAction a:
            {
                Result<bool> result = await StellarManager.CommitSetupRequest(a.Req);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    return;
                }
                await StellarManager.UpdateState();
                break;
            }
            case CommitMoveAndProveAction a:
            {
                GameNetworkState gns = state.Net;
                Result<bool> result = await StellarManager.CommitMoveRequest(a.CommitReq, a.ProveReq, gns.address, gns.lobbyInfo, gns.lobbyParameters);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    return;
                }
                await StellarManager.UpdateState();
                break;
            }
            case ProveMoveAction a:
            {
                GameNetworkState gns = state.Net;
                Result<bool> result = await StellarManager.ProveMoveRequest(a.Req, gns.address, gns.lobbyInfo, gns.lobbyParameters);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    return;
                }
                await StellarManager.UpdateState();
                break;
            }
            case ProveRankAction a:
            {
                Result<bool> result = await StellarManager.ProveRankRequest(a.Req);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    return;
                }
                await StellarManager.UpdateState();
                break;
            }
            case SetupSubmit:
            {
                // Build CommitSetupReq from LocalUiState.PendingCommits
                GameNetworkState net = state.Net;
                var pending = state.Ui?.PendingCommits ?? new Dictionary<PawnId, Rank?>();
                List<HiddenRank> hiddenRanks = new();
                List<SetupCommit> commits = new();
                foreach (var kv in pending)
                {
                    PawnId pawnId = kv.Key;
                    Rank? maybeRank = kv.Value;
                    if (maybeRank is not Rank rank)
                        continue; // skip uncommitted
                    HiddenRank hiddenRank = new()
                    {
                        pawn_id = pawnId,
                        rank = rank,
                        salt = Globals.RandomSalt(),
                    };
                    hiddenRanks.Add(hiddenRank);
                    commits.Add(new SetupCommit
                    {
                        pawn_id = pawnId,
                        hidden_rank_hash = SCUtility.Get16ByteHash(hiddenRank),
                    });
                }
                List<byte[]> leaves = new();
                foreach (var c in commits) leaves.Add(c.hidden_rank_hash);
                (byte[] root, MerkleTree tree) = MerkleTree.BuildMerkleTree(leaves.ToArray());
                if (net.lobbyParameters.security_mode)
                {
                    // Cache proofs for later prove-rank
                    List<CachedRankProof> proofs = new();
                    for (int i = 0; i < commits.Count; i++)
                    {
                        HiddenRank hiddenRank = hiddenRanks[i];
                        MerkleProof proof = tree.GenerateProof((uint)i);
                        proofs.Add(new CachedRankProof { hidden_rank = hiddenRank, merkle_proof = proof });
                    }
                    CacheManager.StoreHiddenRanksAndProofs(proofs, net.address, net.lobbyInfo.index);
                }
                CommitSetupReq req = new CommitSetupReq
                {
                    lobby_id = net.lobbyInfo.index,
                    rank_commitment_root = root,
                    zz_hidden_ranks = net.lobbyParameters.security_mode ? new HiddenRank[]{} : hiddenRanks.ToArray(),
                };
                Result<bool> submit = await StellarManager.CommitSetupRequest(req);
                if (submit.IsError)
                {
                    HandleFatalNetworkError(submit.Message);
                    return;
                }
                await StellarManager.UpdateState();
                break;
            }
        }
    }

    static void HandleFatalNetworkError(string message)
    {
        string msg = string.IsNullOrEmpty(message) ? "You're now in Offline Mode." : message;
        // Ensure polling stays paused on fatal error
        StellarManager.SetPolling(false);
        MenuController menuController = UnityEngine.Object.FindFirstObjectByType<MenuController>();
        if (menuController != null)
        {
            menuController.OpenMessageModal($"Network Unavailable\n{msg}");
            menuController.ExitGame();
        }
        else
        {
            Debug.LogError($"MenuController not found. Error: {msg}");
        }
    }
}


