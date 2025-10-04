using System;
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
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        switch (action)
        {
            case NetworkStateChanged a:
            {
                // Polling is managed by UI mode (GuiGame). No action here.
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
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                await StellarManager.UpdateState();
                store.Dispatch(new UiWaitingForResponse(null));
                break;
            }
            case CommitSetupAction a:
            {
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                Result<bool> result = await StellarManager.CommitSetupRequest(a.Req);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    store.Dispatch(new UiWaitingForResponse(null));
                    return;
                }
                await StellarManager.UpdateState();
                store.Dispatch(new UiWaitingForResponse(null));
                break;
            }
            case CommitMoveAndProveAction a:
            {
                GameNetworkState gns = state.Net;
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                Result<bool> result = await StellarManager.CommitMoveRequest(a.CommitReq, a.ProveReq, gns.address, gns.lobbyInfo, gns.lobbyParameters);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    store.Dispatch(new UiWaitingForResponse(null));
                    return;
                }
                await StellarManager.UpdateState();
                store.Dispatch(new UiWaitingForResponse(null));
                break;
            }
            case ProveMoveAction a:
            {
                GameNetworkState gns = state.Net;
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                Result<bool> result = await StellarManager.ProveMoveRequest(a.Req, gns.address, gns.lobbyInfo, gns.lobbyParameters);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    store.Dispatch(new UiWaitingForResponse(null));
                    return;
                }
                await StellarManager.UpdateState();
                store.Dispatch(new UiWaitingForResponse(null));
                break;
            }
            case ProveRankAction a:
            {
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                Result<bool> result = await StellarManager.ProveRankRequest(a.Req);
                if (result.IsError)
                {
                    HandleFatalNetworkError(result.Message);
                    store.Dispatch(new UiWaitingForResponse(null));
                    return;
                }
                await StellarManager.UpdateState();
                store.Dispatch(new UiWaitingForResponse(null));
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
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                Result<bool> submit = await StellarManager.CommitSetupRequest(req);
                if (submit.IsError)
                {
                    HandleFatalNetworkError(submit.Message);
                    store.Dispatch(new UiWaitingForResponse(null));
                    return;
                }
                await StellarManager.UpdateState();
                store.Dispatch(new UiWaitingForResponse(null));
                break;
            }
            case MoveSubmit:
            {
                // Build CommitMoveReq + ProveMoveReq from LocalUiState.MovePairs
                GameNetworkState net = state.Net;
                var pairs = state.Ui?.MovePairs ?? new Dictionary<PawnId, (Vector2Int start, Vector2Int target)>();
                Debug.Log($"[NetworkEffects] MoveSubmit: enter mode={state.Mode} phase={net.lobbyInfo.phase} isMySubphase={net.IsMySubphase()} pairs={pairs.Count} busy={StellarManager.IsBusy}");
                if (!net.IsMySubphase() || pairs.Count == 0)
                {
                    Debug.Log($"[NetworkEffects] MoveSubmit: skipped isMySubphase={net.IsMySubphase()} pairs={pairs.Count}");
                    break;
                }
                List<HiddenMove> hiddenMoves = new();
                List<byte[]> moveHashes = new();
                foreach (var kv in pairs)
                {
                    PawnId pawnId = kv.Key;
                    Vector2Int start = kv.Value.start;
                    Vector2Int target = kv.Value.target;
                    // Diagnostics: verify that at submit time the start tile still holds this pawn id
                    var occ = net.GetAlivePawnFromPosChecked(start);
                    if (occ is PawnState occPawn && occPawn.pawn_id != pawnId)
                    {
                        Debug.LogWarning($"NetworkEffects: MoveSubmit mismatch at start. start={start} target={target} expectedId={pawnId} nowId={occPawn.pawn_id}");
                    }
                    else if (occ is null)
                    {
                        Debug.LogWarning($"NetworkEffects: MoveSubmit no occupant at start. start={start} target={target} expectedId={pawnId}");
                    }
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
                long tStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Debug.Log("[NetworkEffects] MoveSubmit: dispatch UiWaitingForResponse start");
                store.Dispatch(new UiWaitingForResponse(new UiWaitingForResponseData { Action = action, TimestampMs = now }));
                Debug.Log("[NetworkEffects] MoveSubmit: calling CommitMoveRequest");
                Result<bool> result = await StellarManager.CommitMoveRequest(commit, prove, net.address, net.lobbyInfo, net.lobbyParameters);
                if (result.IsError)
                {
                    Debug.LogError($"[NetworkEffects] MoveSubmit: CommitMoveRequest error: {result.Message}");
                    HandleFatalNetworkError(result.Message);
                    Debug.Log("[NetworkEffects] MoveSubmit: clearing UiWaitingForResponse (error)");
                    store.Dispatch(new UiWaitingForResponse(null));
                    return;
                }
                long tAfterCommit = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Debug.Log($"[NetworkEffects] MoveSubmit: CommitMoveRequest ok in {tAfterCommit - tStart}ms");
                Debug.Log("[NetworkEffects] MoveSubmit: calling UpdateState");
                await StellarManager.UpdateState();
                long tAfterUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Debug.Log($"[NetworkEffects] MoveSubmit: UpdateState ok in {tAfterUpdate - tAfterCommit}ms (total {tAfterUpdate - tStart}ms)");
                Debug.Log("[NetworkEffects] MoveSubmit: clearing UiWaitingForResponse (success)");
                store.Dispatch(new UiWaitingForResponse(null));
                Debug.Log("[NetworkEffects] MoveSubmit: exit");
                break;
            }
        }
    }

    static void HandleFatalNetworkError(string message)
    {
        string msg = string.IsNullOrEmpty(message) ? "You're now in Offline Mode." : message;
        // Ensure polling is paused via UI transitions; do not manage here
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


