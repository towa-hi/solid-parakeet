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
    
    public static string testContract = "CA4RRTB5PMHPXWBW3YXND2NSIVNNJFCSE4NQM4AWONRSZMZFYPFTZFWI";
    public static AccountAddress testHost = "GBAYHJ6GFSXZV5CXQWGNRZ2NU3QR6OBW4RYIHL6EB4IEPYC7JPRVZDR3";
    public static AccountAddress testGuest = "GAOWUE62RVIIDPDFEF4ZOAHECXVEBJJNR66F6TG7F4PWQATZKRNZC53S";
    public static string testHostSneed = "SA25YDMQQ5DSGVSJFEEGNJEMRMITRAA6PQUVRRLDRFUN5GMMBPFVLDVM";
    public static string testGuestSneed = "SD43VTCJENK36DTZD5BJTTHVCWU3ZYYD342S247UE6MK57Y7BABMZVPU";
    public static event Action OnNetworkStateUpdated;
    public static event Action<TrustLineEntry> OnAssetsUpdated;
    public static event Action<TaskInfo> OnTaskStarted;
    public static event Action<TaskInfo> OnTaskEnded;
    
    public static NetworkState networkState;

    static TaskInfo currentTask;

    public static void Initialize()
    {
        currentTask = null;
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
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("make_lobby", req, tracker);
        EndTask(task);
        TransactionResultCode code = ProcessTransactionResult(result, simResult);
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
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("leave_lobby", null, tracker);
        EndTask(task);
        TransactionResultCode code = ProcessTransactionResult(result, simResult);
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
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("join_lobby", req, tracker);
        EndTask(task);
        TransactionResultCode code = ProcessTransactionResult(result, simResult);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        return (int)code;
    }
    
    public static async Task<int> CommitSetupRequest(LobbyId lobbyId, byte[] root, Setup setup, List<CachedRankProof> cached)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"CommitSetupRequest");
        CommitSetupReq req = new()
        {
            lobby_id = lobbyId,
            rank_commitment_root = root,
            setup = setup,
        };
        // store commit first
        CacheManager.StoreHiddenRanksAndProofs(cached, networkState.address, lobbyId);
        TaskInfo task = SetCurrentTask("Invoke commit_setup");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("commit_setup", req, tracker);
        EndTask(task);
        TransactionResultCode code = ProcessTransactionResult(result, simResult);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> CommitMoveRequest(LobbyId lobbyId, byte[] hiddenMoveHash, HiddenMove hiddenMove)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation("CommitMoveRequest");
        CommitMoveReq commitMoveReq = new()
        {
            lobby_id = lobbyId,
            move_hash = hiddenMoveHash,
        };
        ProveMoveReq proveMoveReq = new()
        {
            lobby_id = lobbyId,
            move_proof = hiddenMove,
        };
        
        // NOTE: check lobbyInfo just to see if we should batch in prove_move or not
        TaskInfo reqLobbyInfoTask = SetCurrentTask("ReqLobbyInfo");
        LobbyInfo? preRequestLobbyInfoResult = await stellar.ReqLobbyInfo(lobbyId, tracker);
        EndTask(reqLobbyInfoTask);
        if (preRequestLobbyInfoResult is not LobbyInfo preRequestLobbyInfo)
        {
            return -999;
        }
        bool isHost = preRequestLobbyInfo.IsHost(stellar.userAddress);
        Subphase mySubphase = isHost ? Subphase.Host : Subphase.Guest;
        // store hiddenMove before sending
        CacheManager.StoreHiddenMove(hiddenMove, stellar.userAddress, lobbyId);
        // we send prove_move too only if the server is waiting for us
        bool sendProveMoveToo = preRequestLobbyInfo.phase == Phase.MoveCommit && preRequestLobbyInfo.subphase == mySubphase;
        (GetTransactionResult commitMoveResult, SimulateTransactionResult commitMoveSimResult) results;
        if (sendProveMoveToo)
        {
            TaskInfo task = SetCurrentTask("Invoke commit_move_and_prove_move");
            results = await stellar.CallVoidFunctionWithTwoParameters("commit_move_and_prove_move", commitMoveReq, proveMoveReq, tracker);
            EndTask(task);
        }
        else
        {
            TaskInfo task = SetCurrentTask("Invoke commit_move");
            results = await stellar.CallVoidFunction("commit_move", commitMoveReq, tracker);
            EndTask(task);
        }
        TransactionResultCode code = ProcessTransactionResult(results.commitMoveResult, results.commitMoveSimResult);
        if (code == TransactionResultCode.SUCCESS)
        {
            if (results.commitMoveResult.TransactionResult.result is TransactionResult.resultUnion.TxSUCCESS txSuccess)
            {
                foreach (OperationResult operationResult in txSuccess.results)
                {
                    if (operationResult is OperationResult.OpINNER opInner)
                    {
                        if (opInner.tr is OperationResult.trUnion.InvokeHostFunction invokeHostFunction)
                        {
                            if (invokeHostFunction.invokeHostFunctionResult is InvokeHostFunctionResult.InvokeHostFunctionSuccess invokeHostFunctionSuccess)
                            {
                                Debug.Log("success :)");
                            }
                        }
                    }
                }
            }
        }
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> ProveMoveRequest(LobbyId lobbyId, HiddenMove hiddenMove)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveMoveRequest");
        ProveMoveReq req = new()
        {
            lobby_id = lobbyId,
            move_proof = hiddenMove,
        };
        TaskInfo task = SetCurrentTask("Invoke prove_move");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_move", req, tracker);
        EndTask(task);
        TransactionResultCode code = ProcessTransactionResult(result, simResult);
        tracker.EndOperation();
        Debug.Log(tracker.GetReport());
        await UpdateState();
        return (int)code;
    }

    public static async Task<int> ProveRankRequest(LobbyId lobbyId, HiddenRank[] hiddenRanks, MerkleProof[] merkleProofs)
    {
        TimingTracker tracker = new TimingTracker();
        tracker.StartOperation($"ProveRankRequest");
        ProveRankReq req = new()
        {
            lobby_id = lobbyId,
            hidden_ranks = hiddenRanks,
            merkle_proofs = merkleProofs,
        };
        TaskInfo task = SetCurrentTask("Invoke Prove_rank");
        (GetTransactionResult result, SimulateTransactionResult simResult) = await stellar.CallVoidFunction("prove_rank", req, tracker);
        EndTask(task);
        TransactionResultCode code = ProcessTransactionResult(result, simResult);
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
        if (currentTask == null)
        {
            throw new Exception("Task is not set");
        }
        if (currentTask.taskId != taskInfo.taskId)
        {
            throw new Exception("Task is not taskId");
        }
        OnTaskEnded?.Invoke(currentTask);
        currentTask = null;
    }
    
    // NOTE: lobby IDs will be done server side in the future
    static LobbyId GenerateLobbyId()
    {
        uint value = (uint)Random.Range(100000, 1000000); // 6 digit number
        return new LobbyId(value);
    }
    
    
    static TransactionResultCode ProcessTransactionResult(GetTransactionResult result, SimulateTransactionResult simResult)
    {
        if (simResult == null)
        {
            Debug.LogError("ProcessTransactionResult: simulation failed to send");
            return TransactionResultCode.SIMULATION_FAILED_TO_SEND;
        }
        if (simResult.Error != null)
        {
            Debug.Log(simResult.Error);
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
            return TransactionResultCode.TRANSACTION_SIM_REJECTED_BY_CONTRACT;
        }
        if (result == null)
        {
            return TransactionResultCode.TRANSACTION_FAILED_TO_SEND;
        }
        switch (result.TransactionResult.result)
        {
            case TransactionResult.resultUnion.TxSUCCESS txSuccess:
                return TransactionResultCode.SUCCESS;
                break;
            case TransactionResult.resultUnion.TxFAILED txFailed:
                return TransactionResultCode.TRANSACTION_FAILED_MISC;
                break;
            case TransactionResult.resultUnion.TxfeeBumpInnerFailed txfeeBumpInnerFailed:
                return TransactionResultCode.TRANSACTION_FAILED_MISC;
                break;
            case TransactionResult.resultUnion.TxfeeBumpInnerSuccess txfeeBumpInnerSuccess:
                return TransactionResultCode.TRANSACTION_FAILED_MISC;
                break;
            case TransactionResult.resultUnion.TxinsufficientBalance txinsufficientBalance:
                return TransactionResultCode.TRANSACTION_FAILED_MISC;
                break;
            case TransactionResult.resultUnion.TxinsufficientFee txinsufficientFee:
                return TransactionResultCode.TRANSACTION_FAILED_MISC;
                break;

            case TransactionResult.resultUnion.TxbadAuth txbadAuth:
            case TransactionResult.resultUnion.TxbadAuthExtra txbadAuthExtra:
            case TransactionResult.resultUnion.TxbadMinSeqAgeOrGap txbadMinSeqAgeOrGap:
            case TransactionResult.resultUnion.TxbadSeq txbadSeq:
            case TransactionResult.resultUnion.TxbadSponsorship txbadSponsorship:
            case TransactionResult.resultUnion.TxinternalError txinternalError:
            case TransactionResult.resultUnion.TxMALFORMED txMalformed:
            case TransactionResult.resultUnion.TxmissingOperation txmissingOperation:
            case TransactionResult.resultUnion.TxnoAccount txnoAccount:
            case TransactionResult.resultUnion.TxnotSupported txnotSupported:
            case TransactionResult.resultUnion.TxsorobanInvalid txsorobanInvalid:
            case TransactionResult.resultUnion.TxtooEarly txtooEarly:
            case TransactionResult.resultUnion.TxtooLate txtooLate:
                return TransactionResultCode.TRANSACTION_FAILED_MISC;
                break;
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

public enum TransactionResultCode
{
    SUCCESS,
    SIMULATION_FAILED_TO_SEND = -1,
    TRANSACTION_FAILED_TO_SEND = -2,
    TRANSACTION_NOT_FOUND = -3,
    TRANSACTION_SIM_REJECTED_BY_CONTRACT = -4,
    TRANSACTION_FAILED_MISC = -99,
}