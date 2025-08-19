using System;
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
    public static StellarDotnet stellar;
    
    public static string testContract = "CCDRCLUFJW7V5PZB57O6NGPD7XCFMWOB55QFSIZNQ3YQZW34A75J7OCF";
    // public static AccountAddress testHost = "GCLG5VFHJTLIZDRH575XZ36OSZ2FQKQCAAHMIS3Y5EBDPETOMD2TPI6T";
    // public static AccountAddress testGuest = "GARMTOT4WI4DWVOT7JALAZ2ZUOBY2GV7L5QDIZYADYG7W2L4CAK52SG6";
    public static string testHostSneed = "SDZIXICHJQMKQADTZXIFBPYR5PRH5J57F2AIZ4EP3JYDXICTSGF5WIBA";
    public static string testGuestSneed = "SBRXO3DVIUPR3DSS3L3MOLAFTNVBXA26NXCOVJJP2R2LOJZRG5PRWYE2";
    public static event Action OnNetworkStateUpdated;
    public static event Action<TrustLineEntry> OnAssetsUpdated;
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    
    public static NetworkState networkState;

    static TaskInfo currentTask;
    // Tracks tasks that were canceled/aborted so EndTask can ignore them safely
    static HashSet<Guid> canceledTaskIds = new HashSet<Guid>();
    
    // True while a Stellar task is in progress
    public static bool IsBusy => currentTask != null;

    public static AccountAddress GetHostAddress()
    {
        MuxedAccount.KeyTypeEd25519 account = MuxedAccount.FromSecretSeed(testHostSneed);
        string publicAddress = StrKey.EncodeStellarAccountId(account.PublicKey);
        return AccountAddress.Parse(publicAddress);
    }
    public static void Initialize()
    {
        currentTask = null;
        canceledTaskIds.Clear();
        networkState = new NetworkState();
        stellar = new StellarDotnet(testHostSneed, testContract);
    }
    
    public static async Task<bool> UpdateState()
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("UpdateState");
        TaskInfo getNetworkStateTask = SetCurrentTask("ReqNetworkState");
        networkState = await stellar.ReqNetworkState(tracker);
        EndTask(getNetworkStateTask);
        if (networkState.user is User user && user.current_lobby != 0)
        {
            if (!networkState.lobbyInfo.HasValue || !networkState.lobbyParameters.HasValue)
            {
                Debug.LogWarning($"UpdateState(): user.current_lobby is set to {networkState.user?.current_lobby} but lobby data is missing - likely expired");
                User updatedUser = user;
                updatedUser.current_lobby = new LobbyId(0);
                networkState.user = updatedUser;
                Debug.Log("Set current_lobby to null due to expired/missing lobby");
            }
        }
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        OnNetworkStateUpdated?.Invoke();
        return true;
    }
    
    public static async Task<bool> SetContractAddress(string contractId)
    {
        stellar.SetContractId(contractId);
        Debug.Log("OnContractAddressUpdated");
        await UpdateState();
        return true;
    }

    public static async Task<bool> SetSneed(string accountSneed)
    {
        stellar.SetSneed(accountSneed);
        Debug.Log("OnSneedUpdated");
        await UpdateState();
        return true;
    }

    public static string GetUserAddress()
    {
        return stellar.sneed != null ? stellar.userAddress : null;
    }

    public static string GetContractAddress()
    {
        return stellar.contractAddress;
    }

    public static async Task<int> MakeLobbyRequest(LobbyParameters parameters)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"MakeLobbyRequest");
        MakeLobbyReq req = new()
        {
            lobby_id = GenerateLobbyId(),
            parameters = parameters,
        };
        TaskInfo task = SetCurrentTask("Invoke make_lobby");
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await stellar.CallContractFunction("make_lobby", req, tracker);
        EndTask(task);
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> LeaveLobbyRequest()
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"LeaveLobbyRequest");
        TaskInfo task = SetCurrentTask("Invoke leave_lobby");
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await stellar.CallContractFunction("leave_lobby", new IScvMapCompatable[] {}, tracker);
        EndTask(task);
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }
    
    public static async Task<int> JoinLobbyRequest(LobbyId lobbyId)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"JoinLobbyRequest");
        JoinLobbyReq req = new()
        {
            lobby_id = lobbyId,
        };
        TaskInfo task = SetCurrentTask("CallVoidFunction");
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await stellar.CallContractFunction("join_lobby", req, tracker);
        EndTask(task);
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        return (int)code;
    }
    
    public static async Task<PackedHistory?> GetPackedHistory(uint lobbyId)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("GetPackedHistory");
        TaskInfo task = SetCurrentTask("ReqPackedHistory");
        PackedHistory? history = await stellar.ReqPackedHistory(lobbyId, tracker);
        EndTask(task);
        tracker.EndOperation();
        return history;
    }
    
    public static async Task<int> CommitSetupRequest(CommitSetupReq req)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"CommitSetupRequest");
        TaskInfo task = SetCurrentTask("Invoke commit_setup");
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results  = await stellar.CallContractFunction("commit_setup", req, tracker);
        EndTask(task);
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> CommitMoveRequest(CommitMoveReq commitMoveReq, ProveMoveReq proveMoveReq)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("CommitMoveRequest");
        Debug.Assert(commitMoveReq.lobby_id == proveMoveReq.lobby_id);
        // NOTE: check lobbyInfo just to see if we should batch in prove_move or not
        TaskInfo reqLobbyInfoTask = SetCurrentTask("ReqLobbyInfo");
        LobbyInfo? preRequestLobbyInfoResult = await stellar.ReqLobbyInfo(commitMoveReq.lobby_id, tracker);
        EndTask(reqLobbyInfoTask);
        if (preRequestLobbyInfoResult is not LobbyInfo preRequestLobbyInfo)
        {
            return -999;
        }
        bool isHost = preRequestLobbyInfo.IsHost(stellar.userAddress);
        Subphase mySubphase = isHost ? Subphase.Host : Subphase.Guest;
        // we send prove_move too only if the server is waiting for us or if secure_mode false
        bool sendProveMoveToo = preRequestLobbyInfo.phase == Phase.MoveCommit && preRequestLobbyInfo.subphase == mySubphase;
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results;
        if (sendProveMoveToo || networkState.lobbyParameters?.security_mode == false)
        {
            TaskInfo task = SetCurrentTask("Invoke commit_move_and_prove_move");
            results = await stellar.CallContractFunction("commit_move_and_prove_move", new IScvMapCompatable[] {commitMoveReq, proveMoveReq}, tracker);
            EndTask(task);
        }
        else
        {
            TaskInfo task = SetCurrentTask("Invoke commit_move");
            results = await stellar.CallContractFunction("commit_move", commitMoveReq, tracker);
            EndTask(task);
        }
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> ProveMoveRequest(ProveMoveReq proveMoveReq)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveMoveRequest");
        bool sendRankProofToo = false;
        bool isSecurityMode = networkState.lobbyParameters?.security_mode == true;
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results;
        UserMove? simulatedMove = null;
        if (isSecurityMode && networkState.lobbyInfo.HasValue)
        {
            LobbyInfo lobby = networkState.lobbyInfo.Value;
            bool canSimulate = lobby.phase == Phase.MoveProve && lobby.subphase != Subphase.Both;
            if (canSimulate)
            {
                TaskInfo reqLobbyInfoTask = SetCurrentTask("Simulate simulate_collisions");
                (_, SimulateTransactionResult collisionResult) = await stellar.SimulateContractFunction("simulate_collisions", new IScvMapCompatable[] { proveMoveReq }, tracker);
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
            results = await stellar.CallContractFunction("prove_move_and_prove_rank", new IScvMapCompatable[] { proveMoveReq, proveRankReq }, tracker);
            EndTask(task);
        }
        else
        {
            TaskInfo task = SetCurrentTask("Invoke prove_move");
            results = await stellar.CallContractFunction("prove_move", proveMoveReq, tracker);
            EndTask(task);
        }
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> ProveRankRequest(ProveRankReq req)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveRankRequest");
        TaskInfo task = SetCurrentTask("Invoke Prove_rank");
        (SimulateTransactionResult, SendTransactionResult, GetTransactionResult) results = await stellar.CallContractFunction("prove_rank", req, tracker);
        EndTask(task);
        ResultCode code = ProcessTransactionResult(results);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }
    
    public static async Task<AccountEntry> GetAccount(string key)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"GetAccount");
        TaskInfo task = SetCurrentTask("ReqAccountEntry");
        AccountEntry result = await stellar.ReqAccountEntry(MuxedAccount.FromAccountId(key));
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
        LedgerEntry.dataUnion.Trustline result = await stellar.GetAssets(userAccount, tracker);
        EndTask(task);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        OnAssetsUpdated?.Invoke(result.trustLine);
        return result.trustLine;
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
