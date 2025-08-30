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
    public static bool isWallet;
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
    static void DebugLogStellarManager(string message)
    {
        if (ResourceRoot.DefaultSettings.stellarManagerLogging)
        {
            Debug.Log(message);
        }
    }
    public static void Initialize(ModalConnectData data)
    {
        DefaultSettings defaultSettings = ResourceRoot.DefaultSettings;
        currentTask = null;
        canceledTaskIds.Clear();
        if (data.isWallet)
        {
            data.isWallet = true;
            Debug.Log("Using fake sneed for now because wallet mode");
            data.sneed = defaultSettings.defaultHostSneed;
        }
        StellarDotnet.Initialize(data.isTestnet, data.sneed, data.contract);
    }

    public static AccountAddress GetHostAddress()
    {
        MuxedAccount.KeyTypeEd25519 account = MuxedAccount.FromSecretSeed(StellarDotnet.sneed);
        string publicAddress = StrKey.EncodeStellarAccountId(account.PublicKey);
        return AccountAddress.Parse(publicAddress);
    }
    

    public static async Task<StatusCode> Connect()
    {
        TaskInfo task = SetCurrentTask("Connect");
        TimingTracker tracker = new();
        tracker.StartOperation("Connect");
        var publicAddress = MuxedAccount.FromSecretSeed(StellarDotnet.sneed);
        // check to make sure address exists on network
        var result = await StellarDotnet.ReqAccountEntry(publicAddress);
        if (result.IsError)
        {
            Debug.LogError($"Connect() ReqAccountEntry failed with error {result.Code} {result.Message}");
            tracker.EndOperation();
            DebugLogStellarManager(tracker.GetReport());
            EndTask(task);
            return result.Code;
        }
        // check to make sure contract exists on network
        var userResult = await StellarDotnet.ReqUser(publicAddress.AccountId, tracker);
        if (userResult.IsError)
        {
            Debug.LogError($"Connect() ReqUser failed with error {userResult.Code} {userResult.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }
    public static async Task<StatusCode> UpdateState(bool showTask = true)
    {
        if (!GameManager.instance.IsOnline())
        {
            NetworkState previousFakeNetworkState = networkState;
            NetworkState newFakeNetworkState = FakeServer.GetFakeNetworkState();
            networkState = FakeServer.GetFakeNetworkState();
            bool fakeStateChanged = HasMeaningfulChange(previousFakeNetworkState, newFakeNetworkState);
            networkState = newFakeNetworkState;
            Debug.Log("update state fake");
            if (fakeStateChanged)
            {
                NetworkDelta delta = ComputeDelta(previousFakeNetworkState, newFakeNetworkState);
                if (HasCompleteGameData(newFakeNetworkState))
                {
                    Debug.Log("firing events");
                    GameNetworkState game = new(newFakeNetworkState);
                    OnGameStateBeforeApplied?.Invoke(game, delta);
                    OnNetworkStateUpdated?.Invoke();
                    OnGameStateAfterApplied?.Invoke(game, delta);
                }
                else
                {
                    Debug.Log("firing just onnetworkstateupdated");
                    OnNetworkStateUpdated?.Invoke();
                }
            }
            return StatusCode.SUCCESS;
        }
        TaskInfo getNetworkStateTask = showTask ? SetCurrentTask("ReqNetworkState") : null;
        TimingTracker tracker = new();
        tracker.StartOperation("UpdateState");
        NetworkState previousNetworkState = networkState;
        var newNetworkStateResult = await StellarDotnet.ReqNetworkState(tracker);
        if (newNetworkStateResult.IsError)
        {
            Debug.LogError($"UpdateState() ReqNetworkState failed with error {newNetworkStateResult.Code} {newNetworkStateResult.Message}");
            tracker.EndOperation();
            DebugLogStellarManager(tracker.GetReport());
            if (showTask)
            {
                // Treat this as an abort so any follow-up EndTask is safely ignored
                AbortCurrentTask();
            }
            return newNetworkStateResult.Code;
        }
        NetworkState newNetworkState = newNetworkStateResult.Value;
        // adjust state if user lobby expired
        if (newNetworkState.user is User user && user.current_lobby != 0)
        {
            Debug.LogWarning($"UpdateState(): user.current_lobby is was {newNetworkState.user?.current_lobby} but lobby data is missing - likely expired. Resetting");
            user.current_lobby = new LobbyId(0);
            newNetworkState.user = user;
            Debug.Assert(newNetworkState.lobbyInfo.HasValue == false);
            Debug.Assert(newNetworkState.lobbyParameters.HasValue == false);
            Debug.Assert(newNetworkState.gameState.HasValue == false);
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
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        if (showTask)
        {
            EndTask(getNetworkStateTask);
        }
        return StatusCode.SUCCESS;
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

    public static async Task<StatusCode> MakeLobbyRequest(LobbyParameters parameters)
    {
        if (!GameManager.instance.IsOnline())
        {
            FakeServer.MakeLobbyAsHost(parameters);
            FakeServer.JoinLobbyAsGuest(FakeServer.fakeLobbyId);
            return StatusCode.SUCCESS;
        }
        // Pause polling during contract invocation
        TimingTracker tracker = new();
        tracker.StartOperation($"MakeLobbyRequest");
        TaskInfo task = SetCurrentTask("MakeLobbyRequest");
        var result = await StellarDotnet.CallContractFunction("make_lobby", new MakeLobbyReq()
        {
            lobby_id = GenerateLobbyId(),
            parameters = parameters,
        }, tracker);
        if (result.IsError)
        {
            Debug.LogError($"MakeLobbyRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }

    public static async Task<StatusCode> LeaveLobbyRequest()
    {
        TimingTracker tracker = new();
        tracker.StartOperation($"LeaveLobbyRequest");
        TaskInfo task = SetCurrentTask("LeaveLobbyRequest");
        var result = await StellarDotnet.CallContractFunction("leave_lobby", new IScvMapCompatable[] 
        {
            // intentionally empty
        }, tracker);
        if (result.IsError)
        {
            Debug.LogError($"LeaveLobbyRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }
    
    public static async Task<StatusCode> JoinLobbyRequest(LobbyId lobbyId)
    {
        if (!GameManager.instance.IsOnline())
        {
            FakeServer.JoinLobbyAsGuest(lobbyId);
            return StatusCode.SUCCESS;
        }
        TaskInfo task = SetCurrentTask("JoinLobbyRequest");
        TimingTracker tracker = new();
        tracker.StartOperation($"JoinLobbyRequest");
        var result = await StellarDotnet.CallContractFunction("join_lobby", new JoinLobbyReq()
        {
            lobby_id = lobbyId,
        }, tracker);
        if (result.IsError)
        {
            Debug.LogError($"JoinLobbyRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }
    
    public static async Task<PackedHistory?> GetPackedHistory(uint lobbyId)
    {
        TaskInfo task = SetCurrentTask("GetPackedHistory");
        TimingTracker tracker = new();
        tracker.StartOperation("GetPackedHistory");
        var result = await StellarDotnet.ReqPackedHistory(lobbyId, tracker);
        if (result.IsError)
        {
            Debug.LogError($"GetPackedHistory() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.IsError ? null : result.Value;
    }
    
    public static async Task<StatusCode> CommitSetupRequest(CommitSetupReq req)
    {
        if (!GameManager.instance.IsOnline())
        {
            Debug.Log("CommitSetupRequest fake");
            // pretend the guest went first
            var guestSetup = FakeServer.GetFakeState().AutoSetup(Team.BLUE);
            List<HiddenRank> guestHiddenRanks = new();
            foreach ((Vector2Int pos, Rank rank) in guestSetup)
            {
                HiddenRank hiddenRank = new()
                {
                    pawn_id = new PawnId(pos, Team.BLUE),
                    rank = rank,
                    salt = Globals.RandomSalt(),
                };
                guestHiddenRanks.Add(hiddenRank);
            }
            CommitSetupReq fakeGuestReq = new()
            {
                lobby_id = req.lobby_id,
                rank_commitment_root = req.rank_commitment_root,
                zz_hidden_ranks = guestHiddenRanks.ToArray(),
            };
            FakeServer.CommitSetup(fakeGuestReq, false);
            Debug.Log("CommitSetupRequest fake guest done");
            // now host goes
            FakeServer.CommitSetup(req, true);
            Debug.Log("CommitSetupRequest fake host done");
            return StatusCode.SUCCESS;
        }
        TaskInfo task = SetCurrentTask("CommitSetupRequest");
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"CommitSetupRequest");
        var result = await StellarDotnet.CallContractFunction("commit_setup", req, tracker);
        if (result.IsError)
        {
            Debug.LogError($"CommitSetupRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }

    public static async Task<StatusCode> CommitMoveRequest(CommitMoveReq commitMoveReq, ProveMoveReq proveMoveReq, AccountAddress userAddress, LobbyInfo lobbyInfo, LobbyParameters lobbyParameters)
    {
        Debug.Assert(commitMoveReq.lobby_id == proveMoveReq.lobby_id);
        Debug.Assert(commitMoveReq.move_hashes.Length == proveMoveReq.move_proofs.Length);
        Debug.Assert(lobbyInfo.IsMySubphase(userAddress));
        Debug.Assert(lobbyInfo.phase == Phase.MoveCommit);

        if (!GameManager.instance.IsOnline())
        {
            Debug.Log("CommitMoveRequest fake guest move");
            List<HiddenMove> fakeGuestMoveProofs = FakeServer.TempFakeHiddenMoves(Team.BLUE);
            List<byte[]> fakeGuestMoveHashes = new();
            foreach (HiddenMove move in fakeGuestMoveProofs)
            {
                fakeGuestMoveHashes.Add(SCUtility.Get16ByteHash(move));
            }
            CommitMoveReq fakeGuestCommitMoveReq = new()
            {
                lobby_id = commitMoveReq.lobby_id,
                move_hashes = fakeGuestMoveHashes.ToArray(),
            };
            ProveMoveReq fakeHostProveMoveReq = new()
            {
                lobby_id = commitMoveReq.lobby_id,
                move_proofs = fakeGuestMoveProofs.ToArray(),
            };
            FakeServer.CommitMoveAndProveMove(commitMoveReq, proveMoveReq, false);
            Debug.Log("CommitMoveRequest fake host move");
            FakeServer.CommitMoveAndProveMove(commitMoveReq, proveMoveReq, true);
            Debug.Log("CommitMoveRequest fake done");
            return StatusCode.SUCCESS;
        }
        TaskInfo task = SetCurrentTask("CommitMoveRequest");
        TimingTracker tracker = new();
        tracker.StartOperation("CommitMoveRequest");
        // If opponent has already made a moveCommit, we can safely batch
        // If security mode is disabled, we can always batch
        bool canBatchProveMove = lobbyInfo.subphase != Subphase.Both || !lobbyParameters.security_mode;
        Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)> result;
        if (canBatchProveMove) {
            result = await StellarDotnet.CallContractFunction("commit_move_and_prove_move", new IScvMapCompatable[] {commitMoveReq, proveMoveReq}, tracker);
        } else {
            result = await StellarDotnet.CallContractFunction("commit_move", commitMoveReq, tracker);
        }
        if (result.IsError) {
            Debug.LogError($"CommitMoveRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }

    public static async Task<StatusCode> ProveMoveRequest(ProveMoveReq proveMoveReq, AccountAddress userAddress, LobbyInfo lobbyInfo, LobbyParameters lobbyParameters)
    {
        Debug.Assert(lobbyInfo.phase == Phase.MoveProve);
        Debug.Assert(lobbyParameters.security_mode);
        Debug.Assert(lobbyInfo.IsMySubphase(userAddress));

        TaskInfo task = SetCurrentTask("ProveMoveRequest");
        TimingTracker tracker = new();
        tracker.StartOperation($"ProveMoveRequest");
        bool canSimulate = lobbyInfo.phase == Phase.MoveProve && lobbyInfo.subphase != Subphase.Both;
        PawnId[] neededRankProofs = Array.Empty<PawnId>();
        if (canSimulate)
        {
            var simResult = await StellarDotnet.SimulateContractFunction("simulate_collisions", new IScvMapCompatable[] { proveMoveReq }, tracker);
            if (simResult.IsError)
            {
                Debug.LogError($"ProveMoveRequest() failed with error {simResult.Code} {simResult.Message}");
                tracker.EndOperation();
                DebugLogStellarManager(tracker.GetReport());
                EndTask(task);
                return simResult.Code;
            }
            var (_, simulateTransactionResult) = simResult.Value;
            if (simulateTransactionResult.Error != null)
            {
                Debug.LogError($"ProveMoveRequest() simulate_collisions failed with error {simResult.Code} {simResult.Message}");
                tracker.EndOperation();
                DebugLogStellarManager(tracker.GetReport());
                EndTask(task);
                return StatusCode.SIMULATION_FAILED;
            }
            SCVal scVal = simulateTransactionResult.Results.FirstOrDefault()!.Result;
            UserMove simulatedMove = SCUtility.SCValToNative<UserMove>(scVal);
            neededRankProofs = simulatedMove.needed_rank_proofs;
        }
        Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)> result;
        if (neededRankProofs.Length > 0)
        {
            // construct a proveRankReq
            List<HiddenRank> hiddenRanks = new();
            List<MerkleProof> merkleProofs = new();
            foreach (PawnId pawnId in neededRankProofs)
            {
                if (CacheManager.GetHiddenRankAndProof(pawnId) is not CachedRankProof cachedRankProof)
                {
                    throw new Exception($"cachemanager could not find pawn {pawnId}");
                }
                hiddenRanks.Add(cachedRankProof.hidden_rank);
                merkleProofs.Add(cachedRankProof.merkle_proof);
            }
            ProveRankReq proveRankReq = new()
            {
                hidden_ranks = hiddenRanks.ToArray(),
                lobby_id = proveMoveReq.lobby_id,
                merkle_proofs = merkleProofs.ToArray(),
            };
            result = await StellarDotnet.CallContractFunction("prove_move_and_prove_rank", new IScvMapCompatable[] { proveMoveReq, proveRankReq }, tracker);
        }
        else
        {
            result = await StellarDotnet.CallContractFunction("prove_move", proveMoveReq, tracker);
        }
        if (result.IsError)
        {
            Debug.LogError($"ProveMoveRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }

    public static async Task<StatusCode> ProveRankRequest(ProveRankReq req)
    {
        TaskInfo task = SetCurrentTask("Invoke Prove_rank");
        TimingTracker tracker = new();
        tracker.StartOperation($"ProveRankRequest");
        var result = await StellarDotnet.CallContractFunction("prove_rank", req, tracker);
        if (result.IsError)
        {
            Debug.LogError($"ProveRankRequest() failed with error {result.Code} {result.Message}");
        }
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        EndTask(task);
        return result.Code;
    }
    
    public static async Task<Result<AccountEntry>> GetAccount(string key)
    {
        TimingTracker tracker = new();
        tracker.StartOperation($"GetAccount");
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        var result = await StellarDotnet.ReqAccountEntry(MuxedAccount.FromAccountId(key));
        if (result.IsError)
        {
            Debug.LogError($"GetAccount() failed with error {result.Code} {result.Message}");
        }
        EndTask(task);
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        return result;
    }

    public static async Task<Result<TrustLineEntry>> GetAssets(string userId)
    {
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        TimingTracker tracker = new();
        tracker.StartOperation($"GetAssets");
        var userAccount = MuxedAccount.FromAccountId(userId);
        var result = await StellarDotnet.GetAssets(userAccount, tracker);
        if (result.IsError)
        {
            Debug.LogError($"GetAssets() failed with error {result.Code} {result.Message}");
        }
        TrustLineEntry trustLineEntry = result.Value.trustLine;
        EndTask(task);
        tracker.EndOperation();
        DebugLogStellarManager(tracker.GetReport());
        return Result<TrustLineEntry>.Ok(trustLineEntry);
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
        NetworkDelta delta = new();
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
        // Compare raw GameState pawn arrays directly; they are index-aligned and consistent across states
        PawnState[] pre = previous.gameState.pawns;
        PawnState[] post = current.gameState.pawns;
        Dictionary<PawnId, SnapshotPawnDelta> pawnDeltas = new();
        List<BattleEvent> battles = new();
        Dictionary<PawnId, MoveEvent> moves = new();
        for (int i = 0; i < pre.Length; i++)
        {
            PawnState prePawn = pre[i];
            PawnState postPawn = post[i];
            SnapshotPawnDelta pawnDelta = new(prePawn, postPawn);
            pawnDeltas[pawnDelta.pawnId] = pawnDelta;
            if (prePawn.alive && prePawn.pos != postPawn.pos)
            {
                moves[prePawn.pawn_id] = new MoveEvent
                {
                    pawn = prePawn.pawn_id,
                    from = prePawn.pos,
                    target = postPawn.pos,
                };
            }
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
                winnerPos = pos,
                revealedRanks = Array.Empty<(PawnId pawn, Rank rank, bool wasHidden)>(),
            });
        }
        
        return new TurnResolveDelta {
            pawnDeltas = pawnDeltas,
            moves = moves,
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
        if (previous.online != current.online)
        {
            return true;
        }
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
        PushPollingHold();
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
        PopPollingHold();
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
