using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Contract;
using Stellar;
using Stellar.RPC;
using Stellar.Utilities;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public static class StellarManager
{
    public static event Action OnNetworkStateUpdated;
    public static event Action<GameNetworkState, NetworkDelta> OnGameStateBeforeApplied;
    public static event Action<GameNetworkState, NetworkDelta> OnGameStateAfterApplied;
    public static event Action<TrustLineEntry> OnAssetsUpdated;
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    
    public static NetworkState networkState;

    static TaskInfo currentTask;
    // Tracks tasks that were canceled/aborted so EndTask can ignore them safely
    static HashSet<Guid> canceledTaskIds = new HashSet<Guid>();
    // Centralized polling state
    static bool isPolling;
    static Coroutine pollingCoroutine;
    static bool desiredPolling;
    static int pollingHoldCount;
    
    // True while a Stellar task is in progress
    public static bool IsBusy => currentTask != null;

    static void DebugLogPoll(string message)
    {
        if (ResourceRoot.DefaultSettings.pollingLogging)
        {
            Debug.Log(message);
        }
    }
    public static void Initialize()
    {
        DefaultSettings defaultSettings = ResourceRoot.DefaultSettings;
        currentTask = null;
        canceledTaskIds.Clear();
        StellarDotnet.Initialize(defaultSettings.defaultHostSneed, defaultSettings.defaultContractAddress);
    }
    public static AccountAddress GetHostAddress()
    {
        MuxedAccount.KeyTypeEd25519 account = MuxedAccount.FromSecretSeed(StellarDotnet.sneed);
        string publicAddress = StrKey.EncodeStellarAccountId(account.PublicKey);
        return AccountAddress.Parse(publicAddress);
    }
    
    public static async Task<bool> UpdateState(bool showTask = true)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("UpdateState");
        TaskInfo getNetworkStateTask = null;
        if (showTask)
        {
            getNetworkStateTask = SetCurrentTask("ReqNetworkState");
        }
        NetworkState previousNetworkState = networkState;
        NetworkState newNetworkState = await StellarDotnet.ReqNetworkState(tracker);
        if (showTask && getNetworkStateTask != null)
        {
            EndTask(getNetworkStateTask);
        }
        if (newNetworkState.user is User user && user.current_lobby != 0)
        {
            if (!newNetworkState.lobbyInfo.HasValue || !newNetworkState.lobbyParameters.HasValue)
            {
                Debug.LogWarning($"UpdateState(): user.current_lobby is set to {newNetworkState.user?.current_lobby} but lobby data is missing - likely expired");
                User updatedUser = user;
                updatedUser.current_lobby = new LobbyId(0);
                newNetworkState.user = updatedUser;
                Debug.Log("Set current_lobby to null due to expired/missing lobby");
            }
        }
        tracker.EndOperation();
        if (!showTask)
        {
            DebugLogPoll(tracker.GetReport());
        }
        else
        {
            Debug.Log(tracker.GetReport());
        }
        bool stateChanged = HasMeaningfulChange(previousNetworkState, newNetworkState);
        networkState = newNetworkState;
        if (stateChanged)
        {
            NetworkDelta delta = ComputeDelta(previousNetworkState, newNetworkState);
            if (HasCompleteGameData(newNetworkState))
            {
                GameNetworkState game = new(newNetworkState);
                OnGameStateBeforeApplied?.Invoke(game, delta);
                OnNetworkStateUpdated?.Invoke();
                OnGameStateAfterApplied?.Invoke(game, delta);
            }
            else
            {
                OnNetworkStateUpdated?.Invoke();
            }
        }
        return stateChanged;
    }
    
    public static async Task<bool> SetContractAddress(string contractId)
    {
        StellarDotnet.SetContractId(contractId);
        await UpdateState();
        return true;
    }

    public static async Task<bool> SetSneed(string accountSneed)
    {
        StellarDotnet.SetSneed(accountSneed);
        await UpdateState();
        return true;
    }

    public static string GetUserAddress()
    {
        return StellarDotnet.sneed != null ? StellarDotnet.userAddress : null;
    }

    public static string GetContractAddress()
    {
        return StellarDotnet.contractAddress;
    }

    public static string GetCurrentSneed()
    {
        return StellarDotnet.sneed;
    }

    public static async Task<int> MakeLobbyRequest(LobbyParameters parameters)
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"MakeLobbyRequest");
        MakeLobbyReq req = new()
        {
            lobby_id = GenerateLobbyId(),
            parameters = parameters,
        };
        TaskInfo task = SetCurrentTask("Invoke make_lobby");
        try
        {
            (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await StellarDotnet.CallContractFunction("make_lobby", req, tracker);
            EndTask(task);
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }

    public static async Task<int> LeaveLobbyRequest()
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"LeaveLobbyRequest");
        TaskInfo task = SetCurrentTask("Invoke leave_lobby");
        try
        {
            (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await StellarDotnet.CallContractFunction("leave_lobby", new IScvMapCompatable[] {}, tracker);
            EndTask(task);
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }
    
    public static async Task<int> JoinLobbyRequest(LobbyId lobbyId)
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"JoinLobbyRequest");
        JoinLobbyReq req = new()
        {
            lobby_id = lobbyId,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        try
        {
            (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await StellarDotnet.CallContractFunction("join_lobby", req, tracker);
            EndTask(task);
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }
    
    public static async Task<PackedHistory?> GetPackedHistory(uint lobbyId)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("GetPackedHistory");
        TaskInfo task = SetCurrentTask("ReqPackedHistory");
        PackedHistory? history = await StellarDotnet.ReqPackedHistory(lobbyId, tracker);
        EndTask(task);
        tracker.EndOperation();
        return history;
    }
    
    public static async Task<int> CommitSetupRequest(CommitSetupReq req)
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"CommitSetupRequest");
        TaskInfo task = SetCurrentTask("Invoke commit_setup");
        try
        {
            (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results  = await StellarDotnet.CallContractFunction("commit_setup", req, tracker);
            EndTask(task);
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }

    public static async Task<int> CommitMoveRequest(CommitMoveReq commitMoveReq, ProveMoveReq proveMoveReq)
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("CommitMoveRequest");
        Debug.Assert(commitMoveReq.lobby_id == proveMoveReq.lobby_id);
        // NOTE: check lobbyInfo just to see if we should batch in prove_move or not
        TaskInfo reqLobbyInfoTask = SetCurrentTask("ReqLobbyInfo");
        try
        {
            LobbyInfo? preRequestLobbyInfoResult = await StellarDotnet.ReqLobbyInfo(commitMoveReq.lobby_id, tracker);
            EndTask(reqLobbyInfoTask);
            if (preRequestLobbyInfoResult is not LobbyInfo preRequestLobbyInfo)
            {
                return -999;
            }
            bool isHost = preRequestLobbyInfo.IsHost(StellarDotnet.userAddress);
            Subphase mySubphase = isHost ? Subphase.Host : Subphase.Guest;
            // we send prove_move too only if the server is waiting for us or if secure_mode false
            bool sendProveMoveToo = preRequestLobbyInfo.phase == Phase.MoveCommit && preRequestLobbyInfo.subphase == mySubphase;
            (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results;
            if (sendProveMoveToo || networkState.lobbyParameters?.security_mode == false)
            {
                TaskInfo task = SetCurrentTask("Invoke commit_move_and_prove_move");
                results = await StellarDotnet.CallContractFunction("commit_move_and_prove_move", new IScvMapCompatable[] {commitMoveReq, proveMoveReq}, tracker);
                EndTask(task);
            }
            else
            {
                TaskInfo task = SetCurrentTask("Invoke commit_move");
                results = await StellarDotnet.CallContractFunction("commit_move", commitMoveReq, tracker);
                EndTask(task);
            }
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }

    public static async Task<int> ProveMoveRequest(ProveMoveReq proveMoveReq)
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveMoveRequest");
        bool sendRankProofToo = false;
        bool isSecurityMode = networkState.lobbyParameters?.security_mode == true;
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results;
        UserMove? simulatedMove = null;
        try
        {
            if (isSecurityMode && networkState.lobbyInfo.HasValue)
            {
                LobbyInfo lobby = networkState.lobbyInfo.Value;
                bool canSimulate = lobby.phase == Phase.MoveProve && lobby.subphase != Subphase.Both;
                if (canSimulate)
                {
                    TaskInfo reqLobbyInfoTask = SetCurrentTask("Simulate simulate_collisions");
                    (_, SimulateTransactionResult collisionResult) = await StellarDotnet.SimulateContractFunction("simulate_collisions", new IScvMapCompatable[] { proveMoveReq }, tracker);
                    EndTask(reqLobbyInfoTask);
                    if (collisionResult.Error != null)
                    {
                        throw new Exception($"collisionResult failed for some reason {collisionResult.Error}");
                    }
                    SCVal scVal = collisionResult.Results.FirstOrDefault()!.Result;
                    UserMove move = SCUtility.SCValToNative<UserMove>(scVal);
                    Debug.Log($"simulated move move needed rank proofs count: {move.needed_rank_proofs.Length} move hashes count: {move.move_hashes.Length} move proofs count: {move.move_proofs.Length}");
                    sendRankProofToo = move.needed_rank_proofs != null && move.needed_rank_proofs.Length > 0;
                    simulatedMove = move;
                }
            }

            if (sendRankProofToo && simulatedMove is UserMove simulatedMoveVal)
            {
                Debug.Log("sendRankProofToo is true");
                List<HiddenRank> hiddenRanks = new List<HiddenRank>();
                List<MerkleProof> merkleProofs = new List<MerkleProof>();
                foreach (PawnId pawnId in simulatedMoveVal.needed_rank_proofs ?? Array.Empty<PawnId>())
                {
                    if (CacheManager.GetHiddenRankAndProof(pawnId) is not CachedRankProof cachedRankProof)
                    {
                        Debug.LogError($"cachemanager could not find pawn {pawnId}");
                        throw new Exception($"cachemanager could not find pawn {pawnId}");
                    }
                    Debug.Log($"adding hidden rank for {cachedRankProof.hidden_rank.pawn_id}");
                    hiddenRanks.Add(cachedRankProof.hidden_rank);
                    Debug.Log($"Adding merkle proof for {cachedRankProof.hidden_rank.pawn_id}");
                    merkleProofs.Add(cachedRankProof.merkle_proof);
                }

                if (simulatedMove == null)
                {
                    Debug.LogError("could not find simulatedMove");
                    throw new Exception("could not find simulatedMove");
                }
                if (simulatedMove is UserMove move)
                {
                    if (hiddenRanks.Count != move.needed_rank_proofs.Length)
                    {
                        Debug.LogError($"hiddenRanks count {hiddenRanks.Count} expected {move.needed_rank_proofs.Length}");
                        throw new Exception($"hiddenRanks count {hiddenRanks.Count} expected {move.needed_rank_proofs.Length}");
                    }

                    if (merkleProofs.Count != move.needed_rank_proofs.Length)
                    {
                        Debug.LogError($"merkleProofs count {merkleProofs.Count} expected {move.needed_rank_proofs.Length}");
                        throw new Exception($"merkleProofs count {merkleProofs.Count} expected {move.needed_rank_proofs.Length}");
                    }
                }
                ProveRankReq proveRankReq = new ProveRankReq
                {
                    hidden_ranks = hiddenRanks.ToArray(),
                    lobby_id = proveMoveReq.lobby_id,
                    merkle_proofs = merkleProofs.ToArray(),
                };
                TaskInfo task = SetCurrentTask("Invoke prove_move_and_prove_rank");
                results = await StellarDotnet.CallContractFunction("prove_move_and_prove_rank", new IScvMapCompatable[] { proveMoveReq, proveRankReq }, tracker);
                EndTask(task);
            }
            else
            {
                TaskInfo task = SetCurrentTask("Invoke prove_move");
                results = await StellarDotnet.CallContractFunction("prove_move", proveMoveReq, tracker);
                EndTask(task);
            }
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }

    public static async Task<int> ProveRankRequest(ProveRankReq req)
    {
        // Pause polling during contract invocation
        PushPollingHold();
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveRankRequest");
        TaskInfo task = SetCurrentTask("Invoke Prove_rank");
        try
        {
            (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await StellarDotnet.CallContractFunction("prove_rank", req, tracker);
            EndTask(task);
            ResultCode code = ProcessTransactionResult(results);
            tracker.EndOperation();
            Debug.Log(tracker.GetReport());
            await UpdateState();
            return (int)code;
        }
        finally
        {
            PopPollingHold();
        }
    }
    
    public static async Task<AccountEntry> GetAccount(string key)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"GetAccount");
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        AccountEntry result = await StellarDotnet.ReqAccountEntry(MuxedAccount.FromAccountId(key));
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        return result;
    }

    public static async Task<TrustLineEntry> GetAssets(string userId)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"GetAssets");
        MuxedAccount.KeyTypeEd25519 userAccount = MuxedAccount.FromAccountId(userId);
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        LedgerEntry.dataUnion.Trustline result = await StellarDotnet.GetAssets(userAccount, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        OnAssetsUpdated?.Invoke(result.trustLine);
        return result.trustLine;
    }

    // Start/stop centralized polling. Polling will automatically skip while busy
    public static void SetPolling(bool enable)
    {
        desiredPolling = enable;
        if (enable && pollingHoldCount == 0)
        {
            if (pollingCoroutine == null)
            {
                isPolling = true;
                pollingCoroutine = CoroutineRunner.instance.StartCoroutine(PollCoroutine());
            }
        }
        else
        {
            isPolling = false;
            if (pollingCoroutine != null)
            {
                CoroutineRunner.instance.StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }
        }
    }

    static IEnumerator PollCoroutine()
    {
        while (isPolling)
        {
            // Avoid overlapping with any in-flight task
            if (!IsBusy)
            {
                var task = UpdateState(false);
                while (!task.IsCompleted)
                {
                    yield return null;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
        pollingCoroutine = null;
    }

    static bool HasCompleteGameData(NetworkState state)
    {
        return state.inLobby && state.lobbyInfo.HasValue && state.lobbyParameters.HasValue && state.gameState.HasValue;
    }

    static NetworkDelta ComputeDelta(NetworkState previous, NetworkState current)
    {
        NetworkDelta delta = new NetworkDelta();
        delta.InLobbyChanged = previous.inLobby != current.inLobby;
        bool prevComplete = HasCompleteGameData(previous);
        bool currComplete = HasCompleteGameData(current);
        if (prevComplete && currComplete)
        {
            string prevLobbyInfo = SCValXdr.EncodeToBase64(previous.lobbyInfo!.Value.ToScvMap());
            string currLobbyInfo = SCValXdr.EncodeToBase64(current.lobbyInfo!.Value.ToScvMap());
            delta.LobbyInfoChanged = prevLobbyInfo != currLobbyInfo;

            string prevGameState = SCValXdr.EncodeToBase64(previous.gameState!.Value.ToScvMap());
            string currGameState = SCValXdr.EncodeToBase64(current.gameState!.Value.ToScvMap());
            delta.GameStateChanged = prevGameState != currGameState;

            delta.PhaseChanged = previous.lobbyInfo!.Value.phase != current.lobbyInfo!.Value.phase;
            delta.TurnChanged = previous.gameState!.Value.turn != current.gameState!.Value.turn;
            if (delta.TurnChanged)
            {
                GameNetworkState prevGns = new GameNetworkState(previous);
                GameNetworkState currGns = new GameNetworkState(current);
                delta.TurnResolve = BuildTurnResolveDeltaSimple(prevGns, currGns);
            }
        }
        else
        {
            delta.LobbyInfoChanged = (previous.lobbyInfo.HasValue != current.lobbyInfo.HasValue) || current.inLobby;
            delta.GameStateChanged = (previous.gameState.HasValue != current.gameState.HasValue) || current.inLobby;
            delta.PhaseChanged = delta.LobbyInfoChanged || current.inLobby;
            delta.TurnChanged = delta.GameStateChanged || current.inLobby;
        }
        return delta;
    }

    static Dictionary<Vector2Int, (SnapshotPawnDelta, SnapshotPawnDelta)> ComputeCollisions(Dictionary<PawnId, SnapshotPawnDelta> pawnDeltas)
    {
        Dictionary<Vector2Int, (SnapshotPawnDelta, SnapshotPawnDelta)> collisions = new();
        // Map intended positions to team deltas
        Dictionary<Vector2Int, List<SnapshotPawnDelta>> posToRed = new();
        Dictionary<Vector2Int, List<SnapshotPawnDelta>> posToBlue = new();
        
        // Build intention maps (consider pawns that were alive at turn start)
        foreach (var kvp in pawnDeltas)
        {
            SnapshotPawnDelta d = kvp.Value;
            if (!d.preAlive) continue;
            Vector2Int intended = d.postPos; // post is the target; if no move, equals pre
            Team team = d.pawnId.GetTeam();
            if (team == Team.RED)
            {
                if (!posToRed.TryGetValue(intended, out var list))
                {
                    list = new List<SnapshotPawnDelta>();
                    posToRed[intended] = list;
                }
                list.Add(d);
            }
            else
            {
                if (!posToBlue.TryGetValue(intended, out var list))
                {
                    list = new List<SnapshotPawnDelta>();
                    posToBlue[intended] = list;
                }
                list.Add(d);
            }
        }
        
        // Same-target collisions: exactly one RED and one BLUE intend the same tile
        foreach (var kv in posToRed)
        {
            Vector2Int pos = kv.Key;
            List<SnapshotPawnDelta> redList = kv.Value;
            if (redList.Count != 1) continue;
            if (!posToBlue.TryGetValue(pos, out var blueList)) continue;
            if (blueList.Count != 1) continue;
            SnapshotPawnDelta red = redList[0];
            SnapshotPawnDelta blue = blueList[0];
            if (!collisions.ContainsKey(pos))
            {
                // Order as (RED, BLUE)
                var pair = red.pawnId.GetTeam() == Team.RED ? (red, blue) : (blue, red);
                collisions[pos] = pair;
            }
        }
        
        // Swap collisions: a moving RED and a moving BLUE swap start positions
        List<SnapshotPawnDelta> moving = new List<SnapshotPawnDelta>();
        foreach (var kvp in pawnDeltas)
        {
            SnapshotPawnDelta d = kvp.Value;
            if (!d.preAlive) continue;
            if (d.prePos != d.postPos) moving.Add(d);
        }
        int n = moving.Count;
        for (int i = 0; i < n; i++)
        {
            var a = moving[i];
            Team aTeam = a.pawnId.GetTeam();
            for (int j = i + 1; j < n; j++)
            {
                var b = moving[j];
                Team bTeam = b.pawnId.GetTeam();
                if (aTeam == bTeam) continue;
                if (a.postPos == b.prePos && b.postPos == a.prePos)
                {
                    Vector2Int pos = a.postPos; // target position of one side
                    SnapshotPawnDelta red = aTeam == Team.RED ? a : b;
                    SnapshotPawnDelta blue = aTeam == Team.RED ? b : a;
                    if (!collisions.ContainsKey(pos))
                    {
                        collisions[pos] = (red, blue);
                    }
                }
            }
        }
        
        return collisions;
    }
    static TurnResolveDelta BuildTurnResolveDeltaSimple(GameNetworkState previous, GameNetworkState current)
    {
        // pre is the state before moves or changes have been applied
        TurnSnapshot preSnapshot = new(previous.gameState);
        // post is the state after moves and changes have been applied
        TurnSnapshot postSnapshot = new(current.gameState);
        Dictionary<PawnId, SnapshotPawnDelta> pawnDeltas = new();
        List<BattleEvent> battles = new();
        // determine moved pawns
        for (int i = 0; i < preSnapshot.pawns.Length; i++)
        {
            SnapshotPawn prePawn = preSnapshot.pawns[i];
            SnapshotPawn postPawn = postSnapshot.pawns[i];
            SnapshotPawnDelta pawnDelta = new(prePawn, postPawn);
            pawnDeltas[pawnDelta.pawnId] = pawnDelta;
        }
        Dictionary<Vector2Int, (SnapshotPawnDelta, SnapshotPawnDelta)> collisionPairs = ComputeCollisions(pawnDeltas);
        
        // Build battles from collision pairs
        foreach (var kv in collisionPairs)
        {
            Vector2Int pos = kv.Key;
            var (a, b) = kv.Value; // a is RED, b is BLUE by construction
            List<PawnId> participantsList = new List<PawnId> { a.pawnId, b.pawnId };
            List<PawnId> revealedList = new List<PawnId>();
            List<PawnId> deadList = new List<PawnId>();
            if (a.postRevealed && !a.preRevealed) revealedList.Add(a.pawnId);
            if (b.postRevealed && !b.preRevealed) revealedList.Add(b.pawnId);
            if (!a.postAlive && a.preAlive) deadList.Add(a.pawnId);
            if (!b.postAlive && b.preAlive) deadList.Add(b.pawnId);
            battles.Add(new BattleEvent
            {
                participants = participantsList.ToArray(),
                revealed = revealedList.ToArray(),
                dead = deadList.ToArray(),
                winner_pos = pos,
                revealedRanks = Array.Empty<(PawnId pawn, Rank rank, bool wasHidden)>(),
            });
        }
        
        return new TurnResolveDelta {
            pre = preSnapshot,
            post = postSnapshot,
            pawnDeltas = pawnDeltas.Values.ToArray(),
            battles = battles.ToArray(),
        };
    }
    
    static void PushPollingHold()
    {
        pollingHoldCount++;
        if (pollingCoroutine != null)
        {
            isPolling = false;
            CoroutineRunner.instance.StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
    }

    static void PopPollingHold()
    {
        pollingHoldCount = Math.Max(0, pollingHoldCount - 1);
        if (pollingHoldCount == 0 && desiredPolling && pollingCoroutine == null)
        {
            isPolling = true;
            pollingCoroutine = CoroutineRunner.instance.StartCoroutine(PollCoroutine());
        }
    }

    static bool HasMeaningfulChange(NetworkState previous, NetworkState current)
    {
        // If lobby/game presence changed, it's a change
        bool prevInLobby = previous.inLobby;
        bool currInLobby = current.inLobby;
        if (prevInLobby != currInLobby)
        {
            return true;
        }
        // When both have complete game data, compare lobbyInfo and gameState by their canonical encodings
        bool prevComplete = HasCompleteGameData(previous);
        bool currComplete = HasCompleteGameData(current);
        if (prevComplete && currComplete)
        {
            string prevLobbyInfo = SCValXdr.EncodeToBase64(previous.lobbyInfo!.Value.ToScvMap());
            string currLobbyInfo = SCValXdr.EncodeToBase64(current.lobbyInfo!.Value.ToScvMap());
            if (prevLobbyInfo != currLobbyInfo)
            {
                return true;
            }
            string prevGameState = SCValXdr.EncodeToBase64(previous.gameState!.Value.ToScvMap());
            string currGameState = SCValXdr.EncodeToBase64(current.gameState!.Value.ToScvMap());
            if (prevGameState != currGameState)
            {
                return true;
            }
            return false;
        }
        if (prevComplete != currComplete)
        {
            return true;
        }
        // Fallback: compare simplified string representations
        return !string.Equals(previous.ToString(), current.ToString(), StringComparison.Ordinal);
    }

    static TaskInfo SetCurrentTask(string message)
    {
        if (currentTask != null)
        {
            throw new Exception("Task is already set");
        }
        TaskInfo taskInfo = new();
        taskInfo.taskId = Guid.NewGuid();
        taskInfo.taskMessage = message;
        currentTask = taskInfo;
        OnTaskStarted?.Invoke(taskInfo);
        return taskInfo;
    }

    static void EndTask(TaskInfo taskInfo)
    {
        if (taskInfo == null)
        {
            throw new Exception("Task is null");
        }
        // If there is no current task, this may have been canceled earlier
        if (currentTask == null)
        {
            if (canceledTaskIds.Remove(taskInfo.taskId))
            {
                // Swallow end for canceled task
                return;
            }
            throw new Exception("Task is not set");
        }
        // If task IDs don't match, check if the ending task was canceled
        if (currentTask.taskId != taskInfo.taskId)
        {
            if (canceledTaskIds.Remove(taskInfo.taskId))
            {
                // Swallow end for canceled task
                return;
            }
            throw new Exception("Task is not taskId");
        }
        OnTaskEnded?.Invoke(currentTask);
        currentTask = null;
    }

    // Allows callers (e.g., when leaving the game view) to drop any in-flight task so
    // subsequent calls can proceed without "Task is already set" exceptions.
    public static void AbortCurrentTask()
    {
        if (currentTask != null)
        {
            canceledTaskIds.Add(currentTask.taskId);
            // Notify listeners that task ended to clean up UI state
            OnTaskEnded?.Invoke(currentTask);
            currentTask = null;
        }
    }
    
    // NOTE: lobby IDs will be done server side in the future
    static LobbyId GenerateLobbyId()
    {
        uint value = (uint)Random.Range(100000, 1000000); // 6 digit number
        return new LobbyId(value);
    }

    static ResultCode ProcessTransactionResult((SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results)
    {
        return ProcessTransactionResult(results.Item1, results.Item2, results.Item3);
    }

    static ResultCode ProcessTransactionResult(SimulateTransactionResult simResult, SendTransactionResult sendResult,
        GetTransactionResult getResult)
    {
        if (simResult == null)
        {
            Debug.LogError("ProcessTransactionResult: simulation failed to send");
            return ResultCode.SIM_SEND_FAILED;
        }

        if (simResult.Error != null)
        {
            List<int> errorCodes = new();
            foreach (DiagnosticEvent diag in simResult.DiagnosticEvents.Where(diag => !diag.inSuccessfulContractCall))
            {
                Debug.LogError(JsonUtility.ToJson(diag));
                ContractEvent.bodyUnion.case_0 body = (ContractEvent.bodyUnion.case_0)diag._event.body;
                foreach (SCVal topic in body.v0.topics)
                {
                    if (topic is not SCVal.ScvError { error: SCError.SceContract contractError }) continue;
                    int code = (int)contractError.contractCode.InnerValue;
                    errorCodes.Add(code);
                }
            }

            if (errorCodes.Count > 1)
            {
                Debug.LogError("ProcessTransactionResult failed to simulate with more than 1 error");
            }

            foreach (int err in errorCodes)
            {
                ErrorCode errorCode = (ErrorCode)err;
                Debug.LogError(errorCode.ToString());
            }

            return ResultCode.SIM_REJECTED;
        }

        if (sendResult == null)
        {
            return ResultCode.TX_SEND_FAILED;
        }

        if (sendResult.ErrorResult != null)
        {
            return ResultCode.TX_REJECTED;
        }

        if (getResult == null)
        {
            return ResultCode.GET_FAILED_OR_TIMED_OUT;
        }
        switch (getResult.Status)
        {
            case GetTransactionResult_Status.SUCCESS:
                return ResultCode.SUCCESS;
            case GetTransactionResult_Status.NOT_FOUND:
                return ResultCode.GET_NOT_FOUND;
            case GetTransactionResult_Status.FAILED:
                Debug.LogError(getResult.ResultXdr);
                if (getResult.TransactionResult.result is TransactionResult.resultUnion.TxFAILED txFailed)
                {
                    if (txFailed.results.FirstOrDefault() is OperationResult.OpINNER { tr: OperationResult.trUnion.InvokeHostFunction invokeHostFunction })
                    {
                        Debug.LogError(invokeHostFunction.invokeHostFunctionResult.Discriminator);
                    }
                }
                return ResultCode.GET_FAILED_OR_TIMED_OUT;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public class TaskInfo
{
    public Guid taskId;
    public string taskMessage;
}

public enum ResultCode
{
    SUCCESS,
    SIM_SEND_FAILED,
    SIM_REJECTED,
    TX_SEND_FAILED,
    TX_REJECTED,
    GET_NOT_FOUND,
    GET_FAILED_OR_TIMED_OUT,
    GET_INSUFFICIENT_REFUNDABLE_FEE,
    
    TRANSACTION_NOT_FOUND,
    TRANSACTION_SIM_REJECTED_BY_CONTRACT,
    TRANSACTION_FAILED_MISC,
}

public struct TurnResolveDelta
{
    public TurnSnapshot pre;
    public TurnSnapshot post;
    public SnapshotPawnDelta[] pawnDeltas;
    public BattleEvent[] battles;
}

public struct TurnSnapshot
{
    public uint turn;
    public SnapshotPawn[] pawns;

    public TurnSnapshot(GameState gs)
    {
        turn = gs.turn;
        pawns = gs.pawns
            .Select(p => new SnapshotPawn
            {
                pawnId = p.pawn_id,
                team = p.GetTeam(),
                pos = p.pos,
                alive = p.alive,
                revealed = p.zz_revealed,
                rank = p.rank,
                moved = p.moved,
                movedScout = p.moved_scout,
            })
            .ToArray();
    }

    public SnapshotPawn GetPawn(PawnId pawnId) 
    {
        return pawns.FirstOrDefault(p => p.pawnId == pawnId);
    }
    public SnapshotPawn? GetPawnAtPos(Vector2Int pos)
    {
        return pawns.FirstOrDefault(p => p.pos == pos);
    }
}

public struct SnapshotPawn
{
    public PawnId pawnId;
    public Team team;
    public Vector2Int pos;
    public bool alive;
    public bool revealed;
    public Rank? rank;
    public bool moved;
    public bool movedScout;
}

public struct SnapshotPawnDelta
{
    public PawnId pawnId;
    public Vector2Int prePos;
    public Vector2Int postPos;
    public bool preAlive;
    public bool postAlive;
    public bool preRevealed;
    public bool postRevealed;

    public SnapshotPawnDelta(SnapshotPawn pre, SnapshotPawn post)
    {
        pawnId = pre.pawnId;
        prePos = pre.pos;
        postPos = post.pos;
        preAlive = pre.alive;
        postAlive = post.alive;
        preRevealed = pre.revealed;
        postRevealed = post.revealed;
    }
    
    public bool PosChanged => prePos == postPos;
    public bool AliveChanged => preAlive == postAlive;
    public bool RevealedChanged => preRevealed == postRevealed;
}
public struct MoveEvent
{
    public PawnId pawn;
    public Vector2Int from;
    public Vector2Int target;
}
public struct BattleEvent
{
    public PawnId[] participants;
    public PawnId[] revealed;
    public PawnId[] dead;
    public Vector2Int winner_pos;
    public (PawnId pawn, Rank rank, bool wasHidden)[] revealedRanks;
}