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
            case NetworkStateChanged a:
            {
                // Toggle polling based on whose subphase it is
                bool shouldPoll = !a.Net.IsMySubphase();
                StellarManager.SetPolling(shouldPoll);
                // Auto-submit proofs when entering/progressing MoveProve or RankProve
                if (StellarManager.IsBusy)
                {
                    break;
                }
                GameNetworkState net = state.Net;
                // Auto ProveMove when in MoveProve and it's our subphase
                if (net.lobbyInfo.phase == Phase.MoveProve && net.IsMySubphase())
                {
                    var hashes = net.GetUserMove().move_hashes;
                    if (hashes != null && hashes.Length > 0)
                    {
                        List<HiddenMove> hiddenMoves = new();
                        bool missing = false;
                        foreach (var h in hashes)
                        {
                            var maybe = CacheManager.GetHiddenMove(h);
                            if (maybe is HiddenMove hm)
                            {
                                hiddenMoves.Add(hm);
                            }
                            else
                            {
                                Debug.LogWarning("Auto ProveMove: missing hidden move for a committed hash; skipping auto-submit");
                                missing = true;
                                break;
                            }
                        }
                        if (!missing)
                        {
                            ProveMoveReq prove = new()
                            {
                                lobby_id = net.lobbyInfo.index,
                                move_proofs = hiddenMoves.ToArray(),
                            };
                            store.Dispatch(new ProveMoveAction(prove));
                        }
                    }
                    break;
                }
                // Auto ProveRank when in RankProve and it's our subphase
                if (net.lobbyInfo.phase == Phase.RankProve && net.IsMySubphase())
                {
                    var needed = net.GetUserMove().needed_rank_proofs;
                    if (needed != null && needed.Length > 0)
                    {
                        List<HiddenRank> hiddenRanks = new();
                        List<MerkleProof> merkleProofs = new();
                        bool missing = false;
                        foreach (PawnId pawnId in needed)
                        {
                            if (CacheManager.GetHiddenRankAndProof(pawnId) is not CachedRankProof cached)
                            {
                                Debug.LogWarning($"Auto ProveRank: missing cached rank/proof for pawn {pawnId}; skipping auto-submit");
                                missing = true;
                                break;
                            }
                            hiddenRanks.Add(cached.hidden_rank);
                            merkleProofs.Add(cached.merkle_proof);
                        }
                        if (!missing)
                        {
                            ProveRankReq req = new()
                            {
                                lobby_id = net.lobbyInfo.index,
                                hidden_ranks = hiddenRanks.ToArray(),
                                merkle_proofs = merkleProofs.ToArray(),
                            };
                            store.Dispatch(new ProveRankAction(req));
                        }
                    }
                    break;
                }
                break;
            }
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
            case MoveSubmit:
            {
                // Build CommitMoveReq + ProveMoveReq from LocalUiState.MovePairs
                GameNetworkState net = state.Net;
                var pairs = state.Ui?.MovePairs ?? new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>();
                if (!net.IsMySubphase() || pairs.Count == 0)
                {
                    break;
                }
                List<HiddenMove> hiddenMoves = new();
                List<byte[]> moveHashes = new();
                foreach (var kv in pairs)
                {
                    PawnId pawnId = kv.Key;
                    Vector2Int start = kv.Value.start;
                    Vector2Int target = kv.Value.target;
                    HiddenMove hm = new()
                    {
                        pawn_id = pawnId,
                        salt = Globals.RandomSalt(),
                        start_pos = start,
                        target_pos = target,
                    };
                    CacheManager.StoreHiddenMove(hm, net.address, net.lobbyInfo.index);
                    hiddenMoves.Add(hm);
                    moveHashes.Add(SCUtility.Get16ByteHash(hm));
                }
                CommitMoveReq commit = new()
                {
                    lobby_id = net.lobbyInfo.index,
                    move_hashes = moveHashes.ToArray(),
                };
                ProveMoveReq prove = new()
                {
                    lobby_id = net.lobbyInfo.index,
                    move_proofs = hiddenMoves.ToArray(),
                };
                Result<bool> result = await StellarManager.CommitMoveRequest(commit, prove, net.address, net.lobbyInfo, net.lobbyParameters);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
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


