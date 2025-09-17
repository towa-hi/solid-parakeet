using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // Online/Offline mode - always start Offline; no persistence
    enum OnlineModeInternal { Online = 0, Offline = 1 }

    public static event Action OnNetworkStateUpdated;
    public static event Action<GameNetworkState, NetworkDelta> OnGameStateBeforeApplied;
    public static event Action<GameNetworkState, NetworkDelta> OnGameStateAfterApplied;
    public static event Action<TrustLineEntry> OnAssetsUpdated;
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    
    public static NetworkContext networkContext;

    public static NetworkState networkState;

    static TaskInfo currentTask;
    // Tracks tasks that were canceled/aborted so EndTask can ignore them safely
    static HashSet<Guid> canceledTaskIds = new HashSet<Guid>();
    // Centralized polling state
    static bool isPolling;
    static Coroutine pollingCoroutine;
    static bool desiredPolling;
    static int pollingHoldCount;

    public static bool initialized = false;
    
    static StellarManager()
    {
        
    }

    static Result<bool> ErrWithContext<T>(Result<T> inner, string context)
    {
        if (inner.IsOk)
        {
            return Result<bool>.Ok(true);
        }
        string combined = string.IsNullOrEmpty(inner.Message) ? context : ($"{context}: {inner.Message}");
        return Result<bool>.Err(inner.Code, combined);
    }

    // True while a Stellar task is in progress
    public static bool IsBusy => currentTask != null;

    public static void SetContext(bool online, bool isWallet, MuxedAccount userAccount, bool isTestnet, string serverUri, string contractAddress)
    {
        networkContext = new NetworkContext(online, isWallet, userAccount, isTestnet, serverUri, contractAddress);
        Debug.Log($"SetContext: online={online} userAccount={userAccount.AccountId} isWallet={isWallet} serverUri={serverUri} contractAddress={contractAddress}");
        if (isTestnet)
        {
            Network.UseTestNetwork();
        }
        else
        {
            Network.UsePublicNetwork();
        }
    }


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

    public static async Task<Result<bool>> Initialize(ModalConnectData data)
    {
        if (currentTask != null)
        {
            return Result<bool>.Err(StatusCode.OTHER_ERROR, "ConnectToNetwork() is interrupting an existing task");
        }
        if (!data.isWallet && !StrKey.IsValidEd25519SecretSeed(data.sneed))
        {
            return Result<bool>.Err(StatusCode.OTHER_ERROR, "Invalid seed");
        }
        if (!StrKey.IsValidContractId(data.contract))
        {
            return Result<bool>.Err(StatusCode.OTHER_ERROR, "Invalid contract id");
        }
        Uninitialize();
        MuxedAccount userAccount;
        if (data.online)
        {
            if (data.isWallet)
            {
                Result<WalletManager.WalletConnection> walletResult = await WalletManager.ConnectWallet();
                if (walletResult.IsError)
                {
                    Uninitialize();
                    return Result<bool>.Err(walletResult);
                }
                userAccount = MuxedAccount.FromPublicKey(StrKey.DecodeStellarAccountId(walletResult.Value.address));
            }
            else
            {
                userAccount = MuxedAccount.FromSecretSeed(data.sneed);
            }
        }
        else
        {
            userAccount = MuxedAccount.FromSecretSeed(ResourceRoot.DefaultSettings.defaultHostSneed);
        }
        SetContext(data.online, data.isWallet, userAccount, data.isTestnet, data.serverUri, data.contract);
        canceledTaskIds.Clear();
        initialized = true;
        return Result<bool>.Ok(true);
    }

    public static void Uninitialize()
    {
        ResetPolling();
        AbortCurrentTask();
        SetContext(false, false, MuxedAccount.FromSecretSeed(ResourceRoot.DefaultSettings.defaultHostSneed), false, "unused", ResourceRoot.DefaultSettings.defaultContractAddress);
        SetNetworkState(new NetworkState(networkContext));
        initialized = false;
    }

    // public static AccountAddress GetHostAddress()
    // {
    //     MuxedAccount.KeyTypeEd25519 account = MuxedAccount.FromSecretSeed(StellarDotnet.sneed);
    //     string publicAddress = StrKey.EncodeStellarAccountId(account.PublicKey);
    //     return AccountAddress.Parse(publicAddress);
    // }
    
    public static async Task<Result<bool>> UpdateState(bool showTask = true)
    {
        if (!networkContext.online)
        {
            NetworkState previousFakeNetworkState = networkState;
            NetworkState newFakeNetworkState = FakeServer.GetFakeNetworkState();
            bool fakeStateChanged = HasMeaningfulChange(previousFakeNetworkState, newFakeNetworkState);
            SetNetworkState(newFakeNetworkState);
            Debug.Log($"update state fake: changed={fakeStateChanged} prevPhase={(previousFakeNetworkState.lobbyInfo.HasValue ? previousFakeNetworkState.lobbyInfo.Value.phase.ToString() : "-")} prevSub={(previousFakeNetworkState.lobbyInfo.HasValue ? previousFakeNetworkState.lobbyInfo.Value.subphase.ToString() : "-")} prevTurn={(previousFakeNetworkState.gameState.HasValue ? previousFakeNetworkState.gameState.Value.turn.ToString() : "-")} -> currPhase={(newFakeNetworkState.lobbyInfo.HasValue ? newFakeNetworkState.lobbyInfo.Value.phase.ToString() : "-")} currSub={(newFakeNetworkState.lobbyInfo.HasValue ? newFakeNetworkState.lobbyInfo.Value.subphase.ToString() : "-")} currTurn={(newFakeNetworkState.gameState.HasValue ? newFakeNetworkState.gameState.Value.turn.ToString() : "-")}");
            if (fakeStateChanged)
            {
                NetworkDelta delta = ComputeDelta(previousFakeNetworkState, newFakeNetworkState);
                Debug.Log($"delta: phaseChanged={delta.PhaseChanged} turnChanged={delta.TurnChanged} hasResolve={(delta.TurnResolve.HasValue)}");
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
            return Result<bool>.Ok(true);
        }
        using (TaskScope scope = new TaskScope("ReqNetworkState", showTask, "UpdateState"))
        {
            NetworkState previousNetworkState = networkState;
            var newNetworkStateResult = await StellarDotnet.ReqNetworkState(networkContext, scope.tracker);
            if (newNetworkStateResult.IsError)
            {
                if (showTask)
                {
                    // Treat this as an abort so any follow-up EndTask is safely ignored
                    scope.Cancel();
                }
                return ErrWithContext(newNetworkStateResult, "UpdateState: failed to fetch network state");
            }
            NetworkState newNetworkState = newNetworkStateResult.Value;
            // if we got a user with an assigned current_lobby but we couldnt get a lobbyInfo 
            if (newNetworkState.user is User user && user.current_lobby != 0 && newNetworkState.lobbyInfo.HasValue == false)
            {
                // patch users current_lobby to 0 if lobby data is mising
                Debug.LogWarning($"UpdateState(): user.current_lobby is was {newNetworkState.user?.current_lobby} but lobby data is missing - likely expired. Resetting");
                user.current_lobby = new LobbyId(0);
                newNetworkState.user = user;
                Debug.Assert(newNetworkState.lobbyInfo.HasValue == false);
                Debug.Assert(newNetworkState.lobbyParameters.HasValue == false);
                Debug.Assert(newNetworkState.gameState.HasValue == false);
            }
            bool stateChanged = HasMeaningfulChange(previousNetworkState, newNetworkState);
            SetNetworkState(newNetworkState);
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
            return Result<bool>.Ok(true);
        }
    }

    public static async Task<Result<bool>> MakeLobbyRequest(LobbyParameters parameters, bool isMultiplayer)
    {
        if (!networkContext.online || !isMultiplayer)
        {
            FakeServer.MakeLobbyAsHost(parameters);
            FakeServer.JoinLobbyAsGuest(FakeServer.fakeLobbyId);
            return Result<bool>.Ok(true);
        }
        using (TaskScope scope = new TaskScope("MakeLobbyRequest"))
        {
            var result = await StellarDotnet.CallContractFunction(networkContext, "make_lobby", new MakeLobbyReq()
            {
                lobby_id = GenerateLobbyId(),
                parameters = parameters,
            }, scope.tracker);
            return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "MakeLobbyRequest failed");
        }
    }

    public static async Task<Result<bool>> LeaveLobbyRequest()
    {
        if (!networkContext.online)
        {
            FakeServer.Reset();
            await UpdateState();
            return Result<bool>.Ok(true);
        }
        using (TaskScope scope = new TaskScope("LeaveLobbyRequest"))
        {
            var result = await StellarDotnet.CallContractFunction(networkContext, "leave_lobby", new IScvMapCompatable[]
            {
                // intentionally empty
            }, scope.tracker);
            return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "LeaveLobbyRequest failed");
        }
    }

    public static async Task<Result<bool>> JoinLobbyRequest(LobbyId lobbyId)
    {
        if (!networkContext.online)
        {
            FakeServer.JoinLobbyAsGuest(lobbyId);
            return Result<bool>.Ok(true);
        }
        using (TaskScope scope = new TaskScope("JoinLobbyRequest"))
        {
            var result = await StellarDotnet.CallContractFunction(networkContext, "join_lobby", new JoinLobbyReq()
            {
                lobby_id = lobbyId,
            }, scope.tracker);
            return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "JoinLobbyRequest failed");
        }
    }

    public static async Task<PackedHistory?> GetPackedHistory(uint lobbyId)
    {
        if (!networkContext.online)
        {
            throw new Exception("GetPackedHistory is not supported in offline mode");
        }
        using (TaskScope scope = new TaskScope("GetPackedHistory"))
        {
            var result = await StellarDotnet.ReqPackedHistory(networkContext, lobbyId, scope.tracker);
            if (result.IsError)
            {
                Debug.LogError($"GetPackedHistory() failed with error {result.Code} {result.Message}");
            }
            return result.IsError ? null : result.Value;
        }
    }

    public static async Task<Result<bool>> CommitSetupRequest(CommitSetupReq req)
    {
        if (!networkContext.online)
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
            return Result<bool>.Ok(true);
        }
        using (TaskScope scope = new TaskScope("CommitSetupRequest"))
        {
            var result = await StellarDotnet.CallContractFunction(networkContext, "commit_setup", req, scope.tracker);
            // result.Err already logs
            return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "CommitSetupRequest failed");
        }
    }

    public static async Task<Result<bool>> CommitMoveRequest(CommitMoveReq commitMoveReq, ProveMoveReq proveMoveReq, AccountAddress userAddress, LobbyInfo lobbyInfo, LobbyParameters lobbyParameters)
    {
        Debug.Assert(commitMoveReq.lobby_id == proveMoveReq.lobby_id);
        Debug.Assert(commitMoveReq.move_hashes.Length == proveMoveReq.move_proofs.Length);
        Debug.Assert(lobbyInfo.IsMySubphase(userAddress));
        Debug.Assert(lobbyInfo.phase == Phase.MoveCommit);

        if (!networkContext.online)
        {

            Debug.Log("CommitMoveRequest fake guest move");
            Team myTeam = lobbyInfo.GetMyTeam(userAddress, lobbyParameters.host_team);
            Team otherTeam = myTeam == Team.RED ? Team.BLUE : Team.RED;
            List<HiddenMove> fakeGuestMoveProofs;
            using (TaskScope scope = new TaskScope("CommitMoveRequest (offline)"))
            {
                Debug.Log("task started");
                fakeGuestMoveProofs = await FakeServer.TempFakeHiddenMoves(otherTeam);
                Debug.Log("ending task");
            }
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
            ProveMoveReq fakeGuestProveMoveReq = new()
            {
                lobby_id = commitMoveReq.lobby_id,
                move_proofs = fakeGuestMoveProofs.ToArray(),
            };
            FakeServer.CommitMoveAndProveMove(fakeGuestCommitMoveReq, fakeGuestProveMoveReq, false);
            Debug.Log("CommitMoveRequest fake host move");
            FakeServer.CommitMoveAndProveMove(commitMoveReq, proveMoveReq, true);
            Debug.Log("CommitMoveRequest fake done");
            return Result<bool>.Ok(true);
        }
        using (TaskScope scope = new TaskScope("CommitMoveRequest"))
        {
            // If opponent has already made a moveCommit, we can safely batch
            // If security mode is disabled, we can always batch
            bool canBatchProveMove = lobbyInfo.subphase != Subphase.Both || !lobbyParameters.security_mode;
            Result<(SimulateTransactionResult, SendTransactionResult, GetTransactionResult)> result;
            if (canBatchProveMove) {
                result = await StellarDotnet.CallContractFunction(networkContext, "commit_move_and_prove_move", new IScvMapCompatable[] {commitMoveReq, proveMoveReq}, scope.tracker);
            } else {
                result = await StellarDotnet.CallContractFunction(networkContext, "commit_move", commitMoveReq, scope.tracker);
            }
            // result.Err already logs
            return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "CommitMoveRequest failed");
        }
    }

    public static async Task<Result<bool>> ProveMoveRequest(ProveMoveReq proveMoveReq, AccountAddress userAddress, LobbyInfo lobbyInfo, LobbyParameters lobbyParameters)
    {
        Debug.Assert(lobbyInfo.phase == Phase.MoveProve);
        Debug.Assert(lobbyParameters.security_mode);
        Debug.Assert(lobbyInfo.IsMySubphase(userAddress));
        using (TaskScope scope = new TaskScope("ProveMoveRequest"))
        {
        bool canSimulate = lobbyInfo.phase == Phase.MoveProve && lobbyInfo.subphase != Subphase.Both;
        PawnId[] neededRankProofs = Array.Empty<PawnId>();
        if (canSimulate)
        {
            var simResult = await StellarDotnet.SimulateContractFunction(networkContext, "simulate_collisions", new IScvMapCompatable[] { proveMoveReq }, scope.tracker);
            if (simResult.IsError)
            {
                return ErrWithContext(simResult, "ProveMoveRequest: simulate_collisions failed");
            }
            var (_, simulateTransactionResult) = simResult.Value;
            if (simulateTransactionResult.Error != null)
            {
                return Result<bool>.Err(StatusCode.SIMULATION_FAILED, "ProveMoveRequest: simulation returned error");
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
            result = await StellarDotnet.CallContractFunction(networkContext, "prove_move_and_prove_rank", new IScvMapCompatable[] { proveMoveReq, proveRankReq }, scope.tracker);
        }
        else
        {
            result = await StellarDotnet.CallContractFunction(networkContext, "prove_move", proveMoveReq, scope.tracker);
        }
        // result.Err already logs
        return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "ProveMoveRequest failed");
        }
    }

    public static async Task<Result<bool>> ProveRankRequest(ProveRankReq req)
    {
        using (TaskScope scope = new TaskScope("Invoke Prove_rank", true, "ProveRankRequest"))
        {
            var result = await StellarDotnet.CallContractFunction(networkContext, "prove_rank", req, scope.tracker);
            // result.Err already logs
            return result.IsOk ? Result<bool>.Ok(true) : ErrWithContext(result, "ProveRankRequest failed");
        }
    }

    public static async Task<Result<AccountEntry>> GetAccount(string key)
    {
        using (TaskScope scope = new TaskScope("ReqAccountEntry", true, "GetAccount"))
        {
            var result = await StellarDotnet.ReqAccountEntry(networkContext, scope.tracker);
            if (result.IsError)
            {
                Debug.LogError($"GetAccount() failed with error {result.Code} {result.Message}");
            }
            return result;
        }
    }

    public static async Task<Result<TrustLineEntry>> GetAssets(string userId)
    {
        using (TaskScope scope = new TaskScope("ReqAssets"))
        {
            var result = await StellarDotnet.GetAssets(networkContext, scope.tracker);
            if (result.IsError)
            {
                Debug.LogError($"GetAssets() failed with error {result.Code} {result.Message}");
                return Result<TrustLineEntry>.Err(result);
            }
            TrustLineEntry trustLineEntry = result.Value.trustLine;
            return Result<TrustLineEntry>.Ok(trustLineEntry);
        }
    }

    // Start/stop centralized polling. Polling will automatically skip while busy
    public static void SetPolling(bool enable)
    {
        if (!networkContext.online && enable)
        {
            throw new Exception("SetPolling is not supported in offline mode");
        }
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
        // TODO: figure out how to handle offline mode
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

    static void ResetPolling()
    {
        desiredPolling = false;
        pollingHoldCount = 0;
        isPolling = false;
        if (pollingCoroutine != null)
        {
            CoroutineRunner.instance.StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
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
        if (previous.fromOnline != current.fromOnline)
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

    static void SetNetworkState(NetworkState newState)
    {
        Debug.Log("network state updated");
        networkState = newState;
        DebugNetworkInspector.UpdateDebugNetworkInspector();
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

    public sealed class TaskScope : IDisposable
    {
        readonly bool showTask;
        bool canceled;
        readonly TaskInfo taskInfo;
        readonly string opName;
        public readonly TimingTracker tracker;

        public TaskScope(string taskMessage, bool showTask = true, string opName = null)
        {
            this.showTask = showTask;
            this.opName = opName ?? taskMessage;
            if (showTask)
            {
                taskInfo = SetCurrentTask(taskMessage);
            }
            tracker = new TimingTracker();
            tracker.StartOperation(this.opName);
        }

        public void Cancel()
        {
            if (showTask && !canceled)
            {
                AbortCurrentTask();
                canceled = true;
            }
        }

        public void Dispose()
        {
            tracker.EndOperation();
            DebugLogStellarManager(tracker.GetReport());
            if (showTask && !canceled && taskInfo != null)
            {
                EndTask(taskInfo);
            }
        }
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

    // TODO: lobby IDs will be done server side in the future
    static LobbyId GenerateLobbyId()
    {
        uint value = (uint)Random.Range(100000, 1000000); // 6 digit number
        return new LobbyId(value);
    }

}

public class TaskInfo
{
    public Guid taskId;
    public string taskMessage;
}

 
